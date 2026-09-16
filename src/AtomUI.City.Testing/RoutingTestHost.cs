namespace AtomUI.City.Testing;

using AtomUI.City.Routing;

/// <summary>
/// Represents routing test host.
/// </summary>
public sealed class RoutingTestHost
{
    private readonly RouteGraphSnapshot _graph;
    private readonly NavigationScope _navigationScope;
    private readonly IReadOnlyDictionary<string, RouteTestDefinition> _routesByName;

    internal RoutingTestHost(IReadOnlyList<RouteTestDefinition> routes)
    {
        Routes = Array.AsReadOnly(routes.ToArray());
        Diagnostics = new TestDiagnostics();
        _graph = RouteGraphSnapshot.Create(routes.Select(ToDescriptor).ToArray());
        _routesByName = routes.ToDictionary(route => route.Name, StringComparer.Ordinal);
        _navigationScope = new NavigationScope(_graph);
    }

    /// <summary>
    /// Gets routes.
    /// </summary>
    public IReadOnlyList<RouteTestDefinition> Routes { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public TestDiagnostics Diagnostics { get; }

    /// <summary>
    /// Executes the create builder operation.
    /// </summary>
    public static RoutingTestHostBuilder CreateBuilder()
    {
        return new RoutingTestHostBuilder();
    }

    /// <summary>
    /// Executes the match operation.
    /// </summary>
    public RouteTestMatch Match(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var match = _graph.Matcher.Match(path);
        if (match.Status == RouteMatchStatus.Success && match.MatchedRoute is not null)
        {
            return RouteTestMatch.Success(_routesByName[match.Route.RouteId], match.Parameters);
        }

        Diagnostics.Add("AUCTEST501", $"Route not found for path '{path}'.");

        return RouteTestMatch.NotFound();
    }

    /// <summary>
    /// Executes the navigate async operation.
    /// </summary>
    public async ValueTask<RouteTestMatch> NavigateAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _navigationScope.NavigateByPathAsync(path, cancellationToken: cancellationToken);
        if (result.Status == NavigationResultStatus.Success && result.ActiveRoute is not null)
        {
            return RouteTestMatch.Success(_routesByName[result.Route.RouteId], result.Parameters);
        }

        Diagnostics.Add("AUCTEST501", result.Error?.Message ?? $"Route not found for path '{path}'.");
        return RouteTestMatch.NotFound();
    }

    private static RouteDescriptor ToDescriptor(RouteTestDefinition route) =>
        new(
            route.Name,
            RouteDefinitionKind.Route,
            route.Pattern,
            new ViewModelTargetDescriptor(route.ViewModelType));
}
