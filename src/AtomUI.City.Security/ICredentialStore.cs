namespace AtomUI.City.Security;

public interface ICredentialStore
{
    ValueTask<SecurityStoreResult<AccountCredentialSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountCredentialSnapshot credential,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> RemoveAllAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);
}
