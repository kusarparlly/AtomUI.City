using System.Security.Claims;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AccountSessionManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SwitchPublishesCoherentProfilePermissionsPrincipalAndToken()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("alice");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Alice", ["orders.read", "orders.write"]),
            workspace.CreateCredential(key, Now, "alice-token"));

        var result = await setup.Manager.SwitchAccountAsync(key);
        var token = await setup.Manager.GetTokenAsync(new AccessTokenRequest("desktop-api", "Bearer"));

        Assert.True(result.Succeeded);
        Assert.Equal(AccountSessionMode.Online, result.Session.Mode);
        Assert.Equal(key, result.Session.AccountKey);
        Assert.Equal("Alice", result.Session.Profile!.DisplayName);
        Assert.Equal(["orders.read", "orders.write"], result.Session.Permissions!.Permissions);
        Assert.Equal("alice", result.Session.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("tenant-a", result.Session.Principal.FindFirst(SecurityClaimTypes.TenantId)?.Value);
        Assert.Equal(AuthenticationState.Authenticated, setup.Authentication.Current.State);
        Assert.Equal(result.Session.Revision, setup.Authentication.Current.Revision);
        Assert.Equal(AccessTokenResultStatus.Success, token.Status);
        Assert.Equal("alice-token", token.Token);
    }

    [Fact]
    public async Task RefreshReloadsTheCurrentAccountAndPublishesOneNewRevision()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("refresh-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Before Refresh", ["orders.read"]),
            workspace.CreateCredential(key, Now, "refresh-token"));
        var switched = await setup.Manager.SwitchAccountAsync(key);
        var notifications = 0;
        setup.Manager.SessionChanged += (_, _) => notifications++;
        var refreshedAccount = new AccountRecordSnapshot(
            new AccountProfileSnapshot(key, "After Refresh", lastUsedAt: Now),
            new PersistedPermissionSnapshot(
                key,
                ["orders.read", "orders.write"],
                revision: 8,
                issuedAt: Now,
                expiresAt: Now.AddHours(4)));
        Assert.True((await setup.AccountStore.SaveAsync(refreshedAccount)).Succeeded);

        var repeatedSwitch = await setup.Manager.SwitchAccountAsync(key);
        var refreshed = await setup.Manager.RefreshAccountAsync(key);

        Assert.Equal(switched.Session.Revision, repeatedSwitch.Session.Revision);
        Assert.True(refreshed.Succeeded);
        Assert.True(refreshed.Session.Revision > switched.Session.Revision);
        Assert.Equal("After Refresh", refreshed.Session.Profile!.DisplayName);
        Assert.Equal(["orders.read", "orders.write"], refreshed.Session.Permissions!.Permissions);
        Assert.Contains(
            refreshed.Session.Principal.Claims,
            claim => claim.Type == SecurityClaimTypes.Permission && claim.Value == "orders.write");
        Assert.Equal(refreshed.Session.Revision, setup.Authentication.Current.Revision);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task RefreshForANonCurrentAccountFailsWithoutChangingTheSession()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var active = workspace.CreateKey("active-refresh-user");
        var inactive = workspace.CreateKey("inactive-refresh-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(active, Now, "Active Refresh User"),
            workspace.CreateCredential(active, Now, "active-refresh-token"));
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(inactive, Now, "Inactive Refresh User"),
            workspace.CreateCredential(inactive, Now, "inactive-refresh-token"));
        await setup.Manager.SwitchAccountAsync(active);
        var before = setup.Manager.Current;

        var result = await setup.Manager.RefreshAccountAsync(inactive);

        Assert.Equal(AccountSwitchResultStatus.Failed, result.Status);
        Assert.Equal("account", result.FailureStage);
        Assert.Equal(active, setup.Manager.Current.AccountKey);
        Assert.Equal(before.Revision, setup.Manager.Current.Revision);
    }

    [Fact]
    public async Task PreCancelledRefreshPreservesTheCurrentSession()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("cancelled-refresh-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Cancelled Refresh User"),
            workspace.CreateCredential(key, Now, "cancelled-refresh-token"));
        await setup.Manager.SwitchAccountAsync(key);
        var before = setup.Manager.Current;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await setup.Manager.RefreshAccountAsync(key, cancellationToken: cancellation.Token);

        Assert.Equal(AccountSwitchResultStatus.Cancelled, result.Status);
        Assert.Equal(before.Revision, setup.Manager.Current.Revision);
        Assert.Equal(key, setup.Manager.Current.AccountKey);
    }

    [Fact]
    public async Task RestoreRehydratesLastActiveAccountAfterProcessLikeRecreation()
    {
        using var workspace = new SecurityTestWorkspace();
        var first = CreateManager(workspace);
        var key = workspace.CreateKey("restart-user");
        await SaveAccountAsync(
            first,
            workspace.CreateAccount(key, Now, "Restart User", ["offline.read"]),
            workspace.CreateCredential(key, Now, "restart-token"));
        await first.Manager.SwitchAccountAsync(key);

        var recreated = CreateManager(workspace);
        var restored = await recreated.Manager.RestoreAsync();
        var token = await recreated.Manager.GetTokenAsync(new AccessTokenRequest("desktop-api"));

        Assert.True(restored.Succeeded);
        Assert.Equal(key, restored.Session.AccountKey);
        Assert.Equal("Restart User", restored.Session.Profile!.DisplayName);
        Assert.Equal(AuthenticationState.Authenticated, recreated.Authentication.Current.State);
        Assert.Equal("restart-token", token.Token);
    }

    [Fact]
    public async Task FailedTargetSwitchKeepsPreviousAccountAndPersistentPointer()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var accountA = workspace.CreateKey("account-a");
        var accountB = workspace.CreateKey("account-b");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(accountA, Now, "Account A"),
            workspace.CreateCredential(accountA, Now, "token-a"));
        await setup.AccountStore.SaveAsync(workspace.CreateAccount(accountB, Now, "Account B"));
        await setup.Manager.SwitchAccountAsync(accountA);
        var revision = setup.Manager.Current.Revision;

        var failed = await setup.Manager.SwitchAccountAsync(accountB);
        var active = await setup.AccountStore.GetLastActiveAccountAsync();

        Assert.Equal(AccountSwitchResultStatus.CredentialUnavailable, failed.Status);
        Assert.Equal(accountA, setup.Manager.Current.AccountKey);
        Assert.Equal(revision, setup.Manager.Current.Revision);
        Assert.Equal(accountA, active.Value);
        Assert.Equal("account-a", setup.Authentication.Current.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task CorruptedTargetCredentialDoesNotPartiallyPublishTargetAccount()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var accountA = workspace.CreateKey("stable-account");
        var accountB = workspace.CreateKey("corrupt-account");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(accountA, Now, "Stable Account"),
            workspace.CreateCredential(accountA, Now, "stable-token"));
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(accountB, Now, "Corrupt Account"),
            workspace.CreateCredential(accountB, Now, "corrupt-token"));
        await setup.Manager.SwitchAccountAsync(accountA);
        var credentialPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(workspace.RootPath, "credentials", accountB.ToStorageKey()),
            "*.json"));
        await File.WriteAllTextAsync(credentialPath, "{ invalid-json");

        var failed = await setup.Manager.SwitchAccountAsync(accountB);

        Assert.Equal(AccountSwitchResultStatus.InvalidData, failed.Status);
        Assert.Equal("credential", failed.FailureStage);
        Assert.Equal(accountA, failed.Session.AccountKey);
        Assert.Equal(accountA, setup.Manager.Current.AccountKey);
        Assert.Equal("stable-account", setup.Authentication.Current.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task OfflineSwitchAcceptsExpiredCredentialAndPermissionsAsRestrictedSession()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("offline-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Offline User", ["cached.read"], expired: true),
            workspace.CreateCredential(key, Now, "expired-token", expired: true));
        Assert.True((await setup.CredentialStore.SaveAsync(
            workspace.CreateCredential(key, Now, "secondary-valid-token", "secondary-api"))).Succeeded);

        var denied = await setup.Manager.SwitchAccountAsync(key);
        var accepted = await setup.Manager.SwitchAccountAsync(key, new AccountSwitchOptions(allowOffline: true));
        var token = await setup.Manager.GetTokenAsync(new AccessTokenRequest("desktop-api"));
        var secondaryToken = await setup.Manager.GetTokenAsync(new AccessTokenRequest("secondary-api"));

        Assert.Equal(AccountSwitchResultStatus.PermissionUnavailable, denied.Status);
        Assert.True(accepted.Succeeded);
        Assert.Equal(AccountSessionMode.OfflineRestricted, accepted.Session.Mode);
        Assert.Equal(AuthenticationState.Expired, setup.Authentication.Current.State);
        Assert.DoesNotContain(
            accepted.Session.Principal.Claims,
            claim => claim.Type == SecurityClaimTypes.Permission);
        Assert.Equal(AccessTokenResultStatus.Expired, token.Status);
        Assert.Equal(AccessTokenResultStatus.Expired, secondaryToken.Status);
        Assert.Null(secondaryToken.Token);
    }

    [Fact]
    public async Task ConcurrentSwitchesNeverPublishMixedAccountSnapshots()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var keys = Enumerable.Range(0, 6)
            .Select(index => workspace.CreateKey($"user-{index}", $"tenant-{index}"))
            .ToArray();
        foreach (var (key, index) in keys.Select((key, index) => (key, index)))
        {
            await SaveAccountAsync(
                setup,
                workspace.CreateAccount(key, Now, $"User {index}", [$"permission-{index}"]),
                workspace.CreateCredential(key, Now, $"token-{index}"));
        }

        var observed = new List<AccountSessionSnapshot>();
        var observedSync = new object();
        setup.Manager.SessionChanged += (_, args) =>
        {
            lock (observedSync)
            {
                observed.Add(args.Current);
            }
        };
        var operations = Enumerable.Range(0, 60)
            .Select(index => setup.Manager.SwitchAccountAsync(keys[index % keys.Length]).AsTask())
            .ToArray();

        var results = await Task.WhenAll(operations);

        Assert.All(results, static result => Assert.True(result.Succeeded));
        Assert.NotEmpty(observed);
        Assert.All(observed, snapshot =>
        {
            var subject = snapshot.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var index = int.Parse(subject!["user-".Length..], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal($"User {index}", snapshot.Profile!.DisplayName);
            Assert.Equal($"tenant-{index}", snapshot.AccountKey!.TenantId);
            Assert.Equal($"permission-{index}", Assert.Single(snapshot.Permissions!.Permissions));
        });
    }

    [Fact]
    public async Task AuthenticationAndSessionObserversSeeTheSameCommittedAccount()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("observer-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Observer User"),
            workspace.CreateCredential(key, Now, "observer-token"));
        SecurityAccountKey? accountSeenByAuthenticationObserver = null;
        long? revisionSeenBySessionObserver = null;
        setup.Authentication.StateChanged += (_, args) =>
        {
            accountSeenByAuthenticationObserver = setup.Manager.Current.AccountKey;
            Assert.Equal(args.Current.Revision, setup.Manager.Current.Revision);
        };
        setup.Manager.SessionChanged += (_, args) =>
        {
            revisionSeenBySessionObserver = setup.Authentication.Current.Revision;
            Assert.Equal(args.Current.AccountKey, setup.Manager.Current.AccountKey);
        };

        var result = await setup.Manager.SwitchAccountAsync(key);

        Assert.True(result.Succeeded);
        Assert.Equal(key, accountSeenByAuthenticationObserver);
        Assert.Equal(result.Session.Revision, revisionSeenBySessionObserver);
    }

    [Fact]
    public async Task RemovingActiveAccountSignsOutWithoutSelectingAnotherAccount()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var active = workspace.CreateKey("active-user");
        var inactive = workspace.CreateKey("inactive-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(active, Now, "Active User"),
            workspace.CreateCredential(active, Now, "active-token"));
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(inactive, Now, "Inactive User"),
            workspace.CreateCredential(inactive, Now, "inactive-token"));
        await setup.Manager.SwitchAccountAsync(active);

        var removed = await setup.Manager.RemoveAccountAsync(active);
        var accounts = await setup.AccountStore.ListAsync();
        var pointer = await setup.AccountStore.GetLastActiveAccountAsync();

        Assert.True(removed.Succeeded);
        Assert.Equal(AccountSessionMode.Anonymous, setup.Manager.Current.Mode);
        Assert.Equal(AuthenticationState.SignedOut, setup.Authentication.Current.State);
        Assert.Null(pointer.Value);
        Assert.Equal(inactive, Assert.Single(accounts.Value!).AccountKey);
    }

    [Fact]
    public async Task PreCancelledSwitchDoesNotChangeCurrentAccount()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("cancelled-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Cancelled User"),
            workspace.CreateCredential(key, Now, "cancelled-token"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await setup.Manager.SwitchAccountAsync(key, cancellationToken: cancellation.Token);

        Assert.Equal(AccountSwitchResultStatus.Cancelled, result.Status);
        Assert.Equal(AccountSessionMode.Anonymous, setup.Manager.Current.Mode);
        Assert.Equal(AuthenticationState.Unknown, setup.Authentication.Current.State);
    }

    [Fact]
    public async Task TokenResolutionStartedForOldAccountCompletesBeforeSwitchCommits()
    {
        using var workspace = new SecurityTestWorkspace();
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var fileCredentialStore = new FileCredentialStore(workspace.RootPath);
        var first = workspace.CreateKey("first-user");
        var second = workspace.CreateKey("second-user");
        await accountStore.SaveAsync(workspace.CreateAccount(first, Now, "First User"));
        await accountStore.SaveAsync(workspace.CreateAccount(second, Now, "Second User"));
        await fileCredentialStore.SaveAsync(workspace.CreateCredential(first, Now, "first-default"));
        await fileCredentialStore.SaveAsync(workspace.CreateCredential(first, Now, "first-slow", "slow-api"));
        await fileCredentialStore.SaveAsync(workspace.CreateCredential(second, Now, "second-default"));
        var credentialStore = new BlockingCredentialStore(fileCredentialStore, first, "slow-api");
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            new AuthenticationStateStore(),
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now));
        await manager.SwitchAccountAsync(first);

        var oldTokenTask = manager.GetTokenAsync(new AccessTokenRequest("slow-api")).AsTask();
        await credentialStore.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var switchTask = manager.SwitchAccountAsync(second).AsTask();
        await Task.Delay(50);
        Assert.False(switchTask.IsCompleted);
        credentialStore.ReleaseRead.TrySetResult();
        var oldToken = await oldTokenTask;
        var switched = await switchTask;

        Assert.True(switched.Succeeded);
        Assert.Equal(second, manager.Current.AccountKey);
        Assert.Equal(AccessTokenResultStatus.Success, oldToken.Status);
        Assert.Equal("first-slow", oldToken.Token);
    }

    [Fact]
    public async Task ReturnedPrincipalCannotMutatePublishedSession()
    {
        using var workspace = new SecurityTestWorkspace();
        var setup = CreateManager(workspace);
        var key = workspace.CreateKey("immutable-user");
        await SaveAccountAsync(
            setup,
            workspace.CreateAccount(key, Now, "Immutable User"),
            workspace.CreateCredential(key, Now, "immutable-token"));
        await setup.Manager.SwitchAccountAsync(key);
        var returned = setup.Manager.Current.Principal;
        returned.AddIdentity(new ClaimsIdentity([new Claim("injected", "true")]));

        Assert.Null(setup.Manager.Current.Principal.FindFirst("injected"));
    }

    [Fact]
    public async Task TokenProviderMapsCredentialStoreExceptionToFailedResult()
    {
        using var workspace = new SecurityTestWorkspace();
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var fileCredentialStore = new FileCredentialStore(workspace.RootPath);
        var key = workspace.CreateKey("throwing-store-user");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Throwing Store User"));
        await fileCredentialStore.SaveAsync(workspace.CreateCredential(key, Now, "throwing-store-token"));
        var credentialStore = new InterceptingCredentialStore(fileCredentialStore);
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            new AuthenticationStateStore(),
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now));
        await manager.SwitchAccountAsync(key);
        var exception = new InvalidOperationException("credential backend failed");
        credentialStore.ExceptionOnGet = exception;

        var result = await manager.GetTokenAsync(new AccessTokenRequest("desktop-api"));

        Assert.Equal(AccessTokenResultStatus.Failed, result.Status);
        Assert.Same(exception, result.Exception);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task TokenProviderObservesCancellationAfterCustomStoreReturns()
    {
        using var workspace = new SecurityTestWorkspace();
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var fileCredentialStore = new FileCredentialStore(workspace.RootPath);
        var key = workspace.CreateKey("cancel-after-store-user");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Cancel After Store User"));
        await fileCredentialStore.SaveAsync(workspace.CreateCredential(key, Now, "cancel-after-store-token"));
        var credentialStore = new InterceptingCredentialStore(fileCredentialStore);
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            new AuthenticationStateStore(),
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now));
        await manager.SwitchAccountAsync(key);
        using var cancellation = new CancellationTokenSource();
        credentialStore.AfterGet = cancellation.Cancel;

        var result = await manager.GetTokenAsync(
            new AccessTokenRequest("desktop-api"),
            cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
        Assert.Null(result.Token);
    }

    private static ManagerSetup CreateManager(SecurityTestWorkspace workspace)
    {
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var credentialStore = new FileCredentialStore(workspace.RootPath);
        var authentication = new AuthenticationStateStore();
        var options = new SecurityPersistenceOptions(workspace.RootPath, "desktop-api");
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            authentication,
            options,
            new FixedTimeProvider(Now));
        return new ManagerSetup(accountStore, credentialStore, authentication, manager);
    }

    private static async Task SaveAccountAsync(
        ManagerSetup setup,
        AccountRecordSnapshot account,
        AccountCredentialSnapshot credential)
    {
        Assert.True((await setup.AccountStore.SaveAsync(account)).Succeeded);
        Assert.True((await setup.CredentialStore.SaveAsync(credential)).Succeeded);
    }

    private sealed record ManagerSetup(
        FileAccountSessionStore AccountStore,
        FileCredentialStore CredentialStore,
        AuthenticationStateStore Authentication,
        AccountSessionManager Manager);

    private sealed class BlockingCredentialStore(
        ICredentialStore inner,
        SecurityAccountKey blockedAccount,
        string blockedResource) : ICredentialStore
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<SecurityStoreResult<AccountCredentialSnapshot>> GetAsync(
            SecurityAccountKey accountKey,
            string resourceName,
            CancellationToken cancellationToken = default)
        {
            if (accountKey.Equals(blockedAccount) &&
                string.Equals(resourceName, blockedResource, StringComparison.Ordinal))
            {
                ReadStarted.TrySetResult();
                await ReleaseRead.Task.WaitAsync(cancellationToken);
            }

            return await inner.GetAsync(accountKey, resourceName, cancellationToken);
        }

        public ValueTask<SecurityStoreResult<bool>> SaveAsync(
            AccountCredentialSnapshot credential,
            CancellationToken cancellationToken = default) =>
            inner.SaveAsync(credential, cancellationToken);

        public ValueTask<SecurityStoreResult<bool>> RemoveAsync(
            SecurityAccountKey accountKey,
            string resourceName,
            CancellationToken cancellationToken = default) =>
            inner.RemoveAsync(accountKey, resourceName, cancellationToken);

        public ValueTask<SecurityStoreResult<bool>> RemoveAllAsync(
            SecurityAccountKey accountKey,
            CancellationToken cancellationToken = default) =>
            inner.RemoveAllAsync(accountKey, cancellationToken);
    }

    private sealed class InterceptingCredentialStore(ICredentialStore inner) : ICredentialStore
    {
        public Exception? ExceptionOnGet { get; set; }

        public Action? AfterGet { get; set; }

        public async ValueTask<SecurityStoreResult<AccountCredentialSnapshot>> GetAsync(
            SecurityAccountKey accountKey,
            string resourceName,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionOnGet is not null)
            {
                throw ExceptionOnGet;
            }

            var result = await inner.GetAsync(accountKey, resourceName, cancellationToken);
            AfterGet?.Invoke();
            return result;
        }

        public ValueTask<SecurityStoreResult<bool>> SaveAsync(
            AccountCredentialSnapshot credential,
            CancellationToken cancellationToken = default) =>
            inner.SaveAsync(credential, cancellationToken);

        public ValueTask<SecurityStoreResult<bool>> RemoveAsync(
            SecurityAccountKey accountKey,
            string resourceName,
            CancellationToken cancellationToken = default) =>
            inner.RemoveAsync(accountKey, resourceName, cancellationToken);

        public ValueTask<SecurityStoreResult<bool>> RemoveAllAsync(
            SecurityAccountKey accountKey,
            CancellationToken cancellationToken = default) =>
            inner.RemoveAllAsync(accountKey, cancellationToken);
    }
}
