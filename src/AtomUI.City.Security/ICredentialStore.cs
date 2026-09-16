namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for icredential store.
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Executes the get async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<AccountCredentialSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the save async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountCredentialSnapshot credential,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the remove async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the remove all async operation.
    /// </summary>
    ValueTask<SecurityStoreResult<bool>> RemoveAllAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);
}
