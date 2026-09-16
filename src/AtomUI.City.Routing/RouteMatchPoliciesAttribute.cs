namespace AtomUI.City.Routing;

/// <summary>
/// Represents route match policies.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteMatchPoliciesAttribute(params Type[] policyTypes) : Attribute
{
    /// <summary>
    /// Gets policy types.
    /// </summary>
    public IReadOnlyList<Type> PolicyTypes { get; } = Array.AsReadOnly(
        (policyTypes ?? throw new ArgumentNullException(nameof(policyTypes))).ToArray());
}
