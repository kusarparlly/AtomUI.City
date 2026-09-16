namespace AtomUI.City.Routing;

/// <summary>
/// Represents route graph exception.
/// </summary>
public sealed class RouteGraphException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteGraphException</c> type.
    /// </summary>
    public RouteGraphException(RouteGraphError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>
    /// Gets error.
    /// </summary>
    public RouteGraphError Error { get; }
}
