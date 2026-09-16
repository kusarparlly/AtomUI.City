namespace AtomUI.City.Routing;

/// <summary>
/// Represents index route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IndexRouteAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>IndexRouteAttribute</c> type.
    /// </summary>
    public IndexRouteAttribute(Type viewModelType)
        : base(RouteDefinitionKind.Index, null, viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
    }
}
