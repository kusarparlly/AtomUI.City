namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported route resolve result status values.
/// </summary>
public enum RouteResolveResultStatus
{
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success,
    /// <summary>
    /// Represents the not found value.
    /// </summary>
    NotFound,
    /// <summary>
    /// Represents the redirect value.
    /// </summary>
    Redirect,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
}
