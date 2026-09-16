namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported route guard result status values.
/// </summary>
public enum RouteGuardResultStatus
{
    /// <summary>
    /// Represents the allow value.
    /// </summary>
    Allow,
    /// <summary>
    /// Represents the reject value.
    /// </summary>
    Reject,
    /// <summary>
    /// Represents the redirect value.
    /// </summary>
    Redirect,
    /// <summary>
    /// Represents the cancel value.
    /// </summary>
    Cancel,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
}
