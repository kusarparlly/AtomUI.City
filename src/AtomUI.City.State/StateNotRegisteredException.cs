namespace AtomUI.City.State;

/// <summary>
/// Represents state not registered exception.
/// </summary>
public sealed class StateNotRegisteredException : KeyNotFoundException
{
    /// <summary>
    /// Initializes a new instance of the <c>StateNotRegisteredException</c> type.
    /// </summary>
    public StateNotRegisteredException(string stateName)
        : base($"State '{stateName}' is not registered.")
    {
        StateName = stateName;
    }

    /// <summary>
    /// Gets state name.
    /// </summary>
    public string StateName { get; }
}
