using System.Collections.Concurrent;

namespace AtomUI.City.Data;

/// <summary>
/// Represents in memory data request cache.
/// </summary>
public sealed class InMemoryDataRequestCache :
    IDataExpiringRequestCache,
    IDataCacheInvalidator,
    IDataCacheMutationGuard
{
    private readonly ConcurrentDictionary<DataCacheKey, CacheEntry> _entries = new();
    private readonly object _mutationSyncRoot = new();
    private readonly IDataDiagnostics? _diagnostics;
    private readonly TimeProvider _timeProvider;
    private long _mutationEpoch;

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryDataRequestCache</c> type.
    /// </summary>
    public InMemoryDataRequestCache(
        IDataDiagnostics? diagnostics = null,
        TimeProvider? timeProvider = null)
    {
        _diagnostics = diagnostics;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Executes the try get async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask<DataCacheLookup<TResponse>> TryGetAsync<TResponse>(
        DataCacheKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var now = _timeProvider.GetUtcNow();
        var expiredRemoved = false;
        DataCacheLookup<TResponse> result;
        lock (_mutationSyncRoot)
        {
            if (_entries.TryGetValue(key, out var entry)
                && entry.ExpiresAt is { } expiresAt
                && expiresAt <= now)
            {
                expiredRemoved = _entries.TryRemove(new KeyValuePair<DataCacheKey, CacheEntry>(key, entry));
                Interlocked.Increment(ref _mutationEpoch);
            }

            result = !expiredRemoved
                     && _entries.TryGetValue(key, out entry)
                     && entry.ResponseType == typeof(TResponse)
                ? DataCacheLookup<TResponse>.Hit((TResponse?)entry.Value)
                : DataCacheLookup<TResponse>.Miss();
        }

        if (expiredRemoved)
        {
            WriteInvalidationDiagnostic(key, DataCacheInvalidationReason.Expired, 1);
        }

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Executes the set async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask SetAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        return SetAsync(key, value, DataCacheEntryOptions.NoExpiration, cancellationToken);
    }

    /// <summary>
    /// Executes the set async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask SetAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var expiresAt = options.TimeToLive is { } ttl
            ? _timeProvider.GetUtcNow().Add(ttl)
            : (DateTimeOffset?)null;
        lock (_mutationSyncRoot)
        {
            _entries[key] = new CacheEntry(typeof(TResponse), value, expiresAt);
        }

        return ValueTask.CompletedTask;
    }

    long IDataCacheMutationGuard.CaptureMutationEpoch() => Volatile.Read(ref _mutationEpoch);

    ValueTask<bool> IDataCacheMutationGuard.TrySetIfUnchangedAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        long expectedEpoch,
        CancellationToken cancellationToken)
        where TResponse : default
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAt = options.TimeToLive is { } ttl
            ? _timeProvider.GetUtcNow().Add(ttl)
            : (DateTimeOffset?)null;
        lock (_mutationSyncRoot)
        {
            if (_mutationEpoch != expectedEpoch)
            {
                return ValueTask.FromResult(false);
            }

            _entries[key] = new CacheEntry(typeof(TResponse), value, expiresAt);
            return ValueTask.FromResult(true);
        }
    }

    /// <summary>
    /// Executes the invalidate async operation.
    /// </summary>
    public ValueTask InvalidateAsync(
        DataCacheKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        int removed;
        lock (_mutationSyncRoot)
        {
            Interlocked.Increment(ref _mutationEpoch);
            removed = _entries.TryRemove(key, out _) ? 1 : 0;
        }

        WriteInvalidationDiagnostic(key, DataCacheInvalidationReason.Manual, removed);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the invalidate async operation.
    /// </summary>
    public ValueTask<DataCacheInvalidationResult> InvalidateAsync(
        DataCacheInvalidation invalidation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        cancellationToken.ThrowIfCancellationRequested();

        int removed;
        DataCacheKey? sample;
        lock (_mutationSyncRoot)
        {
            Interlocked.Increment(ref _mutationEpoch);
            removed = 0;
            sample = null;
            foreach (var entry in _entries)
            {
                if (!invalidation.Matches(entry.Key)
                    || !_entries.TryRemove(new KeyValuePair<DataCacheKey, CacheEntry>(entry.Key, entry.Value)))
                {
                    continue;
                }

                sample ??= entry.Key;
                removed++;
            }
        }

        WriteInvalidationDiagnostic(sample, invalidation.Reason, removed);
        return ValueTask.FromResult(new DataCacheInvalidationResult(removed));
    }

    private void WriteInvalidationDiagnostic(
        DataCacheKey? key,
        DataCacheInvalidationReason reason,
        int removed)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.CacheInvalidated,
            $"Data cache invalidation '{reason}' removed {removed} entr{(removed == 1 ? "y" : "ies")}.",
            DataDiagnosticSeverity.Info,
            ClientId: key?.ClientId,
            OperationName: key?.OperationName,
            TransportKind: key?.TransportKind));
    }

    private sealed record CacheEntry(Type ResponseType, object? Value, DateTimeOffset? ExpiresAt);
}
