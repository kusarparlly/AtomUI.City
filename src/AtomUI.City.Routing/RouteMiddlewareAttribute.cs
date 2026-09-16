namespace AtomUI.City.Routing;

/// <summary>
/// Represents route middleware.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteMiddlewareAttribute(params Type[] middlewareTypes) : Attribute
{
    /// <summary>
    /// Gets middleware types.
    /// </summary>
    public IReadOnlyList<Type> MiddlewareTypes { get; } = Array.AsReadOnly(
        (middlewareTypes ?? throw new ArgumentNullException(nameof(middlewareTypes))).ToArray());
}
