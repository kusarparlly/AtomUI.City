namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported route template segment kind values.
/// </summary>
public enum RouteTemplateSegmentKind
{
    /// <summary>
    /// Represents the literal value.
    /// </summary>
    Literal,
    /// <summary>
    /// Represents the parameter value.
    /// </summary>
    Parameter,
    /// <summary>
    /// Represents the catch all value.
    /// </summary>
    CatchAll,
}
