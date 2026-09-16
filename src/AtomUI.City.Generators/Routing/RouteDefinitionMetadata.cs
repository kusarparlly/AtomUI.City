namespace AtomUI.City.Generators.Routing;

internal sealed class RouteDefinitionMetadata
{
    public RouteDefinitionMetadata(
        string routeMapTypeName,
        string methodName,
        string id,
        RouteDefinitionMetadataKind kind,
        string? template,
        string? viewModelTypeName,
        string? parentMethodName,
        string outletName,
        string? extensionPoint,
        string? redirectTargetMethodName,
        string? titleKey = null,
        string? descriptionKey = null,
        string? breadcrumbKey = null,
        string? groupKey = null,
        string? errorTitleKey = null,
        string? routeReferenceTypeName = null,
        string? parameterTypeName = null,
        IReadOnlyList<RouteParameterBindingMetadata>? parameterBindings = null,
        IReadOnlyList<string>? enterGuardTypeNames = null,
        IReadOnlyList<string>? leaveGuardTypeNames = null,
        IReadOnlyList<string>? matchPolicyTypeNames = null,
        IReadOnlyList<string>? resolverTypeNames = null,
        IReadOnlyList<string>? middlewareTypeNames = null,
        string? reuseKey = null,
        string? activationHint = null)
    {
        if (string.IsNullOrWhiteSpace(routeMapTypeName))
        {
            throw new ArgumentException("Route map type name cannot be empty.", nameof(routeMapTypeName));
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException("Route method name cannot be empty.", nameof(methodName));
        }

        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (outletName is null)
        {
            throw new ArgumentNullException(nameof(outletName));
        }

        RouteMapTypeName = routeMapTypeName;
        MethodName = methodName;
        Id = id;
        Kind = kind;
        Template = template;
        ViewModelTypeName = viewModelTypeName;
        ParentMethodName = parentMethodName;
        OutletName = outletName;
        ExtensionPoint = extensionPoint;
        RedirectTargetMethodName = redirectTargetMethodName;
        TitleKey = titleKey;
        DescriptionKey = descriptionKey;
        BreadcrumbKey = breadcrumbKey;
        GroupKey = groupKey;
        ErrorTitleKey = errorTitleKey;
        RouteReferenceTypeName = routeReferenceTypeName;
        ParameterTypeName = parameterTypeName;
        ParameterBindings = Array.AsReadOnly((parameterBindings ?? []).ToArray());
        EnterGuardTypeNames = Array.AsReadOnly((enterGuardTypeNames ?? []).ToArray());
        LeaveGuardTypeNames = Array.AsReadOnly((leaveGuardTypeNames ?? []).ToArray());
        MatchPolicyTypeNames = Array.AsReadOnly((matchPolicyTypeNames ?? []).ToArray());
        ResolverTypeNames = Array.AsReadOnly((resolverTypeNames ?? []).ToArray());
        MiddlewareTypeNames = Array.AsReadOnly((middlewareTypeNames ?? []).ToArray());
        ReuseKey = reuseKey;
        ActivationHint = activationHint;
    }

    public string RouteMapTypeName { get; }

    public string MethodName { get; }

    public string Id { get; }

    public RouteDefinitionMetadataKind Kind { get; }

    public string? Template { get; }

    public string? ViewModelTypeName { get; }

    public string? ParentMethodName { get; }

    public string OutletName { get; }

    public string? ExtensionPoint { get; }

    public string? RedirectTargetMethodName { get; }

    public string? TitleKey { get; }

    public string? DescriptionKey { get; }

    public string? BreadcrumbKey { get; }

    public string? GroupKey { get; }

    public string? ErrorTitleKey { get; }

    public string? RouteReferenceTypeName { get; }

    public string? ParameterTypeName { get; }

    public IReadOnlyList<RouteParameterBindingMetadata> ParameterBindings { get; }

    public IReadOnlyList<string> EnterGuardTypeNames { get; }
    public IReadOnlyList<string> LeaveGuardTypeNames { get; }
    public IReadOnlyList<string> MatchPolicyTypeNames { get; }
    public IReadOnlyList<string> ResolverTypeNames { get; }
    public IReadOnlyList<string> MiddlewareTypeNames { get; }
    public string? ReuseKey { get; }
    public string? ActivationHint { get; }
}
