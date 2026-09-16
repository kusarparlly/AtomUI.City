namespace AtomUI.City.Routing;

/// <summary>
/// Represents route guards.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteGuardsAttribute(params Type[] guardTypes) : Attribute
{
    /// <summary>
    /// Gets guard types.
    /// </summary>
    public IReadOnlyList<Type> GuardTypes { get; } = Array.AsReadOnly(
        (guardTypes ?? throw new ArgumentNullException(nameof(guardTypes))).ToArray());
}
