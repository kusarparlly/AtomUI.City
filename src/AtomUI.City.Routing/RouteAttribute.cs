namespace AtomUI.City.Routing;

/// <summary>
/// Represents route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteAttribute</c> type.
    /// </summary>
    public RouteAttribute(string template, Type viewModelType)
        : base(RouteDefinitionKind.Route, template, viewModelType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(viewModelType);
    }
}
