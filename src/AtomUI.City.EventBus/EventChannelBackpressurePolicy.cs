namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event channel backpressure policy values.
/// </summary>
public enum EventChannelBackpressurePolicy
{
    /// <summary>
    /// Represents the wait value.
    /// </summary>
    Wait = 0,
    /// <summary>
    /// Represents the reject value.
    /// </summary>
    Reject = 1,
    /// <summary>
    /// Represents the drop oldest value.
    /// </summary>
    DropOldest = 2,
    /// <summary>
    /// Represents the drop newest value.
    /// </summary>
    DropNewest = 3,
    /// <summary>
    /// Represents the coalesce latest value.
    /// </summary>
    CoalesceLatest = 4
}
