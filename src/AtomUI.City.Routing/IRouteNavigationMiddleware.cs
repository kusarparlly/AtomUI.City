namespace AtomUI.City.Routing;

/// <summary>
/// Represents the operation used to route navigation delegate.
/// </summary>
public delegate ValueTask<NavigationResult> RouteNavigationDelegate();

/// <summary>
/// Defines the contract for iroute navigation middleware.
/// </summary>
public interface IRouteNavigationMiddleware
{
    /// <summary>
    /// Executes the invoke async operation.
    /// </summary>
    ValueTask<NavigationResult> InvokeAsync(
        RouteNavigationMiddlewareContext context,
        RouteNavigationDelegate next,
        CancellationToken cancellationToken);
}
