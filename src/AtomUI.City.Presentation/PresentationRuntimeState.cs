namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported presentation runtime state values.
/// </summary>
public enum PresentationRuntimeState
{
    /// <summary>
    /// Represents the not ready value.
    /// </summary>
    NotReady,
    /// <summary>
    /// Represents the ready value.
    /// </summary>
    Ready,
    /// <summary>
    /// Represents the stopping value.
    /// </summary>
    Stopping,
    /// <summary>
    /// Represents the stopped value.
    /// </summary>
    Stopped,
    /// <summary>
    /// Represents the faulted value.
    /// </summary>
    Faulted,
}
