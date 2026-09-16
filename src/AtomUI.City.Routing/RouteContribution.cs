namespace AtomUI.City.Routing;

/// <summary>
/// Represents route contribution.
/// </summary>
public sealed class RouteContribution
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteContribution</c> type.
    /// </summary>
    public RouteContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes,
        Func<Type, object?>? serviceResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(routes);
        if (routes.Count == 0)
        {
            throw new ArgumentException("A route contribution must contain at least one route.", nameof(routes));
        }

        var ownedRoutes = new RouteDescriptor[routes.Count];
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            if (route is null)
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route contribution '{contributionId}' contains a null route descriptor.");
            }

            if (route.ContributionId is not null &&
                !string.Equals(route.ContributionId, contributionId, StringComparison.Ordinal))
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route '{route.RouteId}' belongs to contribution '{route.ContributionId}', not '{contributionId}'.");
            }

            ownedRoutes[index] = route.ContributionId is null
                ? route.WithContributionId(contributionId)
                : route;
        }

        ContributionId = contributionId;
        Routes = Array.AsReadOnly(ownedRoutes);
        ServiceResolver = serviceResolver;
    }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string ContributionId { get; }
    /// <summary>
    /// Gets routes.
    /// </summary>
    public IReadOnlyList<RouteDescriptor> Routes { get; }
    /// <summary>
    /// Gets service resolver.
    /// </summary>
    public Func<Type, object?>? ServiceResolver { get; }
}
