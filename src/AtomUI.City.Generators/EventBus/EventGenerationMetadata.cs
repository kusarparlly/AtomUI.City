using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.EventBus;

internal sealed class EventGenerationManifest
{
    public EventGenerationManifest(
        IReadOnlyList<GeneratedEventContractMetadata> contracts,
        IReadOnlyList<GeneratedEventHandlerMetadata> handlers)
    {
        Contracts = contracts;
        Handlers = handlers;
    }

    public IReadOnlyList<GeneratedEventContractMetadata> Contracts { get; }

    public IReadOnlyList<GeneratedEventHandlerMetadata> Handlers { get; }

    public bool IsEmpty => Contracts.Count == 0 && Handlers.Count == 0;
}

internal sealed class GeneratedEventContractMetadata
{
    public GeneratedEventContractMetadata(
        ITypeSymbol eventTypeSymbol,
        string ownerTypeName,
        string eventTypeName,
        string contractId,
        int schemaVersion,
        string schemaFingerprint,
        IReadOnlyList<GeneratedEventChannelMetadata> channels)
    {
        EventTypeSymbol = eventTypeSymbol;
        OwnerTypeName = ownerTypeName;
        EventTypeName = eventTypeName;
        ContractId = contractId;
        SchemaVersion = schemaVersion;
        SchemaFingerprint = schemaFingerprint;
        Channels = channels;
    }

    public ITypeSymbol EventTypeSymbol { get; }
    public string OwnerTypeName { get; }
    public string EventTypeName { get; }
    public string ContractId { get; }
    public int SchemaVersion { get; }
    public string SchemaFingerprint { get; }
    public IReadOnlyList<GeneratedEventChannelMetadata> Channels { get; }
}

internal sealed class GeneratedEventChannelMetadata
{
    public GeneratedEventChannelMetadata(string name, int capacity, int backpressurePolicy,
        int executionMode, int maximumConcurrency, int queueWaitTimeoutMilliseconds)
    {
        Name = name;
        Capacity = capacity;
        BackpressurePolicy = backpressurePolicy;
        ExecutionMode = executionMode;
        MaximumConcurrency = maximumConcurrency;
        QueueWaitTimeoutMilliseconds = queueWaitTimeoutMilliseconds;
    }

    public string Name { get; }
    public int Capacity { get; }
    public int BackpressurePolicy { get; }
    public int ExecutionMode { get; }
    public int MaximumConcurrency { get; }
    public int QueueWaitTimeoutMilliseconds { get; }
}

internal sealed class GeneratedEventHandlerMetadata
{
    public GeneratedEventHandlerMetadata(ITypeSymbol eventTypeSymbol, string ownerTypeName, string eventTypeName, string handlerTypeName,
        string channelName, int dispatchPolicy, int dispatchMode, int errorPolicy,
        int handlerTimeoutMilliseconds, int disableAfterFailures, IReadOnlyList<string> constructorParameterTypeNames)
    {
        EventTypeSymbol = eventTypeSymbol;
        OwnerTypeName = ownerTypeName;
        EventTypeName = eventTypeName;
        HandlerTypeName = handlerTypeName;
        ChannelName = channelName;
        DispatchPolicy = dispatchPolicy;
        DispatchMode = dispatchMode;
        ErrorPolicy = errorPolicy;
        HandlerTimeoutMilliseconds = handlerTimeoutMilliseconds;
        DisableAfterFailures = disableAfterFailures;
        ConstructorParameterTypeNames = constructorParameterTypeNames;
    }

    public ITypeSymbol EventTypeSymbol { get; }
    public string OwnerTypeName { get; }
    public string EventTypeName { get; }
    public string HandlerTypeName { get; }
    public string ChannelName { get; }
    public int DispatchPolicy { get; }
    public int DispatchMode { get; }
    public int ErrorPolicy { get; }
    public int HandlerTimeoutMilliseconds { get; }
    public int DisableAfterFailures { get; }
    public IReadOnlyList<string> ConstructorParameterTypeNames { get; }
}
