namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iaccount session manager.
/// </summary>
public interface IAccountSessionManager : IAccessTokenProvider
{
    /// <summary>
    /// Gets current.
    /// </summary>
    AccountSessionSnapshot Current { get; }

    /// <summary>
    /// Occurs when session changed.
    /// </summary>
    event EventHandler<AccountSessionChangedEventArgs>? SessionChanged;

    /// <summary>
    /// Executes the list accounts async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAccountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the restore async operation.
    /// </summary>
    ValueTask<AccountSwitchResult> RestoreAsync(
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the switch account async operation.
    /// </summary>
    ValueTask<AccountSwitchResult> SwitchAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the refresh account async operation.
    /// </summary>
    ValueTask<AccountSwitchResult> RefreshAccountAsync(
        SecurityAccountKey accountKey,
        AccountSwitchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the remove account async operation.
    /// </summary>
    ValueTask<AccountSwitchResult> RemoveAccountAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);
}
