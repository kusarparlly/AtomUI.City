using AtomUI.City.Security;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodRefreshingAccessTokenProvider : IAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RefreshedLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PermissionRefreshSkew = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshedPermissionLifetime = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly IAccountSessionManager _sessions;
    private readonly IAccountSessionStore _accounts;
    private readonly ICredentialStore _credentials;
    private readonly TimeProvider _timeProvider;
    private readonly DogfoodRunLedger _ledger;
    private int _refreshCount;
    private int _permissionRefreshCount;

    public DogfoodRefreshingAccessTokenProvider(
        IAccountSessionManager sessions,
        IAccountSessionStore accounts,
        ICredentialStore credentials,
        TimeProvider timeProvider,
        DogfoodRunLedger ledger)
    {
        _sessions = sessions;
        _accounts = accounts;
        _credentials = credentials;
        _timeProvider = timeProvider;
        _ledger = ledger;
    }

    public int RefreshCount => Volatile.Read(ref _refreshCount);

    public int PermissionRefreshCount => Volatile.Read(ref _permissionRefreshCount);

    public async ValueTask<bool> PrepareAccountForOnlineSwitchAsync(
        SecurityAccountKey accountKey,
        string credentialResourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialResourceName);

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var refreshed = false;
            var accountResult = await _accounts.GetAsync(accountKey, cancellationToken).ConfigureAwait(false);
            if (!accountResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The target account snapshot is unavailable: {accountResult.Status}.",
                    accountResult.Exception);
            }

            var credentialResult = await _credentials
                .GetAsync(accountKey, credentialResourceName, cancellationToken)
                .ConfigureAwait(false);
            if (!credentialResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The target account credential is unavailable: {credentialResult.Status}.",
                    credentialResult.Exception);
            }

            var now = _timeProvider.GetUtcNow();
            var credential = credentialResult.Value!;
            if (credential.ExpiresAt is { } expiresAt && expiresAt <= now + RefreshSkew)
            {
                if (string.IsNullOrWhiteSpace(credential.RefreshToken))
                {
                    throw new InvalidOperationException("The target account credential cannot be refreshed.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
                var savedCredential = await _credentials.SaveAsync(
                    new AccountCredentialSnapshot(
                        credential.AccountKey,
                        credential.ResourceName,
                        credential.AccessToken,
                        credential.Scheme,
                        credential.RefreshToken,
                        now + RefreshedLifetime),
                    cancellationToken).ConfigureAwait(false);
                if (!savedCredential.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"The refreshed target credential could not be persisted: {savedCredential.Status}.",
                        savedCredential.Exception);
                }

                Interlocked.Increment(ref _refreshCount);
                _ledger.Record("security-token-refresh", credentialResourceName);
                refreshed = true;
            }

            var account = accountResult.Value!;
            if (account.Permissions.ExpiresAt <= now + PermissionRefreshSkew)
            {
                var savedAccount = await _accounts.SaveAsync(
                    new AccountRecordSnapshot(
                        account.Profile,
                        new PersistedPermissionSnapshot(
                            accountKey,
                            account.Permissions.Permissions,
                            checked(account.Permissions.Revision + 1),
                            now,
                            now + RefreshedPermissionLifetime)),
                    cancellationToken).ConfigureAwait(false);
                if (!savedAccount.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"The refreshed target permission snapshot could not be persisted: {savedAccount.Status}.",
                        savedAccount.Exception);
                }

                Interlocked.Increment(ref _permissionRefreshCount);
                _ledger.Record("security-permission-refresh", accountKey.SubjectId);
                refreshed = true;
            }

            return refreshed;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var initial = await _sessions.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
        if (!RequiresRefresh(initial))
        {
            return initial;
        }

        try
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AccessTokenResult.Cancelled("Credential refresh was cancelled.");
        }

        try
        {
            var captured = _sessions.Current;
            if (captured.Mode != AccountSessionMode.Online || captured.AccountKey is null)
            {
                return initial;
            }

            var stored = await _credentials
                .GetAsync(captured.AccountKey, request.ResourceName, cancellationToken)
                .ConfigureAwait(false);
            if (!stored.Succeeded)
            {
                return AccessTokenResult.Unavailable("The credential required for refresh is unavailable.");
            }

            var credential = stored.Value!;
            var now = _timeProvider.GetUtcNow();
            if (credential.ExpiresAt is null || credential.ExpiresAt > now + RefreshSkew)
            {
                return await _sessions.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(credential.RefreshToken))
            {
                return initial.Status == AccessTokenResultStatus.Expired
                    ? initial
                    : AccessTokenResult.Expired("The credential cannot be refreshed.");
            }

            // This fixture acts as its own identity provider. Production applications replace
            // this step with their protocol-specific token endpoint call.
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
            var current = _sessions.Current;
            if (current.Revision != captured.Revision || !Equals(current.AccountKey, captured.AccountKey))
            {
                return AccessTokenResult.Unavailable("The active account changed during credential refresh.");
            }

            var refreshed = new AccountCredentialSnapshot(
                credential.AccountKey,
                credential.ResourceName,
                credential.AccessToken,
                credential.Scheme,
                credential.RefreshToken,
                now + RefreshedLifetime);
            var saved = await _credentials.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
            if (!saved.Succeeded)
            {
                return AccessTokenResult.Unavailable("The refreshed credential could not be persisted.");
            }

            current = _sessions.Current;
            if (current.Revision != captured.Revision || !Equals(current.AccountKey, captured.AccountKey))
            {
                return AccessTokenResult.Unavailable("The active account changed while credential refresh was committed.");
            }

            var permissionRefresh = await RefreshPermissionsIfNeededAsync(
                captured,
                request.ResourceName,
                cancellationToken).ConfigureAwait(false);
            if (permissionRefresh is not null)
            {
                return permissionRefresh;
            }

            Interlocked.Increment(ref _refreshCount);
            _ledger.Record("security-token-refresh", request.ResourceName);
            return await _sessions.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AccessTokenResult.Cancelled("Credential refresh was cancelled.");
        }
        catch (Exception exception)
        {
            return AccessTokenResult.Failed("Credential refresh failed.", exception);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public void Dispose()
    {
        _refreshGate.Dispose();
    }

    private bool RequiresRefresh(AccessTokenResult result)
    {
        if (result.Status == AccessTokenResultStatus.Expired)
        {
            return true;
        }

        return result.Succeeded &&
               result.ExpiresAt is { } expiresAt &&
               expiresAt <= _timeProvider.GetUtcNow() + RefreshSkew;
    }

    private async ValueTask<AccessTokenResult?> RefreshPermissionsIfNeededAsync(
        AccountSessionSnapshot captured,
        string credentialResourceName,
        CancellationToken cancellationToken)
    {
        var accountKey = captured.AccountKey!;
        var accountResult = await _accounts.GetAsync(accountKey, cancellationToken).ConfigureAwait(false);
        if (!accountResult.Succeeded)
        {
            return AccessTokenResult.Unavailable("The account snapshot required for permission refresh is unavailable.");
        }

        var account = accountResult.Value!;
        var now = _timeProvider.GetUtcNow();
        if (account.Permissions.ExpiresAt > now + PermissionRefreshSkew)
        {
            return null;
        }

        var current = _sessions.Current;
        if (current.Revision != captured.Revision || !Equals(current.AccountKey, accountKey))
        {
            return AccessTokenResult.Unavailable("The active account changed during permission refresh.");
        }

        // This fixture acts as its own authorization server. A production application
        // replaces this snapshot with the server's freshly issued permission set.
        var refreshedAccount = new AccountRecordSnapshot(
            account.Profile,
            new PersistedPermissionSnapshot(
                accountKey,
                account.Permissions.Permissions,
                checked(account.Permissions.Revision + 1),
                now,
                now + RefreshedPermissionLifetime));
        var saved = await _accounts.SaveAsync(refreshedAccount, cancellationToken).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            return AccessTokenResult.Unavailable("The refreshed permission snapshot could not be persisted.");
        }

        var refreshedSession = await _sessions.RefreshAccountAsync(
            accountKey,
            new AccountSwitchOptions(
                allowOffline: false,
                credentialResourceName: credentialResourceName),
            cancellationToken).ConfigureAwait(false);
        if (!refreshedSession.Succeeded)
        {
            return AccessTokenResult.Unavailable(
                $"The refreshed permission snapshot could not be published: {refreshedSession.Status}.");
        }

        Interlocked.Increment(ref _permissionRefreshCount);
        _ledger.Record("security-permission-refresh", accountKey.SubjectId);
        return null;
    }
}
