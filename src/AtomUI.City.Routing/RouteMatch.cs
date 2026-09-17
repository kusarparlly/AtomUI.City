namespace AtomUI.City.Routing;

/// <summary>
/// Represents route match.
/// </summary>
public sealed class RouteMatch
{
    private RouteMatch(
        RouteMatchStatus status,
        RouteDescriptor? route,
        IReadOnlyDictionary<string, string> parameters,
        string? unmatchedPath)
    {
        Status = status;
        MatchedRoute = route;
        Parameters = RouteParameters.Copy(parameters);
        UnmatchedPath = unmatchedPath;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public RouteMatchStatus Status { get; }

    /// <summary>
    /// Gets route.
    /// </summary>
    public RouteDescriptor Route => MatchedRoute ?? throw new InvalidOperationException("No route was matched.");

    /// <summary>
    /// Gets matched route.
    /// </summary>
    public RouteDescriptor? MatchedRoute { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets unmatched path.
    /// </summary>
    public string? UnmatchedPath { get; }

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    internal static RouteMatch Success(
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(parameters);

        return new RouteMatch(RouteMatchStatus.Success, route, parameters, unmatchedPath: null);
    }

    /// <summary>
    /// Executes the not found operation.
    /// </summary>
    internal static RouteMatch NotFound(string path)
    {
        return new RouteMatch(RouteMatchStatus.NotFound, null, RouteParameters.Empty(), path);
    }
}
