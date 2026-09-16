namespace AtomUI.City.Routing;

/// <summary>
/// Represents redirect route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RedirectRouteAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>RedirectRouteAttribute</c> type.
    /// </summary>
    public RedirectRouteAttribute(string template)
        : base(RouteDefinitionKind.Redirect, template, null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
    }
}
