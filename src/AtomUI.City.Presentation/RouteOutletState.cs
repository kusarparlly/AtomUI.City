namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported route outlet state values.
/// </summary>
public enum RouteOutletState
{
    /// <summary>
    /// Represents the empty value.
    /// </summary>
    Empty,
    /// <summary>
    /// Represents the preparing value.
    /// </summary>
    Preparing,
    /// <summary>
    /// Represents the temporary attached value.
    /// </summary>
    TemporaryAttached,
    /// <summary>
    /// Represents the committed value.
    /// </summary>
    Committed,
    /// <summary>
    /// Represents the out of sync value.
    /// </summary>
    OutOfSync,
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
