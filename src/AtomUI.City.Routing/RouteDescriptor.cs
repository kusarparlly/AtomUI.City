namespace AtomUI.City.Routing;

/// <summary>
/// Represents route descriptor.
/// </summary>
public sealed class RouteDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteDescriptor</c> type.
    /// </summary>
    public RouteDescriptor(
        string routeId,
        RouteDefinitionKind kind,
        string? template,
        ViewModelTargetDescriptor? viewModelTarget,
        string? parentRouteId = null,
        string outletName = "primary",
        string? extensionPoint = null,
        string? redirectTargetRouteId = null,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? leaveGuardTypes = null,
        IReadOnlyList<Type>? matchPolicyTypes = null,
        RouteMetadataDescriptor? metadata = null,
        string? contributionId = null,
        IReadOnlyList<Type>? resolverTypes = null,
        IReadOnlyList<Type>? middlewareTypes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outletName);

        RouteId = routeId;
        Kind = kind;
        Template = string.IsNullOrWhiteSpace(template) ? null : RouteTemplate.Parse(template);
        ViewModelTarget = viewModelTarget;
        ParentRouteId = parentRouteId;
        OutletName = outletName;
        ExtensionPoint = extensionPoint;
        RedirectTargetRouteId = redirectTargetRouteId;
        EnterGuardTypes = AsReadOnly(enterGuardTypes);
        LeaveGuardTypes = AsReadOnly(leaveGuardTypes);
        MatchPolicyTypes = AsReadOnly(matchPolicyTypes);
        Metadata = metadata ?? RouteMetadataDescriptor.Empty;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
        ResolverTypes = AsReadOnly(resolverTypes);
        MiddlewareTypes = AsReadOnly(middlewareTypes);
    }

    /// <summary>
    /// Gets route id.
    /// </summary>
    public string RouteId { get; }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public RouteDefinitionKind Kind { get; }

    /// <summary>
    /// Gets template.
    /// </summary>
    public RouteTemplate? Template { get; }

    /// <summary>
    /// Gets view model target.
    /// </summary>
    public ViewModelTargetDescriptor? ViewModelTarget { get; }

    /// <summary>
    /// Gets parent route id.
    /// </summary>
    public string? ParentRouteId { get; }

    /// <summary>
    /// Gets outlet name.
    /// </summary>
    public string OutletName { get; }

    /// <summary>
    /// Gets extension point.
    /// </summary>
    public string? ExtensionPoint { get; }

    /// <summary>
    /// Gets redirect target route id.
    /// </summary>
    public string? RedirectTargetRouteId { get; }

    /// <summary>
    /// Gets enter guard types.
    /// </summary>
    public IReadOnlyList<Type> EnterGuardTypes { get; }

    /// <summary>
    /// Gets leave guard types.
    /// </summary>
    public IReadOnlyList<Type> LeaveGuardTypes { get; }

    /// <summary>
    /// Gets match policy types.
    /// </summary>
    public IReadOnlyList<Type> MatchPolicyTypes { get; }

    /// <summary>
    /// Gets metadata.
    /// </summary>
    public RouteMetadataDescriptor Metadata { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Gets resolver types.
    /// </summary>
    public IReadOnlyList<Type> ResolverTypes { get; }

    /// <summary>
    /// Gets middleware types.
    /// </summary>
    public IReadOnlyList<Type> MiddlewareTypes { get; }

    private static IReadOnlyList<Type> AsReadOnly(IReadOnlyList<Type>? values)
    {
        return values is null
            ? Array.Empty<Type>()
            : Array.AsReadOnly(values.ToArray());
    }

    internal RouteDescriptor WithParentRouteId(string? parentRouteId) =>
        new(
            RouteId,
            Kind,
            Template?.Pattern,
            ViewModelTarget,
            parentRouteId,
            OutletName,
            ExtensionPoint,
            RedirectTargetRouteId,
            EnterGuardTypes,
            LeaveGuardTypes,
            MatchPolicyTypes,
            Metadata,
            ContributionId,
            ResolverTypes,
            MiddlewareTypes);

    internal RouteDescriptor WithContributionId(string contributionId) =>
        new(
            RouteId,
            Kind,
            Template?.Pattern,
            ViewModelTarget,
            ParentRouteId,
            OutletName,
            ExtensionPoint,
            RedirectTargetRouteId,
            EnterGuardTypes,
            LeaveGuardTypes,
            MatchPolicyTypes,
            Metadata,
            contributionId,
            ResolverTypes,
            MiddlewareTypes);
}
