namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute leave guard.
/// </summary>
public interface IRouteLeaveGuard
{
    /// <summary>
    /// Executes the can leave async operation.
    /// </summary>
    ValueTask<RouteGuardResult> CanLeaveAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken);
}
