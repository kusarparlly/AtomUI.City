namespace AtomUI.City.Data;

/// <summary>
/// Represents data cache entry options.
/// </summary>
public sealed class DataCacheEntryOptions
{
    private TimeSpan? _timeToLive;

    /// <summary>
    /// Represents the time to live value.
    /// </summary>
    public TimeSpan? TimeToLive
    {
        get => _timeToLive;
        init
        {
            if (value is { } duration && duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeToLive), value, "Cache time-to-live must be greater than zero.");
            }

            _timeToLive = value;
        }
    }

    /// <summary>
    /// Gets no expiration.
    /// </summary>
    public static DataCacheEntryOptions NoExpiration { get; } = new();
}

/// <summary>
/// Defines the supported data cache invalidation reason values.
/// </summary>
public enum DataCacheInvalidationReason
{
    /// <summary>
    /// Represents the manual value.
    /// </summary>
    Manual,
    /// <summary>
    /// Represents the mutation value.
    /// </summary>
    Mutation,
    /// <summary>
    /// Represents the subscription value.
    /// </summary>
    Subscription,
    /// <summary>
    /// Represents the principal changed value.
    /// </summary>
    PrincipalChanged,
    /// <summary>
    /// Represents the permission changed value.
    /// </summary>
    PermissionChanged,
    /// <summary>
    /// Represents the plugin revoked value.
    /// </summary>
    PluginRevoked,
    /// <summary>
    /// Represents the client version changed value.
    /// </summary>
    ClientVersionChanged,
    /// <summary>
    /// Represents the route left value.
    /// </summary>
    RouteLeft,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired,
}

/// <summary>
/// Represents data cache invalidation.
/// </summary>
public sealed class DataCacheInvalidation
{
    private DataCacheInvalidation(
        DataCacheInvalidationReason reason,
        IReadOnlySet<DataCacheKey>? exactKeys = null,
        string? clientId = null,
        string? operationName = null,
        string? principalRevision = null,
        string? permissionRevision = null,
        string? pluginContributionId = null,
        string? clientVersion = null,
        string? policyVersion = null)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Cache invalidation reason is not supported.");
        }

        Reason = reason;
        ExactKeys = exactKeys;
        ClientId = ValidateOptional(clientId, nameof(clientId));
        OperationName = ValidateOptional(operationName, nameof(operationName));
        PrincipalRevision = ValidateOptional(principalRevision, nameof(principalRevision));
        PermissionRevision = ValidateOptional(permissionRevision, nameof(permissionRevision));
        PluginContributionId = ValidateOptional(pluginContributionId, nameof(pluginContributionId));
        ClientVersion = ValidateOptional(clientVersion, nameof(clientVersion));
        PolicyVersion = ValidateOptional(policyVersion, nameof(policyVersion));
    }

    /// <summary>
    /// Gets reason.
    /// </summary>
    public DataCacheInvalidationReason Reason { get; }

    /// <summary>
    /// Gets exact keys.
    /// </summary>
    public IReadOnlySet<DataCacheKey>? ExactKeys { get; }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets principal revision.
    /// </summary>
    public string? PrincipalRevision { get; }

    /// <summary>
    /// Gets permission revision.
    /// </summary>
    public string? PermissionRevision { get; }

    /// <summary>
    /// Gets plugin contribution id.
    /// </summary>
    public string? PluginContributionId { get; }

    /// <summary>
    /// Gets client version.
    /// </summary>
    public string? ClientVersion { get; }

    /// <summary>
    /// Gets policy version.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets all.
    /// </summary>
    public static DataCacheInvalidation All(DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason);

    /// <summary>
    /// Executes the keys operation.
    /// </summary>
    public static DataCacheInvalidation Keys(
        IEnumerable<DataCacheKey> keys,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var snapshot = keys.ToHashSet();
        if (snapshot.Contains(null!))
        {
            throw new ArgumentException("Cache invalidation keys cannot contain null values.", nameof(keys));
        }

        if (snapshot.Count == 0)
        {
            throw new ArgumentException("At least one cache key is required.", nameof(keys));
        }

        return new DataCacheInvalidation(reason, snapshot);
    }

    /// <summary>
    /// Executes the for client operation.
    /// </summary>
    public static DataCacheInvalidation ForClient(
        string clientId,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, clientId: clientId);

    /// <summary>
    /// Executes the for operation operation.
    /// </summary>
    public static DataCacheInvalidation ForOperation(
        string clientId,
        string operationName,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, clientId: clientId, operationName: operationName);

    /// <summary>
    /// Executes the for principal operation.
    /// </summary>
    public static DataCacheInvalidation ForPrincipal(
        string principalRevision,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PrincipalChanged) =>
        new(reason, principalRevision: principalRevision);

    /// <summary>
    /// Executes the for permission revision operation.
    /// </summary>
    public static DataCacheInvalidation ForPermissionRevision(
        string permissionRevision,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PermissionChanged) =>
        new(reason, permissionRevision: permissionRevision);

    /// <summary>
    /// Executes the for plugin operation.
    /// </summary>
    public static DataCacheInvalidation ForPlugin(
        string pluginContributionId,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PluginRevoked) =>
        new(reason, pluginContributionId: pluginContributionId);

    /// <summary>
    /// Executes the for client version operation.
    /// </summary>
    public static DataCacheInvalidation ForClientVersion(
        string clientId,
        string clientVersion,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.ClientVersionChanged) =>
        new(reason, clientId: clientId, clientVersion: clientVersion);

    /// <summary>
    /// Executes the for policy version operation.
    /// </summary>
    public static DataCacheInvalidation ForPolicyVersion(
        string policyVersion,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, policyVersion: policyVersion);

    /// <summary>
    /// Executes the matches operation.
    /// </summary>
    public bool Matches(DataCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (ExactKeys is not null && !ExactKeys.Contains(key))
        {
            return false;
        }

        return Matches(ClientId, key.ClientId)
            && Matches(OperationName, key.OperationName)
            && Matches(PrincipalRevision, key.PrincipalRevision)
            && Matches(PermissionRevision, key.PermissionRevision)
            && Matches(PluginContributionId, key.PluginContributionId)
            && Matches(ClientVersion, key.ClientVersion)
            && Matches(PolicyVersion, key.PolicyVersion);
    }

    private static bool Matches(string? expected, string? actual) =>
        expected is null || string.Equals(expected, actual, StringComparison.Ordinal);

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }
}

/// <summary>
/// Represents data cache invalidation result.
/// </summary>
public sealed record DataCacheInvalidationResult(int RemovedEntryCount)
{
    /// <summary>
    /// Gets or sets removed entry count.
    /// </summary>
    public int RemovedEntryCount { get; init; } = RemovedEntryCount >= 0
        ? RemovedEntryCount
        : throw new ArgumentOutOfRangeException(nameof(RemovedEntryCount));
}

/// <summary>
/// Defines the contract for idata cache invalidator.
/// </summary>
public interface IDataCacheInvalidator
{
    /// <summary>
    /// Executes the invalidate async operation.
    /// </summary>
    ValueTask<DataCacheInvalidationResult> InvalidateAsync(
        DataCacheInvalidation invalidation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for idata expiring request cache.
/// </summary>
public interface IDataExpiringRequestCache : IDataRequestCache
{
    /// <summary>
    /// Executes the set async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask SetAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        CancellationToken cancellationToken = default);
}

internal interface IDataCacheMutationGuard
{
    long CaptureMutationEpoch();

    ValueTask<bool> TrySetIfUnchangedAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        long expectedEpoch,
        CancellationToken cancellationToken = default);
}
