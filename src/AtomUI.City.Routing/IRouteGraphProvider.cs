namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute graph provider.
/// </summary>
public interface IRouteGraphProvider
{
    /// <summary>
    /// Gets current snapshot.
    /// </summary>
    RouteGraphSnapshot CurrentSnapshot { get; }
}
