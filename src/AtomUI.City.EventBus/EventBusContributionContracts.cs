namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event plugin plane values.
/// </summary>
public enum EventPluginPlane
{
    /// <summary>
    /// Represents the shared value.
    /// </summary>
    Shared = 0,
    /// <summary>
    /// Represents the private value.
    /// </summary>
    Private = 1,
}

/// <summary>
/// Defines the supported event plugin access values.
/// </summary>
[Flags]
public enum EventPluginAccess
{
    /// <summary>
    /// Represents the none value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the publish value.
    /// </summary>
    Publish = 1,
    /// <summary>
    /// Represents the subscribe value.
    /// </summary>
    Subscribe = 2,
}

/// <summary>
/// Defines the supported event bus contribution state values.
/// </summary>
public enum EventBusContributionState
{
    /// <summary>
    /// Represents the activating value.
    /// </summary>
    Activating = 0,
    /// <summary>
    /// Represents the active value.
    /// </summary>
    Active = 1,
    /// <summary>
    /// Represents the quiescing value.
    /// </summary>
    Quiescing = 2,
    /// <summary>
    /// Represents the draining value.
    /// </summary>
    Draining = 3,
    /// <summary>
    /// Represents the disposed value.
    /// </summary>
    Disposed = 4,
    /// <summary>
    /// Represents the faulted value.
    /// </summary>
    Faulted = 5,
}

/// <summary>
/// Represents event plugin access rule.
/// </summary>
public sealed record EventPluginAccessRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPluginAccessRule"/> type.
    /// </summary>
    public EventPluginAccessRule(EventContractId contractId, string channelName, EventPluginAccess access,
        int minimumSchemaVersion = 1, int maximumSchemaVersion = int.MaxValue)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ContractId = contractId;
        ChannelName = EventAttributeValidation.ValidateName(channelName, nameof(channelName));
        if (access == EventPluginAccess.None || (access & ~(EventPluginAccess.Publish | EventPluginAccess.Subscribe)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "Plugin event access must declare Publish, Subscribe, or both.");
        }
        Access = access;
        if (minimumSchemaVersion <= 0 || maximumSchemaVersion < minimumSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(minimumSchemaVersion), "Plugin schema version range is invalid.");
        MinimumSchemaVersion = minimumSchemaVersion;
        MaximumSchemaVersion = maximumSchemaVersion;
    }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }
    /// <summary>
    /// Gets channel name.
    /// </summary>
    public string ChannelName { get; }
    /// <summary>
    /// Gets access.
    /// </summary>
    public EventPluginAccess Access { get; }
    /// <summary>
    /// Gets minimum schema version.
    /// </summary>
    public int MinimumSchemaVersion { get; }
    /// <summary>
    /// Gets maximum schema version.
    /// </summary>
    public int MaximumSchemaVersion { get; }
}

/// <summary>
/// Represents event plugin quotas.
/// </summary>
public sealed class EventPluginQuotas
{
    private static readonly TimeSpan MaximumDrainTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventPluginQuotas Default { get; } = new();

    /// <summary>
    /// Gets or sets maximum shared access rules.
    /// </summary>
    public int MaximumSharedAccessRules { get; init; } = 128;
    /// <summary>
    /// Gets or sets maximum private contracts.
    /// </summary>
    public int MaximumPrivateContracts { get; init; } = 128;
    /// <summary>
    /// Gets or sets maximum subscriptions.
    /// </summary>
    public int MaximumSubscriptions { get; init; } = 256;
    /// <summary>
    /// Gets or sets maximum private channel runtimes.
    /// </summary>
    public int MaximumPrivateChannelRuntimes { get; init; } = 64;
    /// <summary>
    /// Gets or sets drain timeout.
    /// </summary>
    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        ValidatePositive(MaximumSharedAccessRules, nameof(MaximumSharedAccessRules));
        ValidatePositive(MaximumPrivateContracts, nameof(MaximumPrivateContracts));
        ValidatePositive(MaximumSubscriptions, nameof(MaximumSubscriptions));
        if (MaximumPrivateChannelRuntimes is <= 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(MaximumPrivateChannelRuntimes));
        if (DrainTimeout <= TimeSpan.Zero || DrainTimeout > MaximumDrainTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DrainTimeout),
                DrainTimeout,
                $"Plugin EventBus drain timeout must be greater than zero and no greater than {MaximumDrainTimeout}.");
        }
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Plugin event quota must be greater than zero.");
    }
}

/// <summary>
/// Represents event bus contribution request.
/// </summary>
public sealed class EventBusContributionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventBusContributionRequest"/> type.
    /// </summary>
    public EventBusContributionRequest(
        string pluginId,
        IReadOnlyList<EventPluginAccessRule>? sharedAccess = null,
        IReadOnlyList<EventContractDescriptor>? privateContracts = null,
        EventPluginQuotas? quotas = null)
    {
        PluginId = EventAttributeValidation.ValidateName(pluginId, nameof(pluginId));
        SharedAccess = Array.AsReadOnly((sharedAccess ?? [])
            .Select(rule => rule ?? throw new ArgumentException("Shared access rules cannot contain null.", nameof(sharedAccess)))
            .ToArray());
        PrivateContracts = Array.AsReadOnly((privateContracts ?? [])
            .Select(descriptor => descriptor ?? throw new ArgumentException("Private contracts cannot contain null.", nameof(privateContracts)))
            .ToArray());
        Quotas = quotas ?? EventPluginQuotas.Default;
        Quotas.Validate();

        if (SharedAccess.Count > Quotas.MaximumSharedAccessRules)
            throw new ArgumentException("Shared access rule count exceeds the plugin quota.", nameof(sharedAccess));
        if (PrivateContracts.Count > Quotas.MaximumPrivateContracts)
            throw new ArgumentException("Private contract count exceeds the plugin quota.", nameof(privateContracts));
        if (SharedAccess.GroupBy(rule => (rule.ContractId, rule.ChannelName)).Any(group => group.Count() > 1))
            throw new ArgumentException("Shared access rules cannot contain duplicate contract/channel identities.", nameof(sharedAccess));
        if (PrivateContracts.Any(descriptor => descriptor.Plane != EventContractPlane.PluginPrivate))
            throw new ArgumentException("Private contract collection can contain only PluginPrivate descriptors.", nameof(privateContracts));
        if (PrivateContracts.GroupBy(descriptor => descriptor.ContractId).Any(group => group.Count() > 1) ||
            PrivateContracts.GroupBy(descriptor => descriptor.EventType).Any(group => group.Count() > 1))
            throw new ArgumentException("Private contract ids and event types must be unique.", nameof(privateContracts));
        var privateLoadContexts = PrivateContracts
            .Select(descriptor => System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(descriptor.Assembly))
            .Distinct(ReferenceEqualityComparer.Instance)
            .ToArray();
        if (privateLoadContexts.Length > 1)
            throw new ArgumentException("All private event contracts in one contribution must come from the same plugin AssemblyLoadContext.", nameof(privateContracts));
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }
    /// <summary>
    /// Gets shared access.
    /// </summary>
    public IReadOnlyList<EventPluginAccessRule> SharedAccess { get; }
    /// <summary>
    /// Gets private contracts.
    /// </summary>
    public IReadOnlyList<EventContractDescriptor> PrivateContracts { get; }
    /// <summary>
    /// Gets quotas.
    /// </summary>
    public EventPluginQuotas Quotas { get; }
}

/// <summary>
/// Defines the contract for ievent bus contribution controller.
/// </summary>
public interface IEventBusContributionController
{
    /// <summary>
    /// Executes the create async operation.
    /// </summary>
    ValueTask<IEventBusContributionLease> CreateAsync(
        EventBusContributionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for ievent bus contribution lease.
/// </summary>
public interface IEventBusContributionLease : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets plugin id.
    /// </summary>
    string PluginId { get; }
    /// <summary>
    /// Gets state.
    /// </summary>
    EventBusContributionState State { get; }
    /// <summary>
    /// Gets publisher.
    /// </summary>
    IPluginEventPublisher Publisher { get; }
    /// <summary>
    /// Gets subscriber.
    /// </summary>
    IPluginEventSubscriber Subscriber { get; }
    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for iplugin event publisher.
/// </summary>
public interface IPluginEventPublisher
{
    /// <summary>
    /// Executes the publish async&lt;tevent&gt; operation.
    /// </summary>
    ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the publish async&lt;tevent&gt; operation.
    /// </summary>
    ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the post async&lt;tevent&gt; operation.
    /// </summary>
    ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the post async&lt;tevent&gt; operation.
    /// </summary>
    ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for iplugin event subscriber.
/// </summary>
public interface IPluginEventSubscriber
{
    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(EventPluginPlane plane,
        Func<EventContext<TEvent>, ValueTask> handler, EventSubscriptionOptions? options = null);
    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, ValueTask> handler, EventSubscriptionOptions? options = null);
}
