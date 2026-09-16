namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported navigation result status values.
/// </summary>
public enum NavigationResultStatus
{
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success,
    /// <summary>
    /// Represents the rejected value.
    /// </summary>
    Rejected,
    /// <summary>
    /// Represents the redirected value.
    /// </summary>
    Redirected,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents the not found value.
    /// </summary>
    NotFound,
}
