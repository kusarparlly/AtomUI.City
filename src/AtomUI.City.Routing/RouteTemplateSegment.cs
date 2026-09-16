namespace AtomUI.City.Routing;

/// <summary>
/// Represents route template segment.
/// </summary>
public sealed class RouteTemplateSegment
{
    private RouteTemplateSegment(
        RouteTemplateSegmentKind kind,
        string? literal,
        string? name,
        bool isOptional,
        string? defaultValue,
        IReadOnlyList<string> constraints)
    {
        Kind = kind;
        Literal = literal;
        Name = name;
        IsOptional = isOptional;
        DefaultValue = defaultValue;
        Constraints = Array.AsReadOnly(constraints.ToArray());
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public RouteTemplateSegmentKind Kind { get; }

    /// <summary>
    /// Gets literal.
    /// </summary>
    public string? Literal { get; }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets a value indicating whether is optional.
    /// </summary>
    public bool IsOptional { get; }

    /// <summary>
    /// Gets default value.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Gets constraints.
    /// </summary>
    public IReadOnlyList<string> Constraints { get; }

    internal static RouteTemplateSegment LiteralSegment(string literal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(literal);

        return new RouteTemplateSegment(RouteTemplateSegmentKind.Literal, literal, null, false, null, []);
    }

    internal static RouteTemplateSegment ParameterSegment(
        RouteTemplateSegmentKind kind,
        string name,
        bool isOptional,
        string? defaultValue,
        IReadOnlyList<string> constraints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(constraints);

        return new RouteTemplateSegment(kind, null, name, isOptional, defaultValue, constraints);
    }
}
