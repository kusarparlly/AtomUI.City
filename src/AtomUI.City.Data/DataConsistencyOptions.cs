namespace AtomUI.City.Data;

/// <summary>
/// Represents data consistency options.
/// </summary>
public sealed class DataConsistencyOptions
{
    private IReadOnlyList<DataCacheInvalidation> _invalidationsOnSuccess = [];

    /// <summary>
    /// Represents the invalidations on success value.
    /// </summary>
    public IReadOnlyList<DataCacheInvalidation> InvalidationsOnSuccess
    {
        get => _invalidationsOnSuccess;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(static invalidation => invalidation is null))
            {
                throw new ArgumentException("Cache invalidations cannot contain null values.", nameof(InvalidationsOnSuccess));
            }

            _invalidationsOnSuccess = Array.AsReadOnly(value.ToArray());
        }
    }

    /// <summary>
    /// Gets or sets optimistic update.
    /// </summary>
    public IDataOptimisticUpdate? OptimisticUpdate { get; init; }

    /// <summary>
    /// Gets or sets roll back on cancellation.
    /// </summary>
    public bool RollBackOnCancellation { get; init; } = true;

    /// <summary>
    /// Gets none.
    /// </summary>
    public static DataConsistencyOptions None { get; } = new();
}

/// <summary>
/// Defines the contract for idata optimistic update.
/// </summary>
public interface IDataOptimisticUpdate
{
    /// <summary>
    /// Executes the apply async operation.
    /// </summary>
    ValueTask ApplyAsync(DataRequestContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the confirm async operation.
    /// </summary>
    ValueTask ConfirmAsync(DataRequestContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the roll back async operation.
    /// </summary>
    ValueTask RollBackAsync(DataRequestContext context, CancellationToken cancellationToken = default);
}
