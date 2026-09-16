namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for iroute registry.
/// </summary>
public interface IRouteRegistry : IRouteGraphProvider
{
    /// <summary>
    /// Executes the add contribution operation.
    /// </summary>
    RouteContributionLease AddContribution(RouteContribution contribution);

    /// <summary>
    /// Executes the add contribution operation.
    /// </summary>
    RouteContributionLease AddContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes);

    /// <summary>
    /// Executes the remove contribution operation.
    /// </summary>
    bool RemoveContribution(string contributionId);
}
