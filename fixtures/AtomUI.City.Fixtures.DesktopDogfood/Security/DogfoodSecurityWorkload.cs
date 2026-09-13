using System.Security.Claims;
using AtomUI.City.Security;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodSecurityWorkload
{
    private readonly AuthenticationStateStore _authentication;
    private readonly PermissionRegistry _permissions;
    private readonly InMemoryAuthorizationPolicyProvider _policies;
    private readonly IAuthorizationEvaluator _authorization;
    private readonly IAccessTokenProvider _tokens;
    private readonly IAccountSessionStore _accountStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IAccountSessionManager _accountSessions;
    private readonly DogfoodRefreshingAccessTokenProvider _refreshingTokens;
    private readonly DogfoodSecurityWorkspace _securityWorkspace;
    private readonly ApplicationStateRegistry _stateRegistry;

    public DogfoodSecurityWorkload(
        AuthenticationStateStore authentication,
        PermissionRegistry permissions,
        InMemoryAuthorizationPolicyProvider policies,
        IAuthorizationEvaluator authorization,
        IAccessTokenProvider tokens,
        IAccountSessionStore accountStore,
        ICredentialStore credentialStore,
        IAccountSessionManager accountSessions,
        DogfoodRefreshingAccessTokenProvider refreshingTokens,
        DogfoodSecurityWorkspace securityWorkspace,
        IStateRegistry stateRegistry)
    {
        _authentication = authentication;
        _permissions = permissions;
        _policies = policies;
        _authorization = authorization;
        _tokens = tokens;
        _accountStore = accountStore;
        _credentialStore = credentialStore;
        _accountSessions = accountSessions;
        _refreshingTokens = refreshingTokens;
        _securityWorkspace = securityWorkspace;
        _stateRegistry = stateRegistry as ApplicationStateRegistry ??
            throw new InvalidOperationException(
                $"The Dogfood snapshot probe requires {nameof(ApplicationStateRegistry)}; actual={stateRegistry.GetType().FullName}.");
    }

    public async Task InitializeAsync(Action<string> applyStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applyStatus);
        RegisterPermissions();
        RegisterPolicies();

        var observedStates = new List<AuthenticationState> { _authentication.Current.State };
        _authentication.StateChanged += (_, args) => observedStates.Add(args.Current.State);

        var alice = CreatePrincipal("alice", "Viewer", 8, "alpha");
        var bob = CreatePrincipal("bob", "Operator", 32, "beta");
        var admin = CreatePrincipal("admin", "Administrator", 64, "alpha");

        _authentication.SetAnonymous();
        _authentication.SetAuthenticating();
        _authentication.SetAuthenticated(alice, "Bearer", DateTimeOffset.UtcNow.AddMinutes(30));
        _authentication.SetRefreshing(alice);
        _authentication.SetAuthenticated(bob, "Bearer", DateTimeOffset.UtcNow.AddMinutes(30));
        _authentication.SetExpired(bob);
        _authentication.SetSignedOut();
        _authentication.SetFailed("Synthetic identity provider failure");
        _authentication.SetAuthenticating();
        _authentication.SetAuthenticated(admin, "Bearer", DateTimeOffset.UtcNow.AddHours(1));

        foreach (var state in Enum.GetValues<AuthenticationState>())
        {
            if (!observedStates.Contains(state))
            {
                throw new InvalidOperationException($"Authentication state '{state}' was not observed.");
            }
        }

        for (var index = 0; index < 24; index++)
        {
            var result = await _authorization.EvaluatePolicyAsync(
                admin,
                $"dogfood.policy.{index:00}",
                resourceName: $"route-{index:00}",
                cancellationToken: cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Administrator failed policy {index:00}: {result.Status}.");
            }
        }

        var anonymous = await _authorization.EvaluatePolicyAsync(
            SecurityPrincipals.Anonymous,
            "dogfood.policy.00",
            cancellationToken: cancellationToken);
        var forbidden = await _authorization.EvaluatePolicyAsync(
            alice,
            "dogfood.policy.10",
            cancellationToken: cancellationToken);
        if (anonymous.Status != AuthorizationResultStatus.Challenge ||
            forbidden.Status != AuthorizationResultStatus.Forbidden)
        {
            throw new InvalidOperationException("Security negative-path policy results were not deterministic.");
        }

        var accountSessionChanges = 0;
        _accountSessions.SessionChanged += (_, _) => accountSessionChanges++;
        var initialRestore = await _accountSessions.RestoreAsync(
            new AccountSwitchOptions(allowOffline: true),
            cancellationToken);
        if (!initialRestore.Succeeded || initialRestore.Session.Mode != AccountSessionMode.Anonymous)
        {
            throw new InvalidOperationException("A fresh Security workspace did not restore as anonymous.");
        }

        var now = DateTimeOffset.UtcNow;
        var aliceKey = new SecurityAccountKey("oidc", "https://identity.dogfood.test", "alpha", "alice");
        var bobKey = new SecurityAccountKey("oidc", "https://identity.dogfood.test", "beta", "bob");
        var adminKey = new SecurityAccountKey("oidc", "https://identity.dogfood.test", "alpha", "admin");
        await SaveAccountAsync(
            CreateAccount(aliceKey, "Alice", now, 8),
            new AccountCredentialSnapshot(
                aliceKey,
                "dogfood-api",
                "dogfood-alice-token",
                "Bearer",
                "dogfood-alice-refresh",
                now.AddMinutes(30)),
            cancellationToken);
        await SaveAccountAsync(
            CreateAccount(bobKey, "Bob", now, 32, expired: true),
            new AccountCredentialSnapshot(
                bobKey,
                "dogfood-api",
                "dogfood-bob-expired-token",
                "Bearer",
                "dogfood-bob-refresh",
                now.AddMinutes(-1)),
            cancellationToken);
        await SaveAccountAsync(
            CreateAccount(
                adminKey,
                "Administrator",
                now,
                64,
                permissionLifetime: TimeSpan.FromMinutes(4)),
            new AccountCredentialSnapshot(
                adminKey,
                "dogfood-api",
                "dogfood-admin-token",
                "Bearer",
                "dogfood-admin-refresh",
                now.AddHours(4)),
            cancellationToken);
        var aliceDataCredential = await _credentialStore.SaveAsync(
            new AccountCredentialSnapshot(
                aliceKey,
                DogfoodRemoteOperations.ClientId,
                "stress/alice/r8",
                "Bearer",
                refreshToken: "dogfood-alice-data-refresh",
                expiresAt: now.AddMinutes(30)),
            cancellationToken);
        var dataCredential = await _credentialStore.SaveAsync(
            new AccountCredentialSnapshot(
                adminKey,
                DogfoodRemoteOperations.ClientId,
                "stress/admin/r10",
                "Bearer",
                refreshToken: "dogfood-admin-data-refresh",
                expiresAt: now.AddMinutes(5)),
            cancellationToken);
        if (!aliceDataCredential.Succeeded || !dataCredential.Succeeded)
        {
            throw new InvalidOperationException($"Data credential persistence failed: {dataCredential.Status}.");
        }

        var aliceSwitch = await _accountSessions.SwitchAccountAsync(aliceKey, cancellationToken: cancellationToken);
        var bobDenied = await _accountSessions.SwitchAccountAsync(bobKey, cancellationToken: cancellationToken);
        var bobOffline = await _accountSessions.SwitchAccountAsync(
            bobKey,
            new AccountSwitchOptions(allowOffline: true),
            cancellationToken);
        var adminSwitch = await _accountSessions.SwitchAccountAsync(adminKey, cancellationToken: cancellationToken);
        var adminRepeated = await _accountSessions.SwitchAccountAsync(adminKey, cancellationToken: cancellationToken);
        if (!aliceSwitch.Succeeded ||
            bobDenied.Status != AccountSwitchResultStatus.PermissionUnavailable ||
            !bobOffline.Succeeded || bobOffline.Session.Mode != AccountSessionMode.OfflineRestricted ||
            !adminSwitch.Succeeded || adminSwitch.Session.Mode != AccountSessionMode.Online ||
            !adminRepeated.Succeeded || adminRepeated.Session.Revision != adminSwitch.Session.Revision)
        {
            throw new InvalidOperationException("Multi-account switching did not preserve its online/offline transaction contract.");
        }

        var persistedToken = await _tokens.GetTokenAsync(
            new AccessTokenRequest("dogfood-api", "Bearer", "persisted-account"),
            cancellationToken);
        var savedAccounts = await _accountSessions.ListAccountsAsync(cancellationToken);
        if (!persistedToken.Succeeded || !savedAccounts.Succeeded || savedAccounts.Value!.Count != 3)
        {
            throw new InvalidOperationException("Persisted account enumeration or token resolution failed.");
        }

        var refreshProbeResource = "dogfood-refresh-probe";
        var refreshProbeExpiry = now.AddMinutes(1);
        var refreshProbeSaved = await _credentialStore.SaveAsync(
            new AccountCredentialSnapshot(
                adminKey,
                refreshProbeResource,
                "stress/admin/r10",
                "Bearer",
                refreshToken: "dogfood-admin-refresh-probe",
                expiresAt: refreshProbeExpiry),
            cancellationToken);
        var refreshCountBefore = _refreshingTokens.RefreshCount;
        var permissionRefreshCountBefore = _refreshingTokens.PermissionRefreshCount;
        var refreshRequests = Enumerable.Range(0, 16)
            .Select(index => _tokens.GetTokenAsync(
                new AccessTokenRequest(
                    refreshProbeResource,
                    "Bearer",
                    $"proactive-refresh-probe-{index:00}"),
                cancellationToken).AsTask())
            .ToArray();
        var refreshedTokens = await Task.WhenAll(refreshRequests);
        var refreshedSnapshot = await _credentialStore.GetAsync(
            adminKey,
            refreshProbeResource,
            cancellationToken);
        if (!refreshProbeSaved.Succeeded || refreshedTokens.Any(static token => !token.Succeeded) ||
            !refreshedSnapshot.Succeeded || refreshedSnapshot.Value!.ExpiresAt <= refreshProbeExpiry ||
            _refreshingTokens.RefreshCount != refreshCountBefore + 1 ||
            _refreshingTokens.PermissionRefreshCount != permissionRefreshCountBefore + 1 ||
            _accountSessions.Current.Permissions!.Revision != 2)
        {
            throw new InvalidOperationException(
                "The application token provider did not atomically merge token and permission refresh requests.");
        }

        var temporaryKey = new SecurityAccountKey(
            "oidc",
            "https://identity.dogfood.test",
            "temporary",
            "temporary-user");
        await SaveAccountAsync(
            CreateAccount(temporaryKey, "Temporary User", now, 1, expired: true),
            new AccountCredentialSnapshot(
                temporaryKey,
                "dogfood-api",
                "temporary-token",
                "Bearer",
                expiresAt: now.AddMinutes(10)),
            cancellationToken);
        var temporarySecondary = await _credentialStore.SaveAsync(
            new AccountCredentialSnapshot(
                temporaryKey,
                "dogfood-files",
                "temporary-files-token",
                "Bearer",
                refreshToken: "temporary-files-refresh",
                expiresAt: now.AddMinutes(-1)),
            cancellationToken);
        await _refreshingTokens.PrepareAccountForOnlineSwitchAsync(
            temporaryKey,
            "dogfood-files",
            cancellationToken);
        var preparedTemporaryAccount = await _accountStore.GetAsync(temporaryKey, cancellationToken);
        var preparedTemporaryCredential = await _credentialStore.GetAsync(
            temporaryKey,
            "dogfood-files",
            cancellationToken);
        if (!preparedTemporaryAccount.Succeeded || preparedTemporaryAccount.Value!.Permissions.IsExpired(DateTimeOffset.UtcNow) ||
            !preparedTemporaryCredential.Succeeded || preparedTemporaryCredential.Value!.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Inactive account preparation did not renew its permission and credential snapshots.");
        }

        var secondaryRemoved = await _credentialStore.RemoveAsync(
            temporaryKey,
            "dogfood-files",
            cancellationToken);
        var temporaryRemoved = await _accountSessions.RemoveAccountAsync(temporaryKey, cancellationToken);
        if (!temporarySecondary.Succeeded || !secondaryRemoved.Succeeded || !secondaryRemoved.Value ||
            !temporaryRemoved.Succeeded)
        {
            throw new InvalidOperationException("Temporary account removal did not remove all isolated data.");
        }

        var recreatedManager = new AccountSessionManager(
            new FileAccountSessionStore(_securityWorkspace.RootPath),
            new FileCredentialStore(_securityWorkspace.RootPath),
            new AuthenticationStateStore(),
            new SecurityPersistenceOptions(_securityWorkspace.RootPath, "dogfood-api"),
            TimeProvider.System);
        var recreatedRestore = await recreatedManager.RestoreAsync(cancellationToken: cancellationToken);
        var recreatedToken = await recreatedManager.GetTokenAsync(
            new AccessTokenRequest("dogfood-api"),
            cancellationToken);
        var activePointer = await _accountStore.GetLastActiveAccountAsync(cancellationToken);
        var finalAccounts = await _accountStore.ListAsync(cancellationToken);
        if (!recreatedRestore.Succeeded || !Equals(recreatedRestore.Session.AccountKey, adminKey) ||
            !recreatedToken.Succeeded || !activePointer.Succeeded || !Equals(activePointer.Value, adminKey) ||
            !finalAccounts.Succeeded || finalAccounts.Value!.Count != 3 ||
            accountSessionChanges != 5)
        {
            throw new InvalidOperationException("Process-like Security restore or account session notification ledger failed.");
        }

        var persistedStateText = string.Join(
            '|',
            _stateRegistry.CreateSnapshot().Entries.Select(static entry => entry.Value?.ToString()));
        foreach (var secret in new[]
                 {
                     "dogfood-alice-token",
                     "dogfood-alice-refresh",
                     "dogfood-bob-expired-token",
                     "dogfood-bob-refresh",
                     "dogfood-admin-token",
                     "dogfood-admin-refresh",
                     "stress/alice/r8",
                     "stress/admin/r10",
                 })
        {
            if (persistedStateText.Contains(secret, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A Security credential leaked into persisted City State.");
            }
        }

        var staleAliceAccount = await _accountStore.SaveAsync(
            CreateAccount(aliceKey, "Alice", now, 8, expired: true),
            cancellationToken);
        var staleAliceCredential = await _credentialStore.SaveAsync(
            new AccountCredentialSnapshot(
                aliceKey,
                DogfoodRemoteOperations.ClientId,
                "stress/alice/r8",
                "Bearer",
                refreshToken: "dogfood-alice-data-refresh",
                expiresAt: now.AddMinutes(-1)),
            cancellationToken);
        if (!staleAliceAccount.Succeeded || !staleAliceCredential.Succeeded)
        {
            throw new InvalidOperationException("The inactive Alice expiry regression could not be prepared.");
        }

        var tokenStatuses = new HashSet<AccessTokenResultStatus>();
        var syntheticTokenProvider = new DelegateAccessTokenProvider(ResolveTokenAsync);
        foreach (var operation in new[] { "success", "none", "required", "expired", "failed", "unavailable", "cancelled" })
        {
            var result = await syntheticTokenProvider.GetTokenAsync(
                new AccessTokenRequest("dogfood-api", "Bearer", operation),
                cancellationToken);
            tokenStatuses.Add(result.Status);
        }

        if (tokenStatuses.Count != Enum.GetValues<AccessTokenResultStatus>().Length)
        {
            throw new InvalidOperationException("Access token provider did not cover every stable result status.");
        }

        if (_permissions.Permissions.Count != 64 || _policies.Policies.Count != 24)
        {
            throw new InvalidOperationException("Security catalogs do not match their 64/24 ledgers.");
        }

        applyStatus($"Account admin active; auth revision {_authentication.Current.Revision}; 64 permissions and 24 policies verified.");
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_SECURITY permissions=64 policies=24 accounts=3 accountSwitches=4 restored=1 states={observedStates.Distinct().Count()} tokenStatuses={tokenStatuses.Count} revision={_authentication.Current.Revision}");
    }

    public static ValueTask<AccessTokenResult> ResolveTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(request.OperationName switch
        {
            "success" => AccessTokenResult.Success("synthetic-token-not-persisted", "Bearer", DateTimeOffset.UtcNow.AddMinutes(10)),
            "none" => AccessTokenResult.None(),
            "required" => AccessTokenResult.Required(),
            "expired" => AccessTokenResult.Expired(),
            "failed" => AccessTokenResult.Failed("Synthetic failure"),
            "unavailable" => AccessTokenResult.Unavailable(),
            "cancelled" => AccessTokenResult.Cancelled(),
            _ => AccessTokenResult.Success("stress/admin/r10", "Bearer"),
        });
    }

    private void RegisterPermissions()
    {
        for (var index = 0; index < 64; index++)
        {
            if (!_permissions.Add(new PermissionDescriptor(
                    $"dogfood.permission.{index:00}",
                    displayNameKey: $"SecurityData.{index % 60:000}",
                    descriptionKey: $"SecurityData.{(index + 1) % 60:000}",
                    category: $"group-{index / 8}",
                    defaultPolicy: $"dogfood.policy.{index % 24:00}",
                    isHostOnly: index % 16 == 0)))
            {
                throw new InvalidOperationException($"Permission {index:00} could not be registered.");
            }
        }

        const string contribution = "dogfood.synthetic.permissions";
        for (var index = 0; index < 4; index++)
        {
            _permissions.Add(new PermissionDescriptor($"dogfood.temporary.{index}", contributionId: contribution));
        }

        if (_permissions.RemoveByContribution(contribution) != 4)
        {
            throw new InvalidOperationException("Synthetic permission contribution was not revoked atomically.");
        }
    }

    private void RegisterPolicies()
    {
        for (var index = 0; index < 24; index++)
        {
            AuthorizationRequirement[] requirements = index switch
            {
                < 6 => [AuthorizationRequirement.RequireAuthenticated()],
                < 12 =>
                [
                    AuthorizationRequirement.RequireAuthenticated(),
                    AuthorizationRequirement.RequirePermission($"dogfood.permission.{index:00}"),
                ],
                < 18 =>
                [
                    AuthorizationRequirement.RequireAuthenticated(),
                    AuthorizationRequirement.RequireRole("Administrator"),
                ],
                _ =>
                [
                    AuthorizationRequirement.RequireAuthenticated(),
                    AuthorizationRequirement.RequirePermission($"dogfood.permission.{index:00}"),
                    AuthorizationRequirement.RequireClaim("tenant", "alpha"),
                    AuthorizationRequirement.RequireRole("Administrator"),
                ],
            };

            if (!_policies.Add(new AuthorizationPolicy($"dogfood.policy.{index:00}", requirements)))
            {
                throw new InvalidOperationException($"Authorization policy {index:00} could not be registered.");
            }
        }

        const string contribution = "dogfood.synthetic.policies";
        _policies.Add(AuthorizationPolicy.RequireAuthenticated("dogfood.temporary.policy"));
        _policies.Remove("dogfood.temporary.policy");
        _policies.Add(new AuthorizationPolicy(
            "dogfood.temporary.contributed",
            [AuthorizationRequirement.RequireAuthenticated()],
            contribution));
        if (_policies.RemoveByContribution(contribution) != 1)
        {
            throw new InvalidOperationException("Synthetic policy contribution was not revoked atomically.");
        }
    }

    private static ClaimsPrincipal CreatePrincipal(
        string subject,
        string role,
        int permissionCount,
        string tenant)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject),
            new(ClaimTypes.Role, role),
            new("tenant", tenant),
        };
        claims.AddRange(Enumerable.Range(0, permissionCount)
            .Select(index => new Claim(SecurityClaimTypes.Permission, $"dogfood.permission.{index:00}")));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Dogfood", ClaimTypes.Name, ClaimTypes.Role));
    }

    private async Task SaveAccountAsync(
        AccountRecordSnapshot account,
        AccountCredentialSnapshot credential,
        CancellationToken cancellationToken)
    {
        var accountResult = await _accountStore.SaveAsync(account, cancellationToken);
        var credentialResult = await _credentialStore.SaveAsync(credential, cancellationToken);
        if (!accountResult.Succeeded || !credentialResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Account persistence failed: account={accountResult.Status}, credential={credentialResult.Status}.");
        }
    }

    private static AccountRecordSnapshot CreateAccount(
        SecurityAccountKey key,
        string displayName,
        DateTimeOffset now,
        int permissionCount,
        bool expired = false,
        TimeSpan? permissionLifetime = null)
    {
        return new AccountRecordSnapshot(
            new AccountProfileSnapshot(key, displayName, lastUsedAt: now),
            new PersistedPermissionSnapshot(
                key,
                Enumerable.Range(0, permissionCount)
                    .Select(index => $"dogfood.permission.{index:00}")
                    .ToArray(),
                revision: 1,
                issuedAt: now.AddHours(-2),
                expiresAt: expired
                    ? now.AddMinutes(-1)
                    : now + (permissionLifetime ?? TimeSpan.FromHours(2))));
    }
}
