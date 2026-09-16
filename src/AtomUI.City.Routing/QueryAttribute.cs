namespace AtomUI.City.Routing;

/// <summary>
/// Represents query.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class QueryAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets name.
    /// </summary>
    public string? Name { get; } = string.IsNullOrWhiteSpace(name) ? null : name;
}
