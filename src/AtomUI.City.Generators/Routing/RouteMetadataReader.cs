using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AtomUI.City.Generators.Routing;

internal static class RouteMetadataReader
{
    private const string IndexRouteAttributeName = "AtomUI.City.Routing.IndexRouteAttribute";
    private const string LayoutRouteAttributeName = "AtomUI.City.Routing.LayoutRouteAttribute";
    private const string RedirectRouteAttributeName = "AtomUI.City.Routing.RedirectRouteAttribute";
    private const string RouteAttributeName = "AtomUI.City.Routing.RouteAttribute";
    private const string RouteExtensionPointAttributeName = "AtomUI.City.Routing.RouteExtensionPointAttribute";
    private const string RouteGroupAttributeName = "AtomUI.City.Routing.RouteGroupAttribute";
    private const string RouteMapAttributeName = "AtomUI.City.Routing.RouteMapAttribute";
    private const string RouteGuardsAttributeName = "AtomUI.City.Routing.RouteGuardsAttribute";
    private const string RouteResolversAttributeName = "AtomUI.City.Routing.RouteResolversAttribute";
    private const string RouteMatchPoliciesAttributeName = "AtomUI.City.Routing.RouteMatchPoliciesAttribute";
    private const string RouteMiddlewareAttributeName = "AtomUI.City.Routing.RouteMiddlewareAttribute";
    private const string EnterGuardInterfaceName = "AtomUI.City.Routing.IRouteEnterGuard";
    private const string LeaveGuardInterfaceName = "AtomUI.City.Routing.IRouteLeaveGuard";
    private const string MatchPolicyInterfaceName = "AtomUI.City.Routing.IRouteMatchPolicy";
    private const string ResolverInterfaceName = "AtomUI.City.Routing.IRouteResolver";
    private const string MiddlewareInterfaceName = "AtomUI.City.Routing.IRouteNavigationMiddleware";
    private const string QueryAttributeName = "AtomUI.City.Routing.QueryAttribute";
    private const string FragmentAttributeName = "AtomUI.City.Routing.FragmentAttribute";

    public static RouteMapMetadata? TryRead(INamedTypeSymbol type)
    {
        if (!HasAttribute(type, RouteMapAttributeName))
        {
            return null;
        }

        var routeMapTypeName = GetTypeName(type);
        var routes = type
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Select(method => TryReadRoute(routeMapTypeName, method))
            .Where(route => route is not null)
            .Cast<RouteDefinitionMetadata>()
            .ToArray();

        return new RouteMapMetadata(
            routeMapTypeName,
            routes,
            type.Locations.FirstOrDefault(),
            ValidateRouteMap(type));
    }

    private static IReadOnlyList<string> ValidateRouteMap(INamedTypeSymbol type)
    {
        var issues = new List<string>();
        if (!type.IsStatic || type.ContainingType is not null || type.Arity != 0 ||
            type.DeclaredAccessibility != Accessibility.Public ||
            !HasPartialModifier(type))
        {
            issues.Add($"Route map '{type.Name}' must be a top-level public static partial class.");
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(HasRouteDefinitionAttribute))
        {
            var route = TryReadRoute(GetTypeName(type), method);
            var routeDefinitionCount = method.GetAttributes().Count(attribute =>
                TryReadKind(GetAttributeTypeName(attribute)) is not null);
            if (routeDefinitionCount != 1)
            {
                issues.Add($"Route method '{type.Name}.{method.Name}' must declare exactly one route-definition attribute.");
            }

            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public ||
                method.Parameters.Length != 0 || method.Arity != 0 || !method.IsPartialDefinition ||
                method.PartialImplementationPart is not null)
            {
                issues.Add($"Route method '{type.Name}.{method.Name}' must be a public static partial parameterless definition.");
            }

            var returnType = method.ReturnType as INamedTypeSymbol;
            var isRouteReference = string.Equals(
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                "AtomUI.City.Routing.RouteReference",
                StringComparison.Ordinal);
            var isTypedRouteReference = returnType is { IsGenericType: true, TypeArguments.Length: 1 } &&
                string.Equals(
                    returnType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    "AtomUI.City.Routing.RouteReference<TParameters>",
                    StringComparison.Ordinal);
            var isExtensionPoint = string.Equals(
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                "AtomUI.City.Routing.RouteExtensionPoint",
                StringComparison.Ordinal);
            var hasValidReturnType = route?.Kind == RouteDefinitionMetadataKind.ExtensionPoint
                ? isExtensionPoint
                : isRouteReference || isTypedRouteReference;
            if (!hasValidReturnType)
            {
                issues.Add(route?.Kind == RouteDefinitionMetadataKind.ExtensionPoint
                    ? $"Route method '{type.Name}.{method.Name}' must return RouteExtensionPoint."
                    : $"Route method '{type.Name}.{method.Name}' must return RouteReference or RouteReference<TParameters>.");
                continue;
            }

            if (route is not null && route.ParameterTypeName is not null)
            {
                var templateParameters = ReadTemplateParameterNames(route.Template)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var boundParameters = new HashSet<string>(
                    route.ParameterBindings.Select(binding => binding.RouteName),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var parameter in templateParameters.Where(parameter => !boundParameters.Contains(parameter)))
                {
                    issues.Add(
                        $"Route method '{type.Name}.{method.Name}' parameter type does not expose a public member for route parameter '{parameter}'.");
                }

                ValidateParameterMembers(type, method, route, issues);
            }

            ValidateViewModelType(type, method, issues);
            ValidateBehaviorAttributes(type, method, issues);
        }

        return issues;
    }

    private static void ValidateBehaviorAttributes(
        INamedTypeSymbol routeMap,
        IMethodSymbol method,
        ICollection<string> issues)
    {
        ValidateBehaviorAttribute(routeMap, method, RouteGuardsAttributeName,
            [EnterGuardInterfaceName, LeaveGuardInterfaceName], "route guard", issues);
        ValidateBehaviorAttribute(routeMap, method, RouteResolversAttributeName,
            [ResolverInterfaceName], "route resolver", issues);
        ValidateBehaviorAttribute(routeMap, method, RouteMatchPoliciesAttributeName,
            [MatchPolicyInterfaceName], "route match policy", issues);
        ValidateBehaviorAttribute(routeMap, method, RouteMiddlewareAttributeName,
            [MiddlewareInterfaceName], "route middleware", issues);
    }

    private static void ValidateBehaviorAttribute(
        INamedTypeSymbol routeMap,
        IMethodSymbol method,
        string attributeName,
        IReadOnlyList<string> contractNames,
        string behaviorName,
        ICollection<string> issues)
    {
        var attribute = method.GetAttributes().FirstOrDefault(item =>
            string.Equals(GetAttributeTypeName(item), attributeName, StringComparison.Ordinal));
        if (attribute is null)
        {
            return;
        }

        foreach (var type in ReadAttributeTypes(attribute))
        {
            var implementsContract = type.AllInterfaces.Any(@interface =>
                contractNames.Contains(GetTypeName(@interface), StringComparer.Ordinal));
            if (!implementsContract || type.TypeKind != TypeKind.Class || type.IsAbstract ||
                !IsClosedType(type) ||
                !IsAccessible(type) || !HasPublicConstructor(type))
            {
                issues.Add(
                    $"Route method '{routeMap.Name}.{method.Name}' references invalid {behaviorName} type '{type.Name}'.");
            }
        }
    }

    private static void ValidateViewModelType(
        INamedTypeSymbol routeMap,
        IMethodSymbol method,
        ICollection<string> issues)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var kind = TryReadKind(GetAttributeTypeName(attribute));
            if (kind is not (RouteDefinitionMetadataKind.Route or RouteDefinitionMetadataKind.Layout or RouteDefinitionMetadataKind.Index))
            {
                continue;
            }

            var argumentIndex = kind == RouteDefinitionMetadataKind.Route ? 1 : 0;
            if (attribute.ConstructorArguments.Length <= argumentIndex ||
                attribute.ConstructorArguments[argumentIndex].Value is not INamedTypeSymbol viewModel ||
                viewModel.TypeKind != TypeKind.Class || viewModel.IsAbstract ||
                !IsClosedType(viewModel) ||
                !IsAccessible(viewModel) || !HasPublicConstructor(viewModel))
            {
                issues.Add($"Route method '{routeMap.Name}.{method.Name}' must reference an accessible, non-abstract ViewModel class.");
            }
        }
    }

    private static bool HasRouteDefinitionAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(attribute => TryReadKind(GetAttributeTypeName(attribute)) is not null);

    private static bool HasPartialModifier(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static bool IsAccessible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasPublicConstructor(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public);

    private static bool IsClosedType(INamedTypeSymbol type)
    {
        if (type.IsUnboundGenericType)
        {
            return false;
        }

        return type.TypeArguments.All(argument => argument switch
        {
            ITypeParameterSymbol => false,
            INamedTypeSymbol namedType => IsClosedType(namedType),
            _ => true,
        });
    }

    private static RouteDefinitionMetadata? TryReadRoute(string routeMapTypeName, IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var attributeName = GetAttributeTypeName(attribute);
            var kind = TryReadKind(attributeName);

            if (kind is null)
            {
                continue;
            }

            var id = ReadNamedString(attribute, "Id") ?? routeMapTypeName + "." + method.Name;

            return new RouteDefinitionMetadata(
                routeMapTypeName,
                method.Name,
                id,
                kind.Value,
                ReadTemplate(attribute, kind.Value),
                ReadViewModelTypeName(attribute, kind.Value),
                ReadNamedString(attribute, "Parent"),
                ReadNamedString(attribute, "Outlet") ?? "primary",
                ReadExtensionPoint(attribute, kind.Value),
                ReadNamedString(attribute, "Target"),
                ReadNamedString(attribute, "TitleKey"),
                ReadNamedString(attribute, "DescriptionKey"),
                ReadNamedString(attribute, "BreadcrumbKey"),
                ReadNamedString(attribute, "GroupKey"),
                ReadNamedString(attribute, "ErrorTitleKey"),
                GetTypeName(method.ReturnType),
                ReadParameterTypeName(method),
                ReadParameterBindings(method, ReadTemplate(attribute, kind.Value)),
                ReadBehaviorTypeNames(method, RouteGuardsAttributeName, EnterGuardInterfaceName),
                ReadBehaviorTypeNames(method, RouteGuardsAttributeName, LeaveGuardInterfaceName),
                ReadBehaviorTypeNames(method, RouteMatchPoliciesAttributeName, MatchPolicyInterfaceName),
                ReadBehaviorTypeNames(method, RouteResolversAttributeName, ResolverInterfaceName),
                ReadBehaviorTypeNames(method, RouteMiddlewareAttributeName, MiddlewareInterfaceName),
                ReadNamedString(attribute, "ReuseKey"),
                ReadNamedString(attribute, "ActivationHint"));
        }

        return null;
    }

    private static IReadOnlyList<string> ReadBehaviorTypeNames(
        IMethodSymbol method,
        string attributeName,
        string contractName)
    {
        var attribute = method.GetAttributes().FirstOrDefault(item =>
            string.Equals(GetAttributeTypeName(item), attributeName, StringComparison.Ordinal));
        if (attribute is null)
        {
            return [];
        }

        return ReadAttributeTypes(attribute)
            .Where(type => type.AllInterfaces.Any(@interface =>
                string.Equals(GetTypeName(@interface), contractName, StringComparison.Ordinal)))
            .Select(GetTypeName)
            .ToArray();
    }

    private static IEnumerable<INamedTypeSymbol> ReadAttributeTypes(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            yield break;
        }

        var argument = attribute.ConstructorArguments[0];
        foreach (var value in argument.Kind == TypedConstantKind.Array ? argument.Values : [argument])
        {
            if (value.Value is INamedTypeSymbol type)
            {
                yield return type;
            }
        }
    }

    private static RouteDefinitionMetadataKind? TryReadKind(string? attributeName)
    {
        switch (attributeName)
        {
            case RouteAttributeName:
                return RouteDefinitionMetadataKind.Route;
            case LayoutRouteAttributeName:
                return RouteDefinitionMetadataKind.Layout;
            case IndexRouteAttributeName:
                return RouteDefinitionMetadataKind.Index;
            case RouteGroupAttributeName:
                return RouteDefinitionMetadataKind.Group;
            case RedirectRouteAttributeName:
                return RouteDefinitionMetadataKind.Redirect;
            case RouteExtensionPointAttributeName:
                return RouteDefinitionMetadataKind.ExtensionPoint;
            default:
                return null;
        }
    }

    private static string? ReadTemplate(AttributeData attribute, RouteDefinitionMetadataKind kind)
    {
        if (kind is RouteDefinitionMetadataKind.Layout or RouteDefinitionMetadataKind.Index or RouteDefinitionMetadataKind.ExtensionPoint)
        {
            return null;
        }

        return ReadConstructorString(attribute, 0);
    }

    private static string? ReadViewModelTypeName(AttributeData attribute, RouteDefinitionMetadataKind kind)
    {
        var argumentIndex = kind == RouteDefinitionMetadataKind.Route ? 1 : 0;

        if (kind is RouteDefinitionMetadataKind.Group or RouteDefinitionMetadataKind.Redirect or RouteDefinitionMetadataKind.ExtensionPoint)
        {
            return null;
        }

        return ReadConstructorTypeName(attribute, argumentIndex);
    }

    private static string? ReadExtensionPoint(AttributeData attribute, RouteDefinitionMetadataKind kind)
    {
        if (kind == RouteDefinitionMetadataKind.ExtensionPoint)
        {
            return ReadConstructorString(attribute, 0);
        }

        return ReadNamedString(attribute, "ExtensionPoint");
    }

    private static string? ReadConstructorString(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value as string;
    }

    private static string? ReadConstructorTypeName(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value is INamedTypeSymbol type ? GetTypeName(type) : null;
    }

    private static bool HasAttribute(INamedTypeSymbol type, string attributeName)
    {
        return type
            .GetAttributes()
            .Any(attribute => string.Equals(GetAttributeTypeName(attribute), attributeName, StringComparison.Ordinal));
    }

    private static string? ReadNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    private static string? GetAttributeTypeName(AttributeData attribute)
    {
        return attribute.AttributeClass is null ? null : GetTypeName(attribute.AttributeClass);
    }

    private static string GetTypeName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static string? ReadParameterTypeName(IMethodSymbol method)
    {
        return method.ReturnType is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } returnType &&
            string.Equals(
                returnType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                "AtomUI.City.Routing.RouteReference<TParameters>",
                StringComparison.Ordinal)
            ? GetTypeName(returnType.TypeArguments[0])
            : null;
    }

    private static IReadOnlyList<RouteParameterBindingMetadata> ReadParameterBindings(
        IMethodSymbol method,
        string? template)
    {
        var parameterTypeName = ReadParameterTypeName(method);
        if (parameterTypeName is null ||
            method.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 1 } returnType)
        {
            return [];
        }

        if (returnType.TypeArguments[0] is not INamedTypeSymbol parameterType)
        {
            return [];
        }

        return ReadParameterBindingCandidates(parameterType, template)
            .GroupBy(binding => binding.RouteName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<RouteParameterBindingMetadata> ReadParameterBindingCandidates(
        INamedTypeSymbol parameterType,
        string? template)
    {
        var bindableMembers = GetBindableMembers(parameterType);
        var members = new Dictionary<string, ISymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in bindableMembers.OrderBy(member => member.Name, StringComparer.Ordinal))
        {
            if (!members.ContainsKey(member.Name))
            {
                members.Add(member.Name, member);
            }
        }

        var bindings = new List<RouteParameterBindingMetadata>();

        foreach (var routeName in ReadTemplateParameterNames(template))
        {
            if (members.TryGetValue(routeName, out var member))
            {
                bindings.Add(new RouteParameterBindingMetadata(routeName, member.Name));
            }
        }

        foreach (var member in bindableMembers.OrderBy(member => member.Name, StringComparer.Ordinal))
        {
            var bindingAttribute = member.GetAttributes().FirstOrDefault(attribute =>
                string.Equals(GetAttributeTypeName(attribute), QueryAttributeName, StringComparison.Ordinal) ||
                string.Equals(GetAttributeTypeName(attribute), FragmentAttributeName, StringComparison.Ordinal));
            if (bindingAttribute is null)
            {
                continue;
            }

            var isFragment = string.Equals(
                GetAttributeTypeName(bindingAttribute),
                FragmentAttributeName,
                StringComparison.Ordinal);
            var routeName = ReadConstructorString(bindingAttribute, 0) ??
                (isFragment ? "fragment" : member.Name);
            bindings.Add(new RouteParameterBindingMetadata(routeName, member.Name));
        }

        return bindings;
    }

    private static IReadOnlyList<ISymbol> GetBindableMembers(INamedTypeSymbol parameterType)
    {
        var members = new List<ISymbol>();
        var hiddenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var current = parameterType;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            foreach (var group in current.GetMembers().GroupBy(member => member.Name, StringComparer.Ordinal))
            {
                if (!hiddenNames.Add(group.Key))
                {
                    continue;
                }

                members.AddRange(group.Where(IsBindableMember));
            }
        }

        return members;
    }

    private static bool IsBindableMember(ISymbol member) => member switch
    {
        IPropertySymbol property =>
            !property.IsStatic &&
            property.DeclaredAccessibility == Accessibility.Public &&
            property.Parameters.Length == 0 &&
            property.GetMethod?.DeclaredAccessibility == Accessibility.Public,
        IFieldSymbol field =>
            !field.IsStatic &&
            field.DeclaredAccessibility == Accessibility.Public,
        _ => false,
    };

    private static void ValidateParameterMembers(
        INamedTypeSymbol routeMap,
        IMethodSymbol method,
        RouteDefinitionMetadata route,
        ICollection<string> issues)
    {
        if (method.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 1 } returnType ||
            returnType.TypeArguments[0] is not INamedTypeSymbol parameterType)
        {
            return;
        }

        var members = GetBindableMembers(parameterType);
        foreach (var duplicate in members
            .GroupBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"Route method '{routeMap.Name}.{method.Name}' parameter type exposes ambiguous public members for '{duplicate.Key}'.");
        }

        foreach (var duplicate in ReadParameterBindingCandidates(parameterType, route.Template)
            .GroupBy(binding => binding.RouteName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(binding => binding.MemberName).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            issues.Add(
                $"Route method '{routeMap.Name}.{method.Name}' binds route parameter '{duplicate.Key}' from more than one public member.");
        }
    }

    private static IEnumerable<string> ReadTemplateParameterNames(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            yield break;
        }

        foreach (var segment in template!.Split('/'))
        {
            if (!segment.StartsWith("{", StringComparison.Ordinal) ||
                !segment.EndsWith("}", StringComparison.Ordinal))
            {
                continue;
            }

            var body = segment.Substring(1, segment.Length - 2).TrimStart('*');
            var separator = body.IndexOfAny(new[] { ':', '=', '?' });
            var name = separator < 0 ? body : body.Substring(0, separator);

            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }
}
