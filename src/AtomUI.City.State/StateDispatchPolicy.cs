namespace AtomUI.City.State;

/// <summary>
/// Defines the supported state dispatch policy values.
/// </summary>
public enum StateDispatchPolicy
{
    /// <summary>
    /// Represents the immediate value.
    /// </summary>
    Immediate,
    /// <summary>
    /// Represents the queued value.
    /// </summary>
    Queued,
    /// <summary>
    /// Represents the dispatcher value.
    /// </summary>
    Dispatcher,
    /// <summary>
    /// Represents the background value.
    /// </summary>
    Background,
}
