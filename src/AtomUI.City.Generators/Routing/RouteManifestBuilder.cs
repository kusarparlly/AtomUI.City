using AtomUI.City.Generators.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AtomUI.City.Generators.Routing;

internal static class RouteManifestBuilder
{
    public static RouteManifestResult Build(IReadOnlyList<RouteDefinitionMetadata> routes)
    {
        if (routes is null)
        {
            throw new ArgumentNullException(nameof(routes));
        }

        var diagnostics = new List<GeneratorDiagnostic>();
        var routesById = new Dictionary<string, RouteDefinitionMetadata>(StringComparer.Ordinal);
        var routesByMethod = new Dictionary<string, RouteDefinitionMetadata>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            if (routesById.ContainsKey(route.Id))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.DuplicateRoute,
                    $"Route id '{route.Id}' is declared more than once.",
                    route.Id));
            }
            else
            {
                routesById.Add(route.Id, route);
            }

            var methodKey = CreateMethodKey(route.RouteMapTypeName, route.MethodName);

            if (!routesByMethod.ContainsKey(methodKey))
            {
                routesByMethod.Add(methodKey, route);
            }
        }

        var parentRouteIds = new Dictionary<string, string?>(StringComparer.Ordinal);
        var redirectTargetRouteIds = new Dictionary<string, string?>(StringComparer.Ordinal);

        ValidateRouteDefinitions(routes, diagnostics);

        foreach (var route in routes)
        {
            parentRouteIds[route.Id] = ResolveReferencedRouteId(route, route.ParentMethodName, routesByMethod, diagnostics, "parent");
            redirectTargetRouteIds[route.Id] = ResolveReferencedRouteId(route, route.RedirectTargetMethodName, routesByMethod, diagnostics, "redirect target");
        }

        ValidateResolvedRelationships(routes, parentRouteIds, redirectTargetRouteIds, diagnostics);
        DetectTemplateConflicts(routes, parentRouteIds, diagnostics);

        if (diagnostics.Count > 0)
        {
            return new RouteManifestResult(new RouteManifest([]), diagnostics);
        }

        var manifestRoutes = routes
            .Select(route => new RouteManifestRoute(
                route.Id,
                route.Kind,
                route.Template,
                route.ViewModelTypeName,
                parentRouteIds[route.Id],
                route.OutletName,
                route.ExtensionPoint,
                redirectTargetRouteIds[route.Id],
                route.TitleKey,
                route.DescriptionKey,
                route.BreadcrumbKey,
                route.GroupKey,
                route.ErrorTitleKey,
                route.EnterGuardTypeNames,
                route.LeaveGuardTypeNames,
                route.MatchPolicyTypeNames,
                route.ResolverTypeNames,
                route.MiddlewareTypeNames,
                route.ParameterBindings.Select(binding => binding.RouteName).ToArray(),
                route.ReuseKey,
                route.ActivationHint))
            .OrderBy(route => route.Id, StringComparer.Ordinal)
            .ToArray();

        return new RouteManifestResult(new RouteManifest(manifestRoutes), diagnostics);
    }

    private static void ValidateRouteDefinitions(
        IEnumerable<RouteDefinitionMetadata> routes,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var route in routes)
        {
            if (string.IsNullOrWhiteSpace(route.Id))
            {
                diagnostics.Add(Invalid(route, "Route id cannot be empty."));
            }

            if (string.IsNullOrWhiteSpace(route.OutletName))
            {
                diagnostics.Add(Invalid(route, "Outlet name cannot be empty."));
            }

            if (route.Kind is RouteDefinitionMetadataKind.Route or RouteDefinitionMetadataKind.Layout or RouteDefinitionMetadataKind.Index &&
                string.IsNullOrWhiteSpace(route.ViewModelTypeName))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Route '{route.Id}' requires a ViewModel target.",
                    route.Id));
            }

            if (!string.IsNullOrWhiteSpace(route.Template) && !IsValidTemplate(route.Template!))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Route '{route.Id}' has an invalid route template '{route.Template}'.",
                    route.Id));
            }

            switch (route.Kind)
            {
                case RouteDefinitionMetadataKind.Route when string.IsNullOrWhiteSpace(route.Template):
                    diagnostics.Add(Invalid(route, "Routes require a template."));
                    break;
                case RouteDefinitionMetadataKind.Layout when route.Template is not null:
                    diagnostics.Add(Invalid(route, "Layout routes cannot declare a template."));
                    break;
                case RouteDefinitionMetadataKind.Index when route.Template is not null:
                    diagnostics.Add(Invalid(route, "Index routes cannot declare a template."));
                    break;
                case RouteDefinitionMetadataKind.Group when string.IsNullOrWhiteSpace(route.Template) || route.ViewModelTypeName is not null:
                    diagnostics.Add(Invalid(route, "Route groups require a template and cannot declare a ViewModel target."));
                    break;
                case RouteDefinitionMetadataKind.Redirect when string.IsNullOrWhiteSpace(route.Template) || route.ViewModelTypeName is not null:
                    diagnostics.Add(Invalid(route, "Redirect routes require a template and cannot declare a ViewModel target."));
                    break;
                case RouteDefinitionMetadataKind.ExtensionPoint when route.Template is not null || route.ViewModelTypeName is not null:
                    diagnostics.Add(Invalid(route, "Route extension points cannot declare a template or ViewModel target."));
                    break;
                case RouteDefinitionMetadataKind.Route:
                case RouteDefinitionMetadataKind.Layout:
                case RouteDefinitionMetadataKind.Index:
                case RouteDefinitionMetadataKind.Group:
                case RouteDefinitionMetadataKind.Redirect:
                case RouteDefinitionMetadataKind.ExtensionPoint:
                    break;
                default:
                    diagnostics.Add(Invalid(route, $"Route kind '{route.Kind}' is not supported."));
                    break;
            }

            if (route.Kind == RouteDefinitionMetadataKind.Index && string.IsNullOrWhiteSpace(route.ParentMethodName))
            {
                diagnostics.Add(Invalid(route, "Index routes require a parent route."));
            }

            if (route.Kind == RouteDefinitionMetadataKind.Redirect && string.IsNullOrWhiteSpace(route.RedirectTargetMethodName))
            {
                diagnostics.Add(Invalid(route, "Redirect routes require a target route."));
            }

            if (route.Kind == RouteDefinitionMetadataKind.ExtensionPoint && string.IsNullOrWhiteSpace(route.ExtensionPoint))
            {
                diagnostics.Add(Invalid(route, "Route extension points require a stable extension point id."));
            }
        }
    }

    private static void ValidateResolvedRelationships(
        IReadOnlyList<RouteDefinitionMetadata> routes,
        IReadOnlyDictionary<string, string?> parentRouteIds,
        IReadOnlyDictionary<string, string?> redirectTargetRouteIds,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        DetectCycles(routes, parentRouteIds, "parent hierarchy", diagnostics);
        DetectCycles(
            routes.Where(route => route.Kind == RouteDefinitionMetadataKind.Redirect).ToArray(),
            redirectTargetRouteIds,
            "redirect graph",
            diagnostics);

        foreach (var duplicate in routes
            .Where(route => route.Kind == RouteDefinitionMetadataKind.Index)
            .GroupBy(route => (parentRouteIds[route.Id] ?? string.Empty, route.OutletName))
            .Where(group => group.Count() > 1))
        {
            diagnostics.Add(Invalid(duplicate.Last(), "A parent can declare only one index route per outlet."));
        }

        foreach (var duplicate in routes
            .Where(route => route.Kind == RouteDefinitionMetadataKind.ExtensionPoint)
            .GroupBy(route => route.ExtensionPoint, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            diagnostics.Add(Invalid(duplicate.Last(), $"Extension point '{duplicate.Key}' is declared more than once."));
        }

        var routesById = routes
            .GroupBy(route => route.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var redirect in routes.Where(route => route.Kind == RouteDefinitionMetadataKind.Redirect))
        {
            if (!redirectTargetRouteIds.TryGetValue(redirect.Id, out var targetId) ||
                targetId is null ||
                !routesById.TryGetValue(targetId, out var target))
            {
                continue;
            }

            if (target.Kind is RouteDefinitionMetadataKind.Group or RouteDefinitionMetadataKind.ExtensionPoint)
            {
                diagnostics.Add(Invalid(redirect, $"Redirect target '{target.Id}' is not navigable."));
            }
        }
    }

    private static void DetectCycles(
        IReadOnlyList<RouteDefinitionMetadata> routes,
        IReadOnlyDictionary<string, string?> nextByRouteId,
        string graphName,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var routeIds = new HashSet<string>(routes.Select(route => route.Id), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            var current = route.Id;
            while (routeIds.Contains(current) && !visited.Contains(current))
            {
                if (!path.Add(current))
                {
                    diagnostics.Add(Invalid(route, $"Route {graphName} contains a cycle at '{current}'."));
                    break;
                }

                if (!nextByRouteId.TryGetValue(current, out var next) || string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                current = next!;
            }

            foreach (var id in path)
            {
                visited.Add(id);
            }
        }
    }

    private static GeneratorDiagnostic Invalid(RouteDefinitionMetadata route, string message) =>
        new(GeneratorDiagnostics.InvalidManifestInput, $"Route '{route.Id}' is invalid. {message}", route.Id);

    private static bool IsValidTemplate(string template)
    {
        var segments = template.Trim().Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            var starts = segment.StartsWith("{", StringComparison.Ordinal);
            var ends = segment.EndsWith("}", StringComparison.Ordinal);
            if (starts != ends || (!starts && (segment.Contains("{") || segment.Contains("}"))))
            {
                return false;
            }

            if (!starts)
            {
                continue;
            }

            var body = segment.Substring(1, segment.Length - 2);
            var catchAll = body.StartsWith("*", StringComparison.Ordinal);
            if (catchAll)
            {
                body = body.Substring(1);
                if (segmentIndex != segments.Length - 1)
                {
                    return false;
                }
            }

            string? defaultValue = null;
            var defaultIndex = FindTopLevel(body, '=');
            if (defaultIndex >= 0)
            {
                defaultValue = body.Substring(defaultIndex + 1);
                body = body.Substring(0, defaultIndex);
            }

            var parts = SplitTopLevel(body, ':');
            if (parts is null || parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            var name = parts[0].EndsWith("?", StringComparison.Ordinal)
                ? parts[0].Substring(0, parts[0].Length - 1)
                : parts[0];
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                return false;
            }

            if (parts.Skip(1).Any(constraint => !IsValidConstraint(constraint)))
            {
                return false;
            }

            if (defaultValue is not null &&
                parts.Skip(1).Any(constraint => !SatisfiesConstraint(defaultValue, constraint)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidConstraint(string constraint)
    {
        if (constraint is "bool" or "datetime" or "decimal" or "double" or "float" or
            "guid" or "int" or "long" or "alpha")
        {
            return true;
        }

        if (TryArguments(constraint, "min", 1, out var min) ||
            TryArguments(constraint, "max", 1, out min))
        {
            return decimal.TryParse(min[0], NumberStyles.Number, CultureInfo.InvariantCulture, out _);
        }

        if (TryArguments(constraint, "range", 2, out var range))
        {
            return decimal.TryParse(range[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var low) &&
                decimal.TryParse(range[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var high) &&
                low <= high;
        }

        foreach (var name in new[] { "length", "minlength", "maxlength" })
        {
            if (TryArguments(constraint, name, 1, out var lengths))
            {
                return int.TryParse(lengths[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length) && length >= 0;
            }
        }

        if (TryArguments(constraint, "regex", 1, out var regex))
        {
            try
            {
                _ = new Regex(regex[0], RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool SatisfiesConstraint(string value, string constraint)
    {
        if (TryArguments(constraint, "min", 1, out var min))
        {
            return TryParseDecimal(value, out var numeric) &&
                TryParseDecimal(min[0], out var bound) &&
                numeric >= bound;
        }

        if (TryArguments(constraint, "max", 1, out var max))
        {
            return TryParseDecimal(value, out var numeric) &&
                TryParseDecimal(max[0], out var bound) &&
                numeric <= bound;
        }

        if (TryArguments(constraint, "range", 2, out var range))
        {
            return TryParseDecimal(value, out var numeric) &&
                TryParseDecimal(range[0], out var minimum) &&
                TryParseDecimal(range[1], out var maximum) &&
                numeric >= minimum && numeric <= maximum;
        }

        if (TryArguments(constraint, "length", 1, out var length))
        {
            return int.TryParse(length[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
                value.Length == count;
        }

        if (TryArguments(constraint, "minlength", 1, out var minimumLength))
        {
            return int.TryParse(minimumLength[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
                value.Length >= count;
        }

        if (TryArguments(constraint, "maxlength", 1, out var maximumLength))
        {
            return int.TryParse(maximumLength[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
                value.Length <= count;
        }

        if (TryArguments(constraint, "regex", 1, out var regex))
        {
            try
            {
                return Regex.IsMatch(value, regex[0], RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return constraint switch
        {
            "bool" => bool.TryParse(value, out _),
            "datetime" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "double" => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _),
            "float" => float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _),
            "guid" => Guid.TryParse(value, out _),
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "long" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "alpha" => value.Length > 0 && value.All(char.IsLetter),
            _ => false,
        };
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool TryArguments(string value, string name, int count, out string[] arguments)
    {
        var prefix = name + "(";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !value.EndsWith(")", StringComparison.Ordinal))
        {
            arguments = Array.Empty<string>();
            return false;
        }

        var body = value.Substring(prefix.Length, value.Length - prefix.Length - 1);
        arguments = count == 1
            ? new[] { body.Trim() }
            : body.Split(new[] { ',' }, StringSplitOptions.None)
                .Select(argument => argument.Trim())
                .ToArray();
        return arguments.Length == count && arguments.All(argument => argument.Length > 0);
    }

    private static string[]? SplitTopLevel(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var escaped = false;
        var inCharacterClass = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (current == ']' && inCharacterClass)
            {
                inCharacterClass = false;
                continue;
            }

            if (!inCharacterClass && current == '(') depth++;
            if (!inCharacterClass && current == ')') depth--;
            if (depth < 0) return null;
            if (current == separator && depth == 0 && !inCharacterClass)
            {
                parts.Add(value.Substring(start, index - start));
                start = index + 1;
            }
        }

        if (depth != 0 || inCharacterClass) return null;
        parts.Add(value.Substring(start));
        return parts.ToArray();
    }

    private static int FindTopLevel(string value, char character)
    {
        var depth = 0;
        var escaped = false;
        var inCharacterClass = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (current == ']' && inCharacterClass)
            {
                inCharacterClass = false;
                continue;
            }

            if (!inCharacterClass && current == '(') depth++;
            if (!inCharacterClass && current == ')') depth--;
            if (current == character && depth == 0 && !inCharacterClass) return index;
        }

        return -1;
    }

    private static string? ResolveReferencedRouteId(
        RouteDefinitionMetadata route,
        string? referencedMethodName,
        IReadOnlyDictionary<string, RouteDefinitionMetadata> routesByMethod,
        ICollection<GeneratorDiagnostic> diagnostics,
        string referenceKind)
    {
        if (string.IsNullOrWhiteSpace(referencedMethodName))
        {
            return null;
        }

        var referencedMethodKey = CreateMethodKey(route.RouteMapTypeName, referencedMethodName!);

        if (routesByMethod.TryGetValue(referencedMethodKey, out var referencedRoute))
        {
            return referencedRoute.Id;
        }

        diagnostics.Add(new GeneratorDiagnostic(
            GeneratorDiagnostics.InvalidManifestInput,
            $"Route '{route.Id}' references missing {referenceKind} route method '{referencedMethodName}'.",
            route.Id));

        return null;
    }

    private static void DetectTemplateConflicts(
        IEnumerable<RouteDefinitionMetadata> routes,
        IReadOnlyDictionary<string, string?> parentRouteIds,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var routeArray = routes.ToArray();
        var routesById = routeArray
            .GroupBy(route => route.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var candidatesByTemplate = new Dictionary<
            (string OutletName, string TemplateSignature),
            List<RouteDefinitionMetadata>>();

        foreach (var route in routeArray)
        {
            if (string.IsNullOrWhiteSpace(route.Template))
            {
                continue;
            }

            var key = (
                route.OutletName,
                CreateTemplateSignature(GetFullTemplate(route, routesById, parentRouteIds)));

            if (!candidatesByTemplate.TryGetValue(key, out var candidates))
            {
                candidates = new List<RouteDefinitionMetadata>();
                candidatesByTemplate.Add(key, candidates);
            }

            candidates.Add(route);
        }

        foreach (var candidates in candidatesByTemplate.Values)
        {
            var unconditionalCandidates = candidates
                .Where(candidate => candidate.MatchPolicyTypeNames.Count == 0)
                .ToArray();
            if (unconditionalCandidates.Length <= 1)
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.DuplicateRoute,
                $"Effective route template '{GetFullTemplate(unconditionalCandidates[0], routesById, parentRouteIds)}' has more than one unconditional candidate for the same outlet.",
                unconditionalCandidates[1].Id));
        }
    }

    private static string CreateTemplateSignature(string template)
    {
        return string.Join(
            "/",
            template.Trim().Trim('/').Split('/')
                .Select(segment =>
                {
                    if (!segment.StartsWith("{", StringComparison.Ordinal) ||
                        !segment.EndsWith("}", StringComparison.Ordinal))
                    {
                        return "l:" + segment.ToUpperInvariant();
                    }

                    var body = segment.Substring(1, segment.Length - 2);
                    var isCatchAll = body.StartsWith("*", StringComparison.Ordinal);
                    if (isCatchAll)
                    {
                        body = body.Substring(1);
                    }

                    var hasDefault = false;
                    var defaultSeparator = FindTopLevelCharacter(body, '=');
                    if (defaultSeparator >= 0)
                    {
                        hasDefault = true;
                        body = body.Substring(0, defaultSeparator);
                    }

                    var parts = SplitParameterParts(body);
                    var constraints = string.Join(",", parts
                        .Skip(1)
                        .Select(NormalizeConstraintSignature)
                        .OrderBy(constraint => constraint, StringComparer.Ordinal));
                    if (isCatchAll)
                    {
                        return "c:" + constraints;
                    }

                    var isOptional = hasDefault || parts[0].EndsWith("?", StringComparison.Ordinal);
                    return string.Join(
                        ":",
                        "p",
                        isOptional ? "optional" : "required",
                        constraints);
                }));
    }

    private static string NormalizeConstraintSignature(string constraint)
    {
        var openParenthesis = constraint.IndexOf('(');
        if (openParenthesis < 0 || !constraint.EndsWith(")", StringComparison.Ordinal))
        {
            return constraint.ToLowerInvariant();
        }

        return constraint.Substring(0, openParenthesis).ToLowerInvariant() +
            "(" + constraint.Substring(openParenthesis + 1, constraint.Length - openParenthesis - 2).Trim() + ")";
    }

    private static string GetFullTemplate(
        RouteDefinitionMetadata route,
        IReadOnlyDictionary<string, RouteDefinitionMetadata> routesById,
        IReadOnlyDictionary<string, string?> parentRouteIds)
    {
        var segments = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        RouteDefinitionMetadata? current = route;
        while (current is not null && visited.Add(current.Id))
        {
            if (!string.IsNullOrWhiteSpace(current.Template))
            {
                segments.Push(current.Template!);
            }

            current = parentRouteIds.TryGetValue(current.Id, out var parentId) &&
                parentId is not null &&
                routesById.TryGetValue(parentId, out var parent)
                ? parent
                : null;
        }

        return string.Join("/", segments);
    }

    private static IReadOnlyList<string> SplitParameterParts(string value)
    {
        return SplitTopLevel(value, ':') ?? new[] { value };
    }

    private static int FindTopLevelCharacter(string value, char character)
    {
        return FindTopLevel(value, character);
    }

    private static string CreateMethodKey(string routeMapTypeName, string methodName)
    {
        return routeMapTypeName + "." + methodName;
    }
}
