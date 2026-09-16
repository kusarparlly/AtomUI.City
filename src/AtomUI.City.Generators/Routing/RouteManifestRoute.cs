namespace AtomUI.City.Generators.Routing;

internal sealed class RouteManifestRoute
{
    public RouteManifestRoute(
        string id,
        RouteDefinitionMetadataKind kind,
        string? template,
        string? viewModelTypeName,
        string? parentRouteId,
        string outletName,
        string? extensionPoint,
        string? redirectTargetRouteId,
        string? titleKey = null,
        string? descriptionKey = null,
        string? breadcrumbKey = null,
        string? groupKey = null,
        string? errorTitleKey = null,
        IReadOnlyList<string>? enterGuardTypeNames = null,
        IReadOnlyList<string>? leaveGuardTypeNames = null,
        IReadOnlyList<string>? matchPolicyTypeNames = null,
        IReadOnlyList<string>? resolverTypeNames = null,
        IReadOnlyList<string>? middlewareTypeNames = null,
        IReadOnlyList<string>? parameterBindingNames = null,
        string? reuseKey = null,
        string? activationHint = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Route id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(outletName))
        {
            throw new ArgumentException("Outlet name cannot be empty.", nameof(outletName));
        }

        Id = id;
        Kind = kind;
        Template = template;
        ViewModelTypeName = viewModelTypeName;
        ParentRouteId = parentRouteId;
        OutletName = outletName;
        ExtensionPoint = extensionPoint;
        RedirectTargetRouteId = redirectTargetRouteId;
        TitleKey = titleKey;
        DescriptionKey = descriptionKey;
        BreadcrumbKey = breadcrumbKey;
        GroupKey = groupKey;
        ErrorTitleKey = errorTitleKey;
        EnterGuardTypeNames = Array.AsReadOnly((enterGuardTypeNames ?? []).ToArray());
        LeaveGuardTypeNames = Array.AsReadOnly((leaveGuardTypeNames ?? []).ToArray());
        MatchPolicyTypeNames = Array.AsReadOnly((matchPolicyTypeNames ?? []).ToArray());
        ResolverTypeNames = Array.AsReadOnly((resolverTypeNames ?? []).ToArray());
        MiddlewareTypeNames = Array.AsReadOnly((middlewareTypeNames ?? []).ToArray());
        ParameterBindingNames = Array.AsReadOnly((parameterBindingNames ?? []).ToArray());
        ReuseKey = reuseKey;
        ActivationHint = activationHint;
    }

    public string Id { get; }

    public RouteDefinitionMetadataKind Kind { get; }

    public string? Template { get; }

    public string? ViewModelTypeName { get; }

    public string? ParentRouteId { get; }

    public string OutletName { get; }

    public string? ExtensionPoint { get; }

    public string? RedirectTargetRouteId { get; }

    public string? TitleKey { get; }

    public string? DescriptionKey { get; }

    public string? BreadcrumbKey { get; }

    public string? GroupKey { get; }

    public string? ErrorTitleKey { get; }
    public IReadOnlyList<string> EnterGuardTypeNames { get; }
    public IReadOnlyList<string> LeaveGuardTypeNames { get; }
    public IReadOnlyList<string> MatchPolicyTypeNames { get; }
    public IReadOnlyList<string> ResolverTypeNames { get; }
    public IReadOnlyList<string> MiddlewareTypeNames { get; }
    public IReadOnlyList<string> ParameterBindingNames { get; }
    public string? ReuseKey { get; }
    public string? ActivationHint { get; }
}
