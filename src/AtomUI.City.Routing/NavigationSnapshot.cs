namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation snapshot.
/// </summary>
public sealed class NavigationSnapshot
{
    private NavigationSnapshot(
        RouteDescriptor? activeRoute,
        IReadOnlyDictionary<string, string> parameters,
        long routeGraphVersion,
        string? reuseKey,
        IReadOnlyDictionary<string, object?>? resolvedData)
    {
        ActiveRoute = activeRoute;
        Parameters = RouteParameters.Copy(parameters);
        RouteGraphVersion = routeGraphVersion;
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        ResolvedData = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(resolvedData ?? new Dictionary<string, object?>(), StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets route.
    /// </summary>
    public RouteDescriptor Route => ActiveRoute ?? throw new InvalidOperationException("Navigation snapshot does not have an active route.");

    /// <summary>
    /// Gets active route.
    /// </summary>
    public RouteDescriptor? ActiveRoute { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets route graph version.
    /// </summary>
    public long RouteGraphVersion { get; }

    /// <summary>
    /// Gets reuse key.
    /// </summary>
    public string? ReuseKey { get; }

    /// <summary>
    /// Gets resolved data.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ResolvedData { get; }

    /// <summary>
    /// Executes the empty operation.
    /// </summary>
    public static NavigationSnapshot Empty(long routeGraphVersion)
    {
        return new NavigationSnapshot(
            activeRoute: null,
            RouteParameters.Empty(),
            routeGraphVersion,
            reuseKey: null,
            resolvedData: null);
    }

    /// <summary>
    /// Executes the from route operation.
    /// </summary>
    public static NavigationSnapshot FromRoute(
        RouteDescriptor activeRoute,
        IReadOnlyDictionary<string, string> parameters,
        long routeGraphVersion,
        string? reuseKey = null,
        IReadOnlyDictionary<string, object?>? resolvedData = null)
    {
        ArgumentNullException.ThrowIfNull(activeRoute);
        ArgumentNullException.ThrowIfNull(parameters);

        return new NavigationSnapshot(
            activeRoute,
            parameters,
            routeGraphVersion,
            reuseKey,
            resolvedData);
    }
}
