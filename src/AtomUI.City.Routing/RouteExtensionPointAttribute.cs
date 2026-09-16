namespace AtomUI.City.Routing;

/// <summary>
/// Represents route extension point.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteExtensionPointAttribute : RouteDefinitionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteExtensionPointAttribute</c> type.
    /// </summary>
    public RouteExtensionPointAttribute(string extensionPoint)
        : base(RouteDefinitionKind.ExtensionPoint, null, null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionPoint);

        ExtensionPoint = extensionPoint;
    }
}
