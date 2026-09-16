namespace AtomUI.City.Routing;

/// <summary>
/// Represents route resolvers.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteResolversAttribute(params Type[] resolverTypes) : Attribute
{
    /// <summary>
    /// Gets resolver types.
    /// </summary>
    public IReadOnlyList<Type> ResolverTypes { get; } = Array.AsReadOnly(
        (resolverTypes ?? throw new ArgumentNullException(nameof(resolverTypes))).ToArray());
}
