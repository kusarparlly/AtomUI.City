namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iaccount session store.
/// </summary>
public interface IAccountSessionStore
{
    /// <summary>
    /// Executes the list async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<AccountRecordSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the save async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountRecordSnapshot account,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the remove async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get last active account async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<SecurityAccountKey?>> GetLastActiveAccountAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the set last active account async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> SetLastActiveAccountAsync(
        SecurityAccountKey? accountKey,
        CancellationToken cancellationToken = default);
}
