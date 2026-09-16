namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the contract for ievent contract registry.
/// </summary>
public interface IEventContractRegistry
{
    /// <summary>
    /// Gets a value indicating whether is frozen.
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Gets descriptors.
    /// </summary>
    IReadOnlyList<EventContractDescriptor> Descriptors { get; }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(EventContractDescriptor descriptor);

    /// <summary>
    /// Executes the freeze operation.
    /// </summary>
    void Freeze();

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    bool TryGet(EventContractId contractId, out EventContractDescriptor? descriptor);

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    bool TryGet(Type eventType, out EventContractDescriptor? descriptor);

    /// <summary>
    /// Executes the get or create&lt;tevent&gt; operation.
    /// </summary>
    EventContractDescriptor GetOrCreate<TEvent>();
}
