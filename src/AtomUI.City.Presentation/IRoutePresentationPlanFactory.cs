namespace AtomUI.City.Presentation;

/// <summary>
/// Creates an application-specific presentation plan for a committed route.
/// </summary>
public interface IRoutePresentationPlanFactory
{
    /// <summary>
    /// Creates a single-use route outlet plan.
    /// </summary>
    ValueTask<RouteOutletCommitPlan> CreateAsync(
        RoutePresentationPlanContext context,
        CancellationToken cancellationToken = default);
}
