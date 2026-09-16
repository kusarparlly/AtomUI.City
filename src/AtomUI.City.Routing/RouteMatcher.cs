namespace AtomUI.City.Routing;

/// <summary>
/// Represents route matcher.
/// </summary>
public sealed class RouteMatcher
{
    private readonly RouteGraphSnapshot _snapshot;
    private readonly IReadOnlyList<RouteMatcherEntry> _entries;

    internal RouteMatcher(RouteGraphSnapshot snapshot)
    {
        _snapshot = snapshot;
        _entries = snapshot
            .Routes
            .Where(route => route.Kind is RouteDefinitionKind.Route or RouteDefinitionKind.Index or RouteDefinitionKind.Layout or RouteDefinitionKind.Redirect)
            .Select(route =>
            {
                var template = RouteTemplate.Parse(_snapshot.GetFullTemplate(route));

                return new RouteMatcherEntry(route, template);
            })
            .OrderByDescending(entry => entry.Template.SpecificityScore())
            .ThenByDescending(entry => entry.Template.Segments.Count)
            .ThenByDescending(entry => entry.Route.MatchPolicyTypes.Count > 0)
            .ThenByDescending(entry => GetKindPriority(entry.Route.Kind))
            .ThenBy(entry => entry.Route.RouteId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Executes the match operation.
    /// </summary>
    public RouteMatch Match(string path)
    {
        return Match(path, "primary");
    }

    /// <summary>
    /// Executes the match operation.
    /// </summary>
    public RouteMatch Match(string path, string outletName)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (var match in MatchAll(path, outletName))
        {
            return match;
        }

        return RouteMatch.NotFound(path);
    }

    /// <summary>
    /// Executes the match all operation.
    /// </summary>
    public IReadOnlyList<RouteMatch> MatchAll(string path)
    {
        return MatchAll(path, "primary");
    }

    /// <summary>
    /// Executes the match all operation.
    /// </summary>
    public IReadOnlyList<RouteMatch> MatchAll(string path, string outletName)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(outletName);

        var matches = new List<RouteMatch>();

        foreach (var entry in _entries)
        {
            if (!string.Equals(entry.Route.OutletName, outletName, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.Template.TryMatch(path, out var values))
            {
                matches.Add(RouteMatch.Success(entry.Route, values));
            }
        }

        return Array.AsReadOnly(matches.ToArray());
    }

    private sealed record RouteMatcherEntry(RouteDescriptor Route, RouteTemplate Template);

    private static int GetKindPriority(RouteDefinitionKind kind)
    {
        return kind switch
        {
            RouteDefinitionKind.Redirect => 3,
            RouteDefinitionKind.Index => 2,
            RouteDefinitionKind.Route => 1,
            RouteDefinitionKind.Layout => 0,
            _ => -1,
        };
    }
}
