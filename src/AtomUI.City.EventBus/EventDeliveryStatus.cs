namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event delivery status values.
/// </summary>
public enum EventDeliveryStatus
{
    /// <summary>
    /// Represents the succeeded value.
    /// </summary>
    Succeeded = 0,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed = 1,
    /// <summary>
    /// Represents the canceled value.
    /// </summary>
    Canceled = 2,
    /// <summary>
    /// Represents the timed out value.
    /// </summary>
    TimedOut = 3,
    /// <summary>
    /// Represents the skipped value.
    /// </summary>
    Skipped = 4,
}
