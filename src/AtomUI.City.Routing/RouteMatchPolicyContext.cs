namespace AtomUI.City.Routing;

/// <summary>
/// Represents route match policy context.
/// </summary>
public sealed class RouteMatchPolicyContext
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteMatchPolicyContext</c> type.
    /// </summary>
    public RouteMatchPolicyContext(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        NavigationSnapshot currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(currentSnapshot);

        NavigationId = navigationId;
        Target = target;
        Route = route;
        CurrentSnapshot = currentSnapshot;
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
}
