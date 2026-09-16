namespace AtomUI.City.Routing;

/// <summary>
/// Represents route graph snapshot.
/// </summary>
public sealed class RouteGraphSnapshot
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RouteDescriptor>> _childrenByParentId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RouteDescriptor>> _routesByContributionId;
    private readonly IReadOnlyDictionary<string, RouteDescriptor> _routesById;

    private RouteGraphSnapshot(
        long version,
        IReadOnlyList<RouteDescriptor> routes,
        IReadOnlyDictionary<string, RouteDescriptor> routesById,
        IReadOnlyDictionary<string, IReadOnlyList<RouteDescriptor>> childrenByParentId,
        IReadOnlyDictionary<string, IReadOnlyList<RouteDescriptor>> routesByContributionId)
    {
        Version = version;
        Routes = routes;
        _routesById = routesById;
        _childrenByParentId = childrenByParentId;
        _routesByContributionId = routesByContributionId;
        Matcher = new RouteMatcher(this);
    }

    /// <summary>
    /// Gets version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets routes.
    /// </summary>
    public IReadOnlyList<RouteDescriptor> Routes { get; }

    /// <summary>
    /// Gets matcher.
    /// </summary>
    public RouteMatcher Matcher { get; }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static RouteGraphSnapshot Create(IReadOnlyList<RouteDescriptor> routes)
    {
        return Create(routes, version: 1);
    }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static RouteGraphSnapshot Create(IReadOnlyList<RouteDescriptor> routes, long version)
    {
        ArgumentNullException.ThrowIfNull(routes);
        if (version < 1)
        {
            throw new RouteGraphException(
                RouteGraphError.InvalidVersion,
                "Route graph version must be greater than zero.");
        }

        if (routes.Any(route => route is null))
        {
            throw new RouteGraphException(
                RouteGraphError.InvalidRouteDefinition,
                "Route graph cannot contain a null route descriptor.");
        }

        routes = AttachExtensionPointRoutes(routes);
        var routesById = new Dictionary<string, RouteDescriptor>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            if (!routesById.TryAdd(route.RouteId, route))
            {
                throw new RouteGraphException(
                    RouteGraphError.DuplicateRouteId,
                    $"Route id '{route.RouteId}' is declared more than once.");
            }
        }

        foreach (var route in routes)
        {
            if (route.ParentRouteId is null || routesById.ContainsKey(route.ParentRouteId))
            {
                continue;
            }

            throw new RouteGraphException(
                RouteGraphError.MissingParentRoute,
                $"Route '{route.RouteId}' references missing parent route '{route.ParentRouteId}'.");
        }

        ValidateParentHierarchy(routes, routesById);
        ValidateRouteDefinitions(routes, routesById);
        ValidateTemplateConflicts(routes, routesById);

        var childrenByParentId = routes
            .Where(route => route.ParentRouteId is not null)
            .GroupBy(route => route.ParentRouteId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RouteDescriptor>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        var routesByContributionId = routes
            .Where(route => !string.IsNullOrWhiteSpace(route.ContributionId))
            .GroupBy(route => route.ContributionId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RouteDescriptor>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);

        return new RouteGraphSnapshot(
            version,
            Array.AsReadOnly(routes.ToArray()),
            routesById,
            childrenByParentId,
            routesByContributionId);
    }

    /// <summary>
    /// Executes the get required route operation.
    /// </summary>
    public RouteDescriptor GetRequiredRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);

        return _routesById.TryGetValue(routeId, out var route)
            ? route
            : throw new KeyNotFoundException($"Route '{routeId}' was not found.");
    }

    /// <summary>
    /// Executes the try get route operation.
    /// </summary>
    public bool TryGetRoute(string routeId, out RouteDescriptor? route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);

        return _routesById.TryGetValue(routeId, out route);
    }

    /// <summary>
    /// Executes the get children operation.
    /// </summary>
    public IReadOnlyList<RouteDescriptor> GetChildren(string parentRouteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRouteId);

        return _childrenByParentId.TryGetValue(parentRouteId, out var children)
            ? children
            : [];
    }

    /// <summary>
    /// Executes the get contribution routes operation.
    /// </summary>
    public IReadOnlyList<RouteDescriptor> GetContributionRoutes(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        return _routesByContributionId.TryGetValue(contributionId, out var routes)
            ? routes
            : [];
    }

    /// <summary>
    /// Executes the without contribution operation.
    /// </summary>
    public RouteGraphSnapshot WithoutContribution(string contributionId, long? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        var nextVersion = version ?? Version + 1;
        ValidateNextVersion(nextVersion);
        return Create(
            Routes
                .Where(route => !string.Equals(route.ContributionId, contributionId, StringComparison.Ordinal))
                .ToArray(),
            nextVersion);
    }

    /// <summary>
    /// Executes the with contribution operation.
    /// </summary>
    public RouteGraphSnapshot WithContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes,
        long? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(routes);
        if (routes.Count == 0)
        {
            throw new RouteGraphException(
                RouteGraphError.InvalidContribution,
                $"Route contribution '{contributionId}' must contain at least one route.");
        }

        foreach (var route in routes)
        {
            if (route is null)
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route contribution '{contributionId}' cannot contain a null route descriptor.");
            }

            if (!string.Equals(route.ContributionId, contributionId, StringComparison.Ordinal))
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route '{route.RouteId}' does not belong to contribution '{contributionId}'.");
            }
        }

        var nextVersion = version ?? Version + 1;
        ValidateNextVersion(nextVersion);
        return Create(
            Routes
                .Concat(routes)
                .ToArray(),
            nextVersion);
    }

    internal string GetFullTemplate(RouteDescriptor route)
    {
        var segments = new Stack<string>();

        for (var current = route; current is not null; current = current.ParentRouteId is null ? null : GetRequiredRoute(current.ParentRouteId))
        {
            if (current.Template is not null && current.Template.Pattern.Length > 0)
            {
                segments.Push(current.Template.Pattern);
            }
        }

        return string.Join('/', segments);
    }

    private static void ValidateTemplateConflicts(
        IReadOnlyList<RouteDescriptor> routes,
        IReadOnlyDictionary<string, RouteDescriptor> routesById)
    {
        var candidatesByTemplate = new Dictionary<
            (string OutletName, string TemplateSignature),
            List<RouteDescriptor>>();

        foreach (var route in routes)
        {
            if (route.Template is null)
            {
                continue;
            }

            var key = (
                route.OutletName,
                CreateTemplateSignature(RouteTemplate.Parse(GetFullTemplate(route, routesById))));
            if (!candidatesByTemplate.TryGetValue(key, out var candidates))
            {
                candidates = [];
                candidatesByTemplate.Add(key, candidates);
            }

            candidates.Add(route);
        }

        foreach (var candidates in candidatesByTemplate.Values)
        {
            var unconditionalCandidates = candidates
                .Where(candidate => candidate.MatchPolicyTypes.Count == 0)
                .ToArray();
            if (unconditionalCandidates.Length <= 1)
            {
                continue;
            }

            throw new RouteGraphException(
                RouteGraphError.DuplicateRouteTemplate,
                $"Route '{unconditionalCandidates[1].RouteId}' conflicts with route '{unconditionalCandidates[0].RouteId}' for effective template '{GetFullTemplate(unconditionalCandidates[0], routesById)}'.");
        }
    }

    private static void ValidateRouteDefinitions(
        IReadOnlyList<RouteDescriptor> routes,
        IReadOnlyDictionary<string, RouteDescriptor> routesById)
    {
        var indexKeys = new HashSet<(string ParentRouteId, string OutletName)>();
        var extensionPoints = new Dictionary<string, RouteDescriptor>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            ValidateRouteKind(route);
            ValidateBehaviorTypes(route);

            if (route.Kind == RouteDefinitionKind.Index)
            {
                if (route.ParentRouteId is null || route.Template is not null)
                {
                    throw InvalidDefinition(route, "Index routes require a parent and cannot declare a template.");
                }

                var indexKey = (route.ParentRouteId, route.OutletName);
                if (!indexKeys.Add(indexKey))
                {
                    throw new RouteGraphException(
                        RouteGraphError.DuplicateIndexRoute,
                        $"Parent route '{route.ParentRouteId}' declares more than one index route for outlet '{route.OutletName}'.");
                }
            }

            if (route.Kind == RouteDefinitionKind.Redirect)
            {
                if (route.Template is null || route.ViewModelTarget is not null)
                {
                    throw InvalidDefinition(route, "Redirect routes require a template and cannot declare a ViewModel target.");
                }

                if (string.IsNullOrWhiteSpace(route.RedirectTargetRouteId) ||
                    !routesById.TryGetValue(route.RedirectTargetRouteId, out var redirectTarget))
                {
                    throw new RouteGraphException(
                        RouteGraphError.MissingRedirectTarget,
                        $"Redirect route '{route.RouteId}' references missing target '{route.RedirectTargetRouteId}'.");
                }

                if (redirectTarget.Kind is RouteDefinitionKind.Group or RouteDefinitionKind.ExtensionPoint)
                {
                    throw InvalidDefinition(
                        route,
                        $"Redirect target '{redirectTarget.RouteId}' is not navigable.");
                }
            }

            if (route.Kind == RouteDefinitionKind.ExtensionPoint)
            {
                if (string.IsNullOrWhiteSpace(route.ExtensionPoint) || route.ViewModelTarget is not null)
                {
                    throw InvalidDefinition(route, "Extension points require an id and cannot declare a ViewModel target.");
                }

                if (!extensionPoints.TryAdd(route.ExtensionPoint, route))
                {
                    throw new RouteGraphException(
                        RouteGraphError.DuplicateExtensionPoint,
                        $"Route extension point '{route.ExtensionPoint}' is declared more than once.");
                }
            }
        }

        foreach (var route in routes)
        {
            if (route.Kind != RouteDefinitionKind.ExtensionPoint &&
                !string.IsNullOrWhiteSpace(route.ExtensionPoint) &&
                !extensionPoints.ContainsKey(route.ExtensionPoint))
            {
                throw new RouteGraphException(
                    RouteGraphError.MissingExtensionPoint,
                    $"Contributed route '{route.RouteId}' references missing extension point '{route.ExtensionPoint}'.");
            }


            if (!string.IsNullOrWhiteSpace(route.ContributionId) &&
                string.IsNullOrWhiteSpace(route.ExtensionPoint) &&
                route.ParentRouteId is { } parentRouteId &&
                !string.Equals(
                    routesById[parentRouteId].ContributionId,
                    route.ContributionId,
                    StringComparison.Ordinal))
            {
                throw new RouteGraphException(
                    RouteGraphError.MissingExtensionPoint,
                    $"Contributed route '{route.RouteId}' cannot attach directly to host route '{parentRouteId}'.");
            }
        }

        ValidateRedirectCycles(routesById);
    }

    private static IReadOnlyList<RouteDescriptor> AttachExtensionPointRoutes(
        IReadOnlyList<RouteDescriptor> routes)
    {
        var extensionPoints = routes
            .Where(route => route.Kind == RouteDefinitionKind.ExtensionPoint &&
                !string.IsNullOrWhiteSpace(route.ExtensionPoint))
            .GroupBy(route => route.ExtensionPoint!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return routes
            .Select(route =>
            {
                if (route.Kind == RouteDefinitionKind.ExtensionPoint ||
                    string.IsNullOrWhiteSpace(route.ExtensionPoint) ||
                    !extensionPoints.TryGetValue(route.ExtensionPoint, out var extensionPoint))
                {
                    return route;
                }

                if (route.ParentRouteId is not null &&
                    !string.Equals(route.ParentRouteId, extensionPoint.ParentRouteId, StringComparison.Ordinal))
                {
                    throw new RouteGraphException(
                        RouteGraphError.InvalidContribution,
                        $"Route '{route.RouteId}' parent does not match extension point '{route.ExtensionPoint}'.");
                }

                return route.WithParentRouteId(extensionPoint.ParentRouteId);
            })
            .ToArray();
    }

    private static void ValidateRouteKind(RouteDescriptor route)
    {
        switch (route.Kind)
        {
            case RouteDefinitionKind.Route when route.Template is null || route.ViewModelTarget is null:
                throw InvalidDefinition(route, "Routes require both a template and a ViewModel target.");
            case RouteDefinitionKind.Layout when route.ViewModelTarget is null:
                throw InvalidDefinition(route, "Layout routes require a ViewModel target.");
            case RouteDefinitionKind.Index when route.ViewModelTarget is null:
                throw InvalidDefinition(route, "Index routes require a ViewModel target.");
            case RouteDefinitionKind.Group when route.Template is null || route.ViewModelTarget is not null:
                throw InvalidDefinition(route, "Route groups require a template and cannot declare a ViewModel target.");
            case RouteDefinitionKind.ExtensionPoint when route.Template is not null:
                throw InvalidDefinition(route, "Extension points cannot declare a route template.");
            case RouteDefinitionKind.Route:
            case RouteDefinitionKind.Layout:
            case RouteDefinitionKind.Index:
            case RouteDefinitionKind.Group:
            case RouteDefinitionKind.Redirect:
            case RouteDefinitionKind.ExtensionPoint:
                break;
            default:
                throw InvalidDefinition(route, $"Route kind '{route.Kind}' is not supported.");
        }
    }

    private static void ValidateBehaviorTypes(RouteDescriptor route)
    {
        ValidateBehaviorTypes<IRouteEnterGuard>(route, route.EnterGuardTypes, "enter guard");
        ValidateBehaviorTypes<IRouteLeaveGuard>(route, route.LeaveGuardTypes, "leave guard");
        ValidateBehaviorTypes<IRouteMatchPolicy>(route, route.MatchPolicyTypes, "match policy");
        ValidateBehaviorTypes<IRouteResolver>(route, route.ResolverTypes, "resolver");
        ValidateBehaviorTypes<IRouteNavigationMiddleware>(route, route.MiddlewareTypes, "navigation middleware");
    }

    private static void ValidateBehaviorTypes<TContract>(
        RouteDescriptor route,
        IEnumerable<Type> types,
        string behaviorName)
    {
        foreach (var type in types)
        {
            if (type is null ||
                !typeof(TContract).IsAssignableFrom(type) ||
                !type.IsClass ||
                type.IsAbstract ||
                type.ContainsGenericParameters)
            {
                throw InvalidDefinition(
                    route,
                    $"Type '{type?.FullName ?? "<null>"}' is not a concrete {behaviorName} implementing '{typeof(TContract).FullName}'.");
            }
        }
    }

    private static void ValidateRedirectCycles(IReadOnlyDictionary<string, RouteDescriptor> routesById)
    {
        foreach (var route in routesById.Values.Where(route => route.Kind == RouteDefinitionKind.Redirect))
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = route;

            while (current.Kind == RouteDefinitionKind.Redirect)
            {
                if (!visited.Add(current.RouteId))
                {
                    throw new RouteGraphException(
                        RouteGraphError.CircularRedirect,
                        $"Static route redirect cycle detected at '{current.RouteId}'.");
                }

                current = routesById[current.RedirectTargetRouteId!];
            }
        }
    }

    private static RouteGraphException InvalidDefinition(RouteDescriptor route, string message)
    {
        return new RouteGraphException(
            RouteGraphError.InvalidRouteDefinition,
            $"Route '{route.RouteId}' is invalid. {message}");
    }

    private static string CreateTemplateSignature(RouteTemplate template)
    {
        return string.Join(
            "/",
            template.Segments.Select(segment => segment.Kind switch
            {
                RouteTemplateSegmentKind.Literal => "l:" + segment.Literal!.ToUpperInvariant(),
                RouteTemplateSegmentKind.CatchAll => "c:" + CreateConstraintSignature(segment.Constraints),
                _ => string.Join(
                    ":",
                    "p",
                    segment.IsOptional || segment.DefaultValue is not null ? "optional" : "required",
                    CreateConstraintSignature(segment.Constraints)),
            }));
    }

    private static string CreateConstraintSignature(IReadOnlyList<string> constraints)
    {
        return string.Join(",", constraints
            .Select(NormalizeConstraintSignature)
            .OrderBy(constraint => constraint, StringComparer.Ordinal));
    }

    private static string NormalizeConstraintSignature(string constraint)
    {
        var openParenthesis = constraint.IndexOf('(');
        if (openParenthesis < 0 || !constraint.EndsWith(')'))
        {
            return constraint.ToLowerInvariant();
        }

        return constraint[..openParenthesis].ToLowerInvariant() +
            "(" + constraint[(openParenthesis + 1)..^1].Trim() + ")";
    }

    private static string GetFullTemplate(
        RouteDescriptor route,
        IReadOnlyDictionary<string, RouteDescriptor> routesById)
    {
        var segments = new Stack<string>();
        for (var current = route;
             current is not null;
             current = current.ParentRouteId is null ? null : routesById[current.ParentRouteId])
        {
            if (current.Template is not null && current.Template.Pattern.Length > 0)
            {
                segments.Push(current.Template.Pattern);
            }
        }

        return string.Join('/', segments);
    }

    private void ValidateNextVersion(long version)
    {
        if (version <= Version)
        {
            throw new RouteGraphException(
                RouteGraphError.InvalidVersion,
                $"Route graph version '{version}' must be greater than current version '{Version}'.");
        }
    }

    private static void ValidateParentHierarchy(
        IReadOnlyList<RouteDescriptor> routes,
        IReadOnlyDictionary<string, RouteDescriptor> routesById)
    {
        var visitStates = new Dictionary<string, ParentVisitState>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            if (visitStates.TryGetValue(route.RouteId, out var existingState) &&
                existingState == ParentVisitState.Visited)
            {
                continue;
            }

            var chain = new List<RouteDescriptor>();
            RouteDescriptor? current = route;

            while (current is not null)
            {
                if (visitStates.TryGetValue(current.RouteId, out var state))
                {
                    if (state == ParentVisitState.Visiting)
                    {
                        var cycleStart = chain.FindIndex(item =>
                            string.Equals(item.RouteId, current.RouteId, StringComparison.Ordinal));
                        var cycle = chain
                            .Skip(cycleStart)
                            .Select(item => item.RouteId)
                            .Append(current.RouteId);

                        throw new RouteGraphException(
                            RouteGraphError.CircularParentRoute,
                            $"Route parent hierarchy contains a cycle: {string.Join(" -> ", cycle)}.");
                    }

                    break;
                }

                visitStates.Add(current.RouteId, ParentVisitState.Visiting);
                chain.Add(current);
                current = current.ParentRouteId is null
                    ? null
                    : routesById[current.ParentRouteId];
            }

            foreach (var item in chain)
            {
                visitStates[item.RouteId] = ParentVisitState.Visited;
            }
        }
    }

    private enum ParentVisitState
    {
        Visiting,
        Visited,
    }
}
