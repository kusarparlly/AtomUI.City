namespace AtomUI.City.Routing;

/// <summary>
/// Represents route resolve context.
/// </summary>
public sealed class RouteResolveContext
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteResolveContext</c> type.
    /// </summary>
    public RouteResolveContext(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        NavigationSnapshot currentSnapshot,
        IReadOnlyDictionary<string, string> parameters)
    {
        NavigationId = navigationId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        CurrentSnapshot = currentSnapshot ?? throw new ArgumentNullException(nameof(currentSnapshot));
        Parameters = RouteParameters.Copy(parameters ?? throw new ArgumentNullException(nameof(parameters)));
    }

    /// <summary>
    /// Gets navigation id.
    /// </summary>
    public Guid NavigationId { get; }

    /// <summary>
    /// Gets target.
    /// </summary>
    public NavigationTarget Target { get; }

    /// <summary>
    /// Gets route.
    /// </summary>
    public RouteDescriptor Route { get; }

    /// <summary>
    /// Gets current snapshot.
    /// </summary>
    public NavigationSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
