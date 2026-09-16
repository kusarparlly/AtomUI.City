namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute enter guard.
/// </summary>
public interface IRouteEnterGuard
{
    /// <summary>
    /// Executes the can enter async operation.
    /// </summary>
    ValueTask<RouteGuardResult> CanEnterAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken);
}
