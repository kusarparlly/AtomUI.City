namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event subscription state values.
/// </summary>
public enum EventSubscriptionState
{
    /// <summary>
    /// Represents the created value.
    /// </summary>
    Created = 0,
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
