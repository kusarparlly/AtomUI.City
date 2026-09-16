namespace AtomUI.City.Routing;

/// <summary>
/// Represents layout route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LayoutRouteAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>LayoutRouteAttribute</c> type.
    /// </summary>
    public LayoutRouteAttribute(Type viewModelType)
        : base(RouteDefinitionKind.Layout, null, viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
    }
}
