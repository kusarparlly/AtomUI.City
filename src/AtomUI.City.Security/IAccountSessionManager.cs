namespace AtomUI.City.Security;

public interface IAccountSessionManager : IAccessTokenProvider
{
    AccountSessionSnapshot Current { get; }

    event EventHandler<AccountSessionChangedEventArgs>? SessionChanged;

    ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAccountsAsync(
        CancellationToken cancellationToken = default);

    ValueTask<AccountSwitchResult> RestoreAsync(
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<AccountSwitchResult> SwitchAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<AccountSwitchResult> RefreshAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<AccountSwitchResult> RemoveAccountAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);
}
