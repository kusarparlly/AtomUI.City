using AtomUI.City.Routing;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iroute authorization policy provider.
/// </summary>
public interface IRouteAuthorizationPolicyProvider
{
    /// <summary>
    /// Executes the get policy async operation.
    /// </summary>
    ValueTask<AuthorizationPolicy?> GetPolicyAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken = default);
}
