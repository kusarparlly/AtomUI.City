namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute match policy.
/// </summary>
public interface IRouteMatchPolicy
{
    /// <summary>
    /// Executes the can match async operation.
    /// </summary>
    ValueTask<bool> CanMatchAsync(
        RouteMatchPolicyContext context,
        CancellationToken cancellationToken);
}
