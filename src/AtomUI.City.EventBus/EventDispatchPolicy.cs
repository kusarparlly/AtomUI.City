namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event dispatch policy values.
/// </summary>
public enum EventDispatchPolicy
{
    /// <summary>
    /// Represents the current value.
    /// </summary>
    Current = 0,
    /// <summary>
    /// Represents the ui thread value.
    /// </summary>
    UiThread = 1,
    /// <summary>
    /// Represents the background value.
    /// </summary>
    Background = 2,
    /// <summary>
    /// Represents the serialized value.
    /// </summary>
    Serialized = 3,
}
