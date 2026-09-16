namespace AtomUI.City.Routing;

/// <summary>
/// Represents route group.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteGroupAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteGroupAttribute</c> type.
    /// </summary>
    public RouteGroupAttribute(string template)
        : base(RouteDefinitionKind.Group, template, null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
    }
}
