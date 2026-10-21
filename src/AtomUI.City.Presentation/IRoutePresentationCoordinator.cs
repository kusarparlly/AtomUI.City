using AtomUI.City.Routing;

namespace AtomUI.City.Presentation;

/// <summary>
/// Coordinates Router commit, application plan creation and RouteOutlet commit.
/// </summary>
public interface IRoutePresentationCoordinator
{
    /// <summary>
    /// Executes one serialized Routing-Presentation operation for an outlet.
    /// </summary>
    ValueTask<RoutePresentationResult> NavigateAsync(
        IRouter router,
        IRouteOutlet outlet,
        RoutePresentationNavigation navigation,
        IRoutePresentationPlanFactory planFactory,
        CancellationToken cancellationToken = default);
}
