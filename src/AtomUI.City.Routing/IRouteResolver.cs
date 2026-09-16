namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute resolver.
/// </summary>
public interface IRouteResolver
{
    /// <summary>
    /// Executes the resolve async operation.
    /// </summary>
    ValueTask<RouteResolveResult> ResolveAsync(
        RouteResolveContext context,
        CancellationToken cancellationToken);
}
