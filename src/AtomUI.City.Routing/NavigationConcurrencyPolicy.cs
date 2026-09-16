namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported navigation concurrency policy values.
/// </summary>
public enum NavigationConcurrencyPolicy
{
    /// <summary>
    /// Represents the cancel previous value.
    /// </summary>
    CancelPrevious,
    /// <summary>
    /// Represents the queue value.
    /// </summary>
    Queue,
    /// <summary>
    /// Represents the reject if busy value.
    /// </summary>
    RejectIfBusy,
}
