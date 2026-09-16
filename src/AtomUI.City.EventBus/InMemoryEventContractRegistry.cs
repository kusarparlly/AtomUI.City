namespace AtomUI.City.EventBus;

/// <summary>
/// Represents in memory event contract registry.
/// </summary>
public sealed class InMemoryEventContractRegistry : IEventContractRegistry
{
    private readonly Dictionary<EventContractId, EventContractDescriptor> _byContractId = [];
    private readonly Dictionary<Type, EventContractDescriptor> _byEventType = [];
    private readonly object _syncRoot = new();
    private IReadOnlyList<EventContractDescriptor> _descriptors = Array.Empty<EventContractDescriptor>();
    private bool _isFrozen;

    /// <summary>
    /// Represents the is frozen value.
    /// </summary>
    public bool IsFrozen
    {
        get
        {
            lock (_syncRoot)
            {
                return _isFrozen;
            }
        }
    }

    /// <summary>
    /// Represents the descriptors value.
    /// </summary>
    public IReadOnlyList<EventContractDescriptor> Descriptors
    {
        get
        {
            lock (_syncRoot)
            {
                return _isFrozen
                    ? _descriptors
                    : CreateSnapshot();
            }
        }
    }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    public void Register(EventContractDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_syncRoot)
        {
            ThrowIfFrozen();
            RegisterCore(descriptor);
        }
    }

    /// <summary>
    /// Executes the freeze operation.
    /// </summary>
    public void Freeze()
    {
        lock (_syncRoot)
        {
            if (_isFrozen)
            {
                return;
            }

            _descriptors = CreateSnapshot();
            _isFrozen = true;
        }
    }

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    public bool TryGet(EventContractId contractId, out EventContractDescriptor? descriptor)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));

        lock (_syncRoot)
        {
            return _byContractId.TryGetValue(contractId, out descriptor);
        }
    }

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    public bool TryGet(Type eventType, out EventContractDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        lock (_syncRoot)
        {
            return _byEventType.TryGetValue(eventType, out descriptor);
        }
    }

    /// <summary>
    /// Executes the get or create&lt;tevent&gt; operation.
    /// </summary>
    public EventContractDescriptor GetOrCreate<TEvent>()
    {
        var eventType = typeof(TEvent);

        lock (_syncRoot)
        {
            if (_byEventType.TryGetValue(eventType, out var descriptor))
            {
                return descriptor;
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException(
                    $"Event contract registry is frozen and does not contain event type '{eventType.FullName}'.");
            }

            descriptor = EventContractDescriptor.DefaultShared<TEvent>();
            RegisterCore(descriptor);

            return descriptor;
        }
    }

    private void RegisterCore(EventContractDescriptor descriptor)
    {
        if (descriptor.Plane != EventContractPlane.Shared)
        {
            throw new InvalidOperationException(
                $"Shared event contract registry cannot register plugin-private contract '{descriptor.ContractId.Value}'.");
        }

        if (_byContractId.TryGetValue(descriptor.ContractId, out var existingContract))
        {
            throw new InvalidOperationException(
                $"Event contract id '{descriptor.ContractId.Value}' is already registered for '{existingContract.EventType.FullName}'.");
        }

        if (_byEventType.TryGetValue(descriptor.EventType, out var existingType))
        {
            throw new InvalidOperationException(
                $"Event type '{descriptor.EventType.FullName}' is already registered as '{existingType.ContractId.Value}'.");
        }

        _byContractId[descriptor.ContractId] = descriptor;
        _byEventType[descriptor.EventType] = descriptor;
    }

    private IReadOnlyList<EventContractDescriptor> CreateSnapshot()
    {
        var descriptors = _byContractId.Values
            .OrderBy(descriptor => descriptor.ContractId.Value, StringComparer.Ordinal)
            .ToArray();

        return Array.AsReadOnly(descriptors);
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Event contract registry is frozen.");
        }
    }
}
