using AtomUI.City.Routing;

namespace AtomUI.City.Presentation;

/// <summary>
/// Executes one Router navigation operation for a coordinated presentation transaction.
/// </summary>
public delegate ValueTask<NavigationResult> RoutePresentationNavigation(
    CancellationToken cancellationToken);
