using System.Collections.Concurrent;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data request&lt;tresponse&gt;.
/// </summary>
public class DataRequest<TResponse>
{
    private DataAuthenticationOptions _authentication = DataAuthenticationOptions.Anonymous;
    private DataCacheOptions _cache = DataCacheOptions.Disabled;
    private DataResilienceOptions _resilience = DataResilienceOptions.None;
    private DataConcurrencyOptions _concurrency = DataConcurrencyOptions.AllowConcurrent;
    private DataConsistencyOptions _consistency = DataConsistencyOptions.None;
    private DataRequestOrigin _origin = DataRequestOrigin.Host;
    private string? _idempotencyKey;

    /// <summary>
    /// Executes the data request operation.
    /// </summary>
    public DataRequest(
        string clientId,
        string operationName,
        DataTransportKind transportKind,
        DataAccessMode accessMode = DataAccessMode.Query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        ClientId = clientId;
        OperationName = operationName;
        TransportKind = transportKind;
        AccessMode = accessMode;
    }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets transport kind.
    /// </summary>
    public DataTransportKind TransportKind { get; }

    /// <summary>
    /// Gets access mode.
    /// </summary>
    public DataAccessMode AccessMode { get; }

    /// <summary>
    /// Represents the authentication value.
    /// </summary>
    public DataAuthenticationOptions Authentication
    {
        get => _authentication;
        init => _authentication = value ?? throw new ArgumentNullException(nameof(Authentication));
    }

    /// <summary>
    /// Represents the cache value.
    /// </summary>
    public DataCacheOptions Cache
    {
        get => _cache;
        init => _cache = value ?? throw new ArgumentNullException(nameof(Cache));
    }

    /// <summary>
    /// Represents the resilience value.
    /// </summary>
    public DataResilienceOptions Resilience
    {
        get => _resilience;
        init => _resilience = value ?? throw new ArgumentNullException(nameof(Resilience));
    }

    /// <summary>
    /// Represents the concurrency value.
    /// </summary>
    public DataConcurrencyOptions Concurrency
    {
        get => _concurrency;
        init => _concurrency = value ?? throw new ArgumentNullException(nameof(Concurrency));
    }

    /// <summary>
    /// Represents the consistency value.
    /// </summary>
    public DataConsistencyOptions Consistency
    {
        get => _consistency;
        init => _consistency = value ?? throw new ArgumentNullException(nameof(Consistency));
    }

    /// <summary>
    /// Represents the origin value.
    /// </summary>
    public DataRequestOrigin Origin
    {
        get => _origin;
        init => _origin = value ?? throw new ArgumentNullException(nameof(Origin));
    }

    /// <summary>
    /// Gets or sets parent scope.
    /// </summary>
    public LifecycleScope? ParentScope { get; init; }

    /// <summary>
    /// Represents the idempotency key value.
    /// </summary>
    public string? IdempotencyKey
    {
        get => _idempotencyKey;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(IdempotencyKey));
            }

            _idempotencyKey = value;
        }
    }

    /// <summary>
    /// Gets items.
    /// </summary>
    public IDictionary<string, object?> Items { get; } =
        new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
}
