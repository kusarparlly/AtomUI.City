namespace AtomUI.City.State;

/// <summary>
/// Represents state access denied exception.
/// </summary>
public sealed class StateAccessDeniedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <c>StateAccessDeniedException</c> type.
    /// </summary>
    public StateAccessDeniedException(string stateName)
        : base($"Write access to state '{stateName}' was denied.")
    {
        StateName = stateName;
    }

    /// <summary>
    /// Gets state name.
    /// </summary>
    public string StateName { get; }
}
