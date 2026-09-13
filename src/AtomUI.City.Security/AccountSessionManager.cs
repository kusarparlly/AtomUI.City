using System.Globalization;
using System.Security.Claims;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class AccountSessionManager : IAccountSessionManager
{
    private const string StageGate = "gate";
    private const string StageActiveAccount = "active-account";
    private const string StageAccount = "account";
    private const string StagePermissions = "permissions";
    private const string StageCredential = "credential";
    private const string StageCommit = "commit";
    private const string StageRemoveCredentials = "remove-credentials";
    private const string StageRemoveAccount = "remove-account";

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly IAccountSessionStore _accountStore;
    private readonly ICredentialStore _credentialStore;
    private readonly AuthenticationStateStore _authenticationState;
    private readonly SecurityPersistenceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly OrderedEventPublisher<AccountSessionChangedEventArgs> _eventPublisher;
    private AccountSessionSnapshot _current = AccountSessionSnapshot.Anonymous();

    public AccountSessionManager(
        IAccountSessionStore accountStore,
        ICredentialStore credentialStore,
        AuthenticationStateStore authenticationState,
        SecurityPersistenceOptions options,
        TimeProvider timeProvider,
        IHostDiagnostics? diagnostics = null)
    {
        _accountStore = accountStore ?? throw new ArgumentNullException(nameof(accountStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _authenticationState = authenticationState ?? throw new ArgumentNullException(nameof(authenticationState));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _diagnostics = diagnostics;
        _eventPublisher = new OrderedEventPublisher<AccountSessionChangedEventArgs>(
            diagnostics,
            SecurityDiagnosticIds.AccountSessionObserverFailed);
    }

    public event EventHandler<AccountSessionChangedEventArgs>? SessionChanged;

    public AccountSessionSnapshot Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAccountsAsync(
        CancellationToken cancellationToken = default) =>
        _accountStore.ListAsync(cancellationToken);

    public ValueTask<AccountSwitchResult> RestoreAsync(
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            operation: "restore",
            targetAccountKey: null,
            (operationId, token) => RestoreCoreAsync(operationId, options ?? new AccountSwitchOptions(), token),
            cancellationToken);

    public ValueTask<AccountSwitchResult> SwitchAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        return ExecuteAsync(
            operation: "switch",
            targetAccountKey: accountKey,
            (operationId, token) => SwitchCoreAsync(
                accountKey,
                options ?? new AccountSwitchOptions(),
                operationId,
                persistActiveAccount: true,
                forceReload: false,
                token),
            cancellationToken);
    }

    public ValueTask<AccountSwitchResult> RefreshAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        return ExecuteAsync(
            operation: "refresh",
            targetAccountKey: accountKey,
            (operationId, token) => RefreshCoreAsync(
                accountKey,
                options ?? new AccountSwitchOptions(),
                operationId,
                token),
            cancellationToken);
    }

    public ValueTask<AccountSwitchResult> RemoveAccountAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        return ExecuteAsync(
            operation: "remove",
            targetAccountKey: accountKey,
            (operationId, token) => RemoveCoreAsync(accountKey, operationId, token),
            cancellationToken);
    }

    public async ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            WriteTokenDiagnostic(request, AccessTokenResultStatus.Cancelled, accountKey: null);
            return AccessTokenResult.Cancelled("Credential resolution was cancelled.");
        }

        try
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteTokenDiagnostic(request, AccessTokenResultStatus.Cancelled, accountKey: null);
            return AccessTokenResult.Cancelled("Credential resolution was cancelled.");
        }

        try
        {
            var captured = Current;
            if (captured.AccountKey is null)
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Unavailable, accountKey: null);
                return AccessTokenResult.Unavailable("No account session is active.");
            }

            if (captured.Mode == AccountSessionMode.OfflineRestricted)
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Expired, captured.AccountKey);
                return AccessTokenResult.Expired(
                    "The active account session is offline-restricted and cannot issue credentials for server operations.");
            }

            SecurityStoreResult<AccountCredentialSnapshot> result;
            try
            {
                result = await _credentialStore.GetAsync(
                    captured.AccountKey,
                    request.ResourceName,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Cancelled, captured.AccountKey);
                return AccessTokenResult.Cancelled("Credential resolution was cancelled.");
            }
            catch (Exception exception)
            {
                WriteTokenDiagnostic(
                    request,
                    AccessTokenResultStatus.Failed,
                    captured.AccountKey,
                    exception: exception,
                    providerFailure: true);
                return AccessTokenResult.Failed("The credential store failed.", exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Cancelled, captured.AccountKey);
                return AccessTokenResult.Cancelled("Credential resolution was cancelled.");
            }

            var current = Current;
            if (current.Revision != captured.Revision || !Equals(current.AccountKey, captured.AccountKey))
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Unavailable, captured.AccountKey);
                return AccessTokenResult.Unavailable("The account changed while the credential was being resolved.");
            }

            if (!result.Succeeded)
            {
                var accessTokenResult = result.Status switch
                {
                    SecurityStoreResultStatus.NotFound => AccessTokenResult.Required("No credential exists for the requested resource."),
                    SecurityStoreResultStatus.Cancelled => AccessTokenResult.Cancelled("Credential resolution was cancelled."),
                    SecurityStoreResultStatus.InvalidData or SecurityStoreResultStatus.UnsupportedSchema =>
                        AccessTokenResult.Failed("The stored credential is invalid.", result.Exception),
                    _ => AccessTokenResult.Unavailable("The credential store is unavailable."),
                };
                WriteTokenDiagnostic(
                    request,
                    accessTokenResult.Status,
                    captured.AccountKey,
                    exception: result.Exception,
                    providerFailure: result.Status is not SecurityStoreResultStatus.NotFound
                        and not SecurityStoreResultStatus.Cancelled);
                return accessTokenResult;
            }

            var credential = result.Value!;
            if (request.Scheme is not null &&
                !string.Equals(request.Scheme, credential.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                WriteTokenDiagnostic(request, AccessTokenResultStatus.Unavailable, captured.AccountKey);
                return AccessTokenResult.Unavailable("The stored credential does not use the requested scheme.");
            }

            if (credential.ExpiresAt is not null && credential.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                WriteTokenDiagnostic(
                    request,
                    AccessTokenResultStatus.Expired,
                    captured.AccountKey,
                    credential.Scheme,
                    credential.ExpiresAt);
                return AccessTokenResult.Expired("The stored credential has expired.");
            }

            WriteTokenDiagnostic(
                request,
                AccessTokenResultStatus.Success,
                captured.AccountKey,
                credential.Scheme,
                credential.ExpiresAt);
            return AccessTokenResult.Success(credential.AccessToken, credential.Scheme, credential.ExpiresAt);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async ValueTask<AccountSwitchResult> ExecuteAsync(
        string operation,
        SecurityAccountKey? targetAccountKey,
        Func<Guid, CancellationToken, ValueTask<OperationOutcome>> action,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        try
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                operation,
                operationId,
                StageGate,
                AccountSwitchResultStatus.Cancelled,
                "The account operation was cancelled.",
                targetAccountKey: targetAccountKey);
        }

        OperationOutcome outcome;
        try
        {
            outcome = await action(operationId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = new OperationOutcome(Failure(
                operation,
                operationId,
                StageCommit,
                AccountSwitchResultStatus.Cancelled,
                "The account operation was cancelled.",
                targetAccountKey: targetAccountKey));
        }
        catch (Exception exception)
        {
            outcome = new OperationOutcome(Failure(
                operation,
                operationId,
                StageCommit,
                AccountSwitchResultStatus.Failed,
                "The account operation failed.",
                exception,
                targetAccountKey));
        }
        finally
        {
            _operationGate.Release();
        }

        if (outcome.AuthenticationTransition is { } authenticationTransition)
        {
            _authenticationState.DrainDeferredNotifications(authenticationTransition);
        }

        if (outcome.ShouldDrainSession)
        {
            _eventPublisher.Drain(this);
        }

        return outcome.Result;
    }

    private async ValueTask<OperationOutcome> RestoreCoreAsync(
        Guid operationId,
        AccountSwitchOptions options,
        CancellationToken cancellationToken)
    {
        var activeResult = await _accountStore.GetLastActiveAccountAsync(cancellationToken).ConfigureAwait(false);
        if (!activeResult.Succeeded)
        {
            return new OperationOutcome(FailureFromStore("restore", operationId, StageActiveAccount, activeResult));
        }

        if (activeResult.Value is null)
        {
            return CommitAnonymous("restore", operationId, signedOut: false);
        }

        return await SwitchCoreAsync(
            activeResult.Value,
            options,
            operationId,
            persistActiveAccount: false,
            forceReload: false,
            cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<OperationOutcome> RefreshCoreAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions options,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var current = Current;
        if (current.AccountKey?.Equals(accountKey) != true)
        {
            return ValueTask.FromResult(new OperationOutcome(Failure(
                "refresh",
                operationId,
                StageAccount,
                AccountSwitchResultStatus.Failed,
                "The requested account is no longer the active account.",
                targetAccountKey: accountKey)));
        }

        return SwitchCoreAsync(
            accountKey,
            options,
            operationId,
            persistActiveAccount: false,
            forceReload: true,
            cancellationToken);
    }

    private async ValueTask<OperationOutcome> SwitchCoreAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions options,
        Guid operationId,
        bool persistActiveAccount,
        bool forceReload,
        CancellationToken cancellationToken)
    {
        var current = Current;
        var operation = forceReload ? "refresh" : "switch";
        if (!forceReload && current.AccountKey?.Equals(accountKey) == true)
        {
            return new OperationOutcome(AccountSwitchResult.Success(current, operationId));
        }

        var accountResult = await _accountStore.GetAsync(accountKey, cancellationToken).ConfigureAwait(false);
        if (!accountResult.Succeeded)
        {
            return new OperationOutcome(FailureFromStore(
                operation,
                operationId,
                StageAccount,
                accountResult,
                accountKey));
        }

        var account = accountResult.Value!;
        var now = _timeProvider.GetUtcNow();
        var permissionExpired = account.Permissions.IsExpired(now);
        if (permissionExpired && !options.AllowOffline)
        {
            return new OperationOutcome(Failure(
                operation,
                operationId,
                StagePermissions,
                AccountSwitchResultStatus.PermissionUnavailable,
                "The stored permission snapshot has expired.",
                targetAccountKey: accountKey));
        }

        var resourceName = options.CredentialResourceName ?? _options.DefaultCredentialResource;
        var credentialResult = await _credentialStore.GetAsync(accountKey, resourceName, cancellationToken)
            .ConfigureAwait(false);
        AccountCredentialSnapshot? credential = null;
        var credentialUnavailable = false;
        if (credentialResult.Succeeded)
        {
            credential = credentialResult.Value!;
            credentialUnavailable = credential.ExpiresAt is not null && credential.ExpiresAt <= now;
        }
        else if (credentialResult.Status == SecurityStoreResultStatus.NotFound)
        {
            credentialUnavailable = true;
        }
        else
        {
            return new OperationOutcome(FailureFromStore(
                operation,
                operationId,
                StageCredential,
                credentialResult,
                accountKey));
        }

        if (credentialUnavailable && !options.AllowOffline)
        {
            return new OperationOutcome(Failure(
                operation,
                operationId,
                StageCredential,
                AccountSwitchResultStatus.CredentialUnavailable,
                credential is null
                    ? "No credential is stored for this account."
                    : "The stored credential has expired.",
                targetAccountKey: accountKey));
        }

        if (persistActiveAccount)
        {
            var activeResult = await _accountStore.SetLastActiveAccountAsync(accountKey, cancellationToken)
                .ConfigureAwait(false);
            if (!activeResult.Succeeded)
            {
                return new OperationOutcome(FailureFromStore(
                    operation,
                    operationId,
                    StageActiveAccount,
                    activeResult,
                    accountKey));
            }
        }

        var mode = permissionExpired || credentialUnavailable
            ? AccountSessionMode.OfflineRestricted
            : AccountSessionMode.Online;
        return CommitAccount(
            operation,
            operationId,
            account,
            credential,
            resourceName,
            mode,
            permissionExpired,
            credentialUnavailable);
    }

    private async ValueTask<OperationOutcome> RemoveCoreAsync(
        SecurityAccountKey accountKey,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var credentialResult = await _credentialStore.RemoveAllAsync(accountKey, cancellationToken)
            .ConfigureAwait(false);
        if (!credentialResult.Succeeded)
        {
            return new OperationOutcome(FailureFromStore(
                "remove",
                operationId,
                StageRemoveCredentials,
                credentialResult,
                accountKey));
        }

        var accountResult = await _accountStore.RemoveAsync(accountKey, cancellationToken).ConfigureAwait(false);
        if (!accountResult.Succeeded)
        {
            return new OperationOutcome(FailureFromStore(
                "remove",
                operationId,
                StageRemoveAccount,
                accountResult,
                accountKey));
        }

        if (Current.AccountKey?.Equals(accountKey) == true)
        {
            return CommitAnonymous("remove", operationId, signedOut: true);
        }

        var current = Current;
        WriteSessionDiagnostic(
            "remove",
            operationId,
            AccountSwitchResultStatus.Success,
            current.AccountKey,
            accountKey,
            current.Mode,
            null,
            null);
        return new OperationOutcome(AccountSwitchResult.Success(current, operationId));
    }

    private OperationOutcome CommitAccount(
        string operation,
        Guid operationId,
        AccountRecordSnapshot account,
        AccountCredentialSnapshot? credential,
        string resourceName,
        AccountSessionMode mode,
        bool permissionExpired,
        bool credentialUnavailable)
    {
        var previous = Current;
        var principal = CreatePrincipal(account, includePermissions: !permissionExpired);
        AuthenticationStateStore.AuthenticationStateTransition authenticationTransition;
        if (credentialUnavailable)
        {
            authenticationTransition = _authenticationState.SetExpiredForAccountDeferred(
                principal,
                credential?.Scheme ?? account.AccountKey.AuthenticationScheme,
                credential?.ExpiresAt);
        }
        else
        {
            authenticationTransition = _authenticationState.SetAuthenticatedForAccountDeferred(
                principal,
                credential?.Scheme ?? account.AccountKey.AuthenticationScheme,
                credential?.ExpiresAt);
        }

        var credentialContext = new AccountCredentialContext(
            resourceName,
            credential?.Scheme,
            credential?.ExpiresAt,
            credential is not null);
        var next = new AccountSessionSnapshot(
            mode,
            authenticationTransition.Snapshot.Revision,
            account.Profile,
            account.Permissions,
            credentialContext,
            principal);
        var shouldDrain = Publish(next, operationId);
        WriteSessionDiagnostic(
            operation,
            operationId,
            AccountSwitchResultStatus.Success,
            previous.AccountKey,
            account.AccountKey,
            mode,
            null,
            null);
        return new OperationOutcome(
            AccountSwitchResult.Success(next, operationId),
            shouldDrain,
            authenticationTransition);
    }

    private OperationOutcome CommitAnonymous(string operation, Guid operationId, bool signedOut)
    {
        var previous = Current;
        var authenticationTransition = _authenticationState.SetAnonymousForAccountDeferred(signedOut);
        var next = AccountSessionSnapshot.Anonymous(authenticationTransition.Snapshot.Revision);
        var shouldDrain = Publish(next, operationId);
        WriteSessionDiagnostic(
            operation,
            operationId,
            AccountSwitchResultStatus.Success,
            previous.AccountKey,
            null,
            AccountSessionMode.Anonymous,
            null,
            null);
        return new OperationOutcome(
            AccountSwitchResult.Success(next, operationId),
            shouldDrain,
            authenticationTransition);
    }

    private bool Publish(AccountSessionSnapshot next, Guid operationId)
    {
        AccountSessionSnapshot previous;
        AccountSessionChangedEventArgs args;
        lock (_syncRoot)
        {
            previous = _current;
            if (SessionsEqual(previous, next))
            {
                return false;
            }

            _current = next;
            args = new AccountSessionChangedEventArgs(previous, next, operationId);
            return _eventPublisher.Enqueue(SessionChanged, args);
        }
    }

    private AccountSwitchResult FailureFromStore<T>(
        string operation,
        Guid operationId,
        string stage,
        SecurityStoreResult<T> result,
        SecurityAccountKey? targetAccountKey = null)
    {
        var status = result.Status switch
        {
            SecurityStoreResultStatus.NotFound => AccountSwitchResultStatus.NotFound,
            SecurityStoreResultStatus.InvalidData or SecurityStoreResultStatus.UnsupportedSchema =>
                AccountSwitchResultStatus.InvalidData,
            SecurityStoreResultStatus.Cancelled => AccountSwitchResultStatus.Cancelled,
            _ => AccountSwitchResultStatus.Failed,
        };
        return Failure(
            operation,
            operationId,
            stage,
            status,
            result.Message,
            result.Exception,
            targetAccountKey);
    }

    private AccountSwitchResult Failure(
        string operation,
        Guid operationId,
        string stage,
        AccountSwitchResultStatus status,
        string? message,
        Exception? exception = null,
        SecurityAccountKey? targetAccountKey = null)
    {
        var current = Current;
        WriteSessionDiagnostic(
            operation,
            operationId,
            status,
            current.AccountKey,
            targetAccountKey,
            current.Mode,
            stage,
            exception);
        return AccountSwitchResult.Failed(status, current, operationId, stage, message, exception);
    }

    private void WriteSessionDiagnostic(
        string operation,
        Guid operationId,
        AccountSwitchResultStatus status,
        SecurityAccountKey? previousAccountKey,
        SecurityAccountKey? targetAccountKey,
        AccountSessionMode mode,
        string? failureStage,
        Exception? exception)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            status == AccountSwitchResultStatus.Success
                ? SecurityDiagnosticIds.AccountSessionChanged
                : SecurityDiagnosticIds.AccountSessionOperationFailed,
            status == AccountSwitchResultStatus.Success
                ? "Account session operation completed."
                : "Account session operation failed.",
            status == AccountSwitchResultStatus.Success
                ? HostDiagnosticSeverity.Info
                : HostDiagnosticSeverity.Error,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["operationId"] = operationId.ToString("D", CultureInfo.InvariantCulture),
                ["previousAccountIdHash"] = previousAccountKey?.ToStorageKey()[..16],
                ["targetAccountIdHash"] = targetAccountKey?.ToStorageKey()[..16],
                ["mode"] = mode.ToString(),
                ["resultStatus"] = status.ToString(),
                ["failureStage"] = failureStage,
                ["exceptionType"] = exception?.GetType().FullName,
            });
    }

    private void WriteTokenDiagnostic(
        AccessTokenRequest request,
        AccessTokenResultStatus status,
        SecurityAccountKey? accountKey,
        string? resolvedScheme = null,
        DateTimeOffset? expiresAt = null,
        Exception? exception = null,
        bool providerFailure = false)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            providerFailure
                ? SecurityDiagnosticIds.AccessTokenProviderFailed
                : SecurityDiagnosticIds.AccessTokenResolved,
            providerFailure
                ? "The access token provider failed."
                : "Access token request completed.",
            providerFailure ? HostDiagnosticSeverity.Error : HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["resourceName"] = request.ResourceName,
                ["scheme"] = resolvedScheme ?? request.Scheme,
                ["operationName"] = request.OperationName,
                ["accountIdHash"] = accountKey?.ToStorageKey()[..16],
                ["status"] = status.ToString(),
                ["expiresAt"] = expiresAt?.ToString("O", CultureInfo.InvariantCulture),
                ["exceptionType"] = exception?.GetType().FullName,
            });
    }

    private static ClaimsPrincipal CreatePrincipal(
        AccountRecordSnapshot account,
        bool includePermissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.AccountKey.SubjectId, ClaimValueTypes.String, account.AccountKey.Authority),
            new(ClaimTypes.Name, account.Profile.DisplayName, ClaimValueTypes.String, account.AccountKey.Authority),
            new(SecurityClaimTypes.Authority, account.AccountKey.Authority, ClaimValueTypes.String, account.AccountKey.Authority),
        };
        if (account.AccountKey.TenantId is not null)
        {
            claims.Add(new Claim(
                SecurityClaimTypes.TenantId,
                account.AccountKey.TenantId,
                ClaimValueTypes.String,
                account.AccountKey.Authority));
        }

        if (includePermissions)
        {
            claims.AddRange(account.Permissions.Permissions.Select(permission =>
                new Claim(
                    SecurityClaimTypes.Permission,
                    permission,
                    ClaimValueTypes.String,
                    account.AccountKey.Authority)));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            account.AccountKey.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role));
    }

    private static bool SessionsEqual(AccountSessionSnapshot left, AccountSessionSnapshot right) =>
        left.Mode == right.Mode &&
        Equals(left.AccountKey, right.AccountKey) &&
        left.Revision == right.Revision;

    private readonly record struct OperationOutcome(
        AccountSwitchResult Result,
        bool ShouldDrainSession = false,
        AuthenticationStateStore.AuthenticationStateTransition? AuthenticationTransition = null);
}
