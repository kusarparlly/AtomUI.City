namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event channel execution mode values.
/// </summary>
public enum EventChannelExecutionMode
{
    /// <summary>
    /// Represents the serialized value.
    /// </summary>
    Serialized = 0,
    /// <summary>
    /// Represents the partitioned value.
    /// </summary>
    Partitioned = 1,
    /// <summary>
    /// Represents the concurrent value.
    /// </summary>
    Concurrent = 2
}
