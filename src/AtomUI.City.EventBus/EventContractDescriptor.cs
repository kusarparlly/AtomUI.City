using System.Reflection;
using System.Runtime.Loader;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event contract descriptor.
/// </summary>
public sealed class EventContractDescriptor
{
    private EventContractDescriptor(
        EventContractId contractId,
        Type eventType,
        EventContractPlane plane,
        Assembly assembly,
        int schemaVersion,
        string schemaFingerprint,
        bool isGeneratedObjectGraphValidated)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(assembly);

        ContractId = contractId;
        EventType = eventType;
        Plane = plane;
        Assembly = assembly;
        SchemaVersion = schemaVersion > 0
            ? schemaVersion
            : throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Schema version must be greater than zero.");
        SchemaFingerprint = EventAttributeValidation.ValidateName(schemaFingerprint, nameof(schemaFingerprint));
        IsGeneratedObjectGraphValidated = isGeneratedObjectGraphValidated;
    }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }

    /// <summary>
    /// Gets event type.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// Gets plane.
    /// </summary>
    public EventContractPlane Plane { get; }

    /// <summary>
    /// Gets assembly.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets schema fingerprint.
    /// </summary>
    public string SchemaFingerprint { get; }

    internal bool IsGeneratedObjectGraphValidated { get; }

    /// <summary>
    /// Executes the shared&lt;tevent&gt; operation.
    /// </summary>
    public static EventContractDescriptor Shared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly)
    {
        var eventType = typeof(TEvent);
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            1,
            eventType.FullName ?? eventType.Name,
            isGeneratedObjectGraphValidated: false);
    }

    /// <summary>
    /// Executes the shared&lt;tevent&gt; operation.
    /// </summary>
    public static EventContractDescriptor Shared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint)
    {
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated: false);
    }

    /// <summary>
    /// Executes the generated shared&lt;tevent&gt; operation.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static EventContractDescriptor GeneratedShared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint)
    {
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated: true);
    }

    private static EventContractDescriptor CreateShared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint,
        bool isGeneratedObjectGraphValidated)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentNullException.ThrowIfNull(sharedAssembly);

        var eventType = typeof(TEvent);
        if (!ReferenceEquals(eventType.Assembly, sharedAssembly))
        {
            throw new InvalidOperationException(
                $"Shared event contract '{eventType.FullName}' must be defined by shared assembly '{sharedAssembly.GetName().Name}'.");
        }

        var loadContext = AssemblyLoadContext.GetLoadContext(sharedAssembly);
        if (!ReferenceEquals(loadContext, AssemblyLoadContext.Default))
        {
            throw new InvalidOperationException(
                $"Shared event contract '{eventType.FullName}' must be loaded by the default AssemblyLoadContext.");
        }

        return new EventContractDescriptor(
            contractId,
            eventType,
            EventContractPlane.Shared,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated);
    }

    /// <summary>
    /// Executes the plugin private&lt;tevent&gt; operation.
    /// </summary>
    public static EventContractDescriptor PluginPrivate<TEvent>(EventContractId contractId)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));

        var eventType = typeof(TEvent);
        var loadContext = AssemblyLoadContext.GetLoadContext(eventType.Assembly);
        if (loadContext is null ||
            ReferenceEquals(loadContext, AssemblyLoadContext.Default) ||
            !loadContext.IsCollectible)
        {
            throw new InvalidOperationException(
                $"Plugin-private event contract '{eventType.FullName}' must be loaded by a collectible non-default AssemblyLoadContext.");
        }

        return new EventContractDescriptor(
            contractId,
            eventType,
            EventContractPlane.PluginPrivate,
            eventType.Assembly,
            1,
            eventType.FullName ?? eventType.Name,
            isGeneratedObjectGraphValidated: false);
    }

    internal static EventContractDescriptor DefaultShared<TEvent>()
    {
        var eventType = typeof(TEvent);
        var contractName = eventType.FullName ?? eventType.Name;

        return Shared<TEvent>(
            new EventContractId(contractName),
            eventType.Assembly);
    }
}
