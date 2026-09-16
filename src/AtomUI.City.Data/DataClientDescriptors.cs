namespace AtomUI.City.Data;

/// <summary>
/// Represents data client.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class DataClientAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>DataClientAttribute</c> type.
    /// </summary>
    public DataClientAttribute(string clientId, DataTransportKind transportKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        ClientId = clientId;
        TransportKind = transportKind;
    }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets transport kind.
    /// </summary>
    public DataTransportKind TransportKind { get; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public string Version { get; init; } = "1";
}

/// <summary>
/// Represents data operation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DataOperationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>DataOperationAttribute</c> type.
    /// </summary>
    public DataOperationAttribute(
        string operationName,
        DataAccessMode accessMode = DataAccessMode.Query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        OperationName = operationName;
        AccessMode = accessMode;
    }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets access mode.
    /// </summary>
    public DataAccessMode AccessMode { get; }

    /// <summary>
    /// Gets or sets concurrency policy.
    /// </summary>
    public DataConcurrencyPolicy ConcurrencyPolicy { get; init; }

    /// <summary>
    /// Gets or sets timeout milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets max retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; init; }

    /// <summary>
    /// Gets or sets cache enabled.
    /// </summary>
    public bool CacheEnabled { get; init; }

    /// <summary>
    /// Gets or sets authentication policy.
    /// </summary>
    public string AuthenticationPolicy { get; init; } = "Anonymous";
}

/// <summary>
/// Represents data operation descriptor.
/// </summary>
public sealed class DataOperationDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>DataOperationDescriptor</c> type.
    /// </summary>
    public DataOperationDescriptor(
        string operationName,
        Type requestType,
        Type responseType,
        DataAccessMode accessMode,
        DataConcurrencyPolicy concurrencyPolicy = DataConcurrencyPolicy.AllowConcurrent,
        TimeSpan? timeout = null,
        int maxRetryAttempts = 0,
        bool cacheEnabled = false,
        string authenticationPolicy = "Anonymous")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        if (!Enum.IsDefined(concurrencyPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(concurrencyPolicy), concurrencyPolicy, "Data concurrency policy is not supported.");
        }

        if (timeout is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Operation timeout must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxRetryAttempts);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationPolicy);
        if (cacheEnabled && accessMode != DataAccessMode.Query)
        {
            throw new ArgumentException(
                "Data operation caching can be enabled only for query access mode.",
                nameof(cacheEnabled));
        }

        OperationName = operationName;
        RequestType = requestType;
        ResponseType = responseType;
        AccessMode = accessMode;
        ConcurrencyPolicy = concurrencyPolicy;
        Timeout = timeout;
        MaxRetryAttempts = maxRetryAttempts;
        CacheEnabled = cacheEnabled;
        AuthenticationPolicy = authenticationPolicy;
    }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets request type.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets response type.
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// Gets access mode.
    /// </summary>
    public DataAccessMode AccessMode { get; }

    /// <summary>
    /// Gets concurrency policy.
    /// </summary>
    public DataConcurrencyPolicy ConcurrencyPolicy { get; }

    /// <summary>
    /// Gets timeout.
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets max retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; }

    /// <summary>
    /// Gets cache enabled.
    /// </summary>
    public bool CacheEnabled { get; }

    /// <summary>
    /// Gets authentication policy.
    /// </summary>
    public string AuthenticationPolicy { get; }
}

/// <summary>
/// Represents data client descriptor.
/// </summary>
public sealed class DataClientDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>DataClientDescriptor</c> type.
    /// </summary>
    public DataClientDescriptor(
        string clientId,
        Type clientType,
        DataTransportKind transportKind,
        string version,
        IReadOnlyList<DataOperationDescriptor> operations,
        string? pluginContributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientType);
        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Any(static operation => operation is null))
        {
            throw new ArgumentException("Data operations cannot contain null values.", nameof(operations));
        }

        if (operations.GroupBy(static operation => operation.OperationName, StringComparer.Ordinal).Any(static group => group.Count() > 1))
        {
            throw new ArgumentException("Data operation names must be unique within a client descriptor.", nameof(operations));
        }

        if (pluginContributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginContributionId);
        }

        ClientId = clientId;
        ClientType = clientType;
        TransportKind = transportKind;
        Version = version;
        Operations = Array.AsReadOnly(operations.ToArray());
        PluginContributionId = pluginContributionId;
    }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets client type.
    /// </summary>
    public Type ClientType { get; }

    /// <summary>
    /// Gets transport kind.
    /// </summary>
    public DataTransportKind TransportKind { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets operations.
    /// </summary>
    public IReadOnlyList<DataOperationDescriptor> Operations { get; }

    /// <summary>
    /// Gets plugin contribution id.
    /// </summary>
    public string? PluginContributionId { get; }

    /// <summary>
    /// Gets with plugin contribution.
    /// </summary>
    public DataClientDescriptor WithPluginContribution(string contributionId) => new(
        ClientId,
        ClientType,
        TransportKind,
        Version,
        Operations,
        contributionId);
}

/// <summary>
/// Defines the contract for idata client descriptor registrar.
/// </summary>
public interface IDataClientDescriptorRegistrar
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(DataClientDescriptorCatalog catalog);
}

/// <summary>
/// Represents generated data client manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedDataClientManifestAttribute : Attribute
{
    /// <summary>
    /// Represents the current version value.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <c>GeneratedDataClientManifestAttribute</c> type.
    /// </summary>
    public GeneratedDataClientManifestAttribute(Type registrarType, int version = CurrentVersion)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
        if (version != CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Generated data client manifest version is not supported.");
        }

        Version = version;
    }

    /// <summary>
    /// Gets registrar type.
    /// </summary>
    public Type RegistrarType { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public int Version { get; }
}

/// <summary>
/// Represents data client descriptor catalog.
/// </summary>
public sealed class DataClientDescriptorCatalog
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Registration> _descriptors = new(StringComparer.Ordinal);
    private readonly AsyncLocal<GeneratedRegistrationTransaction?> _currentGeneratedRegistration = new();

    /// <summary>
    /// Represents the snapshot value.
    /// </summary>
    public IReadOnlyList<DataClientDescriptor> Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_descriptors.Values.Select(static value => value.Descriptor).ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    public IDisposable Register(DataClientDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var token = new object();
        lock (_syncRoot)
        {
            if (_descriptors.ContainsKey(descriptor.ClientId))
            {
                throw new InvalidOperationException($"Data client descriptor '{descriptor.ClientId}' is already registered.");
            }

            _descriptors.Add(descriptor.ClientId, new Registration(descriptor, token));
        }

        var lease = new DescriptorLease(this, descriptor.ClientId, token);
        _currentGeneratedRegistration.Value?.Track(lease);
        return lease;
    }

    /// <summary>
    /// Executes the register generated&lt;tregistrar&gt; operation.
    /// </summary>
    public void RegisterGenerated<TRegistrar>()
        where TRegistrar : IDataClientDescriptorRegistrar, new()
    {
        var parent = _currentGeneratedRegistration.Value;
        var transaction = new GeneratedRegistrationTransaction(parent);
        _currentGeneratedRegistration.Value = transaction;
        try
        {
            new TRegistrar().Register(this);
            transaction.Commit();
        }
        catch
        {
            transaction.RollBack();
            throw;
        }
        finally
        {
            _currentGeneratedRegistration.Value = parent;
        }
    }

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    public bool TryGet(string clientId, out DataClientDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        lock (_syncRoot)
        {
            if (_descriptors.TryGetValue(clientId, out var registration))
            {
                descriptor = registration.Descriptor;
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    private void Revoke(string clientId, object token)
    {
        lock (_syncRoot)
        {
            if (_descriptors.TryGetValue(clientId, out var registration)
                && ReferenceEquals(registration.Token, token))
            {
                _descriptors.Remove(clientId);
            }
        }
    }

    private sealed record Registration(DataClientDescriptor Descriptor, object Token);

    private sealed class GeneratedRegistrationTransaction(GeneratedRegistrationTransaction? parent)
    {
        private readonly List<DescriptorLease> _leases = [];

        public void Track(DescriptorLease lease) => _leases.Add(lease);

        public void Commit()
        {
            if (parent is not null)
            {
                foreach (var lease in _leases)
                {
                    parent.Track(lease);
                }
            }

            _leases.Clear();
        }

        public void RollBack()
        {
            for (var index = _leases.Count - 1; index >= 0; index--)
            {
                _leases[index].Dispose();
            }

            _leases.Clear();
        }
    }

    private sealed class DescriptorLease(
        DataClientDescriptorCatalog catalog,
        string clientId,
        object token) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                catalog.Revoke(clientId, token);
            }
        }
    }
}
