namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data result status values.
/// </summary>
public enum DataResultStatus
{
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents the partial value.
    /// </summary>
    Partial,
    /// <summary>
    /// Represents the stale suppressed value.
    /// </summary>
    StaleSuppressed,
}
