namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for idata request cache.
/// </summary>
public interface IDataRequestCache
{
    /// <summary>
    /// Executes the try get async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataCacheLookup<TResponse>> TryGetAsync<TResponse>(
        DataCacheKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the set async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask SetAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the invalidate async operation.
    /// </summary>
    ValueTask InvalidateAsync(
        DataCacheKey key,
        CancellationToken cancellationToken = default);
}
