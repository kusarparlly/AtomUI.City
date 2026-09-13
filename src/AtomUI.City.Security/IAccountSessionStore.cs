namespace AtomUI.City.Security;

public interface IAccountSessionStore
{
    ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAsync(
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<AccountRecordSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountRecordSnapshot account,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<SecurityAccountKey?>> GetLastActiveAccountAsync(
        CancellationToken cancellationToken = default);

    ValueTask<SecurityStoreResult<bool>> SetLastActiveAccountAsync(
        SecurityAccountKey? accountKey,
        CancellationToken cancellationToken = default);
}
