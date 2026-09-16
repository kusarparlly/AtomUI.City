namespace AtomUI.City.Routing;

/// <summary>
/// Defines the supported route graph error values.
/// </summary>
public enum RouteGraphError
{
    /// <summary>
    /// Represents the duplicate route id value.
    /// </summary>
    DuplicateRouteId,
    /// <summary>
    /// Represents the duplicate route template value.
    /// </summary>
    DuplicateRouteTemplate,
    /// <summary>
    /// Represents the missing parent route value.
    /// </summary>
    MissingParentRoute,
    /// <summary>
    /// Represents the invalid route template value.
    /// </summary>
    InvalidRouteTemplate,
    /// <summary>
    /// Represents the invalid contribution value.
    /// </summary>
    InvalidContribution,
    /// <summary>
    /// Represents the circular parent route value.
    /// </summary>
    CircularParentRoute,
    /// <summary>
    /// Represents the invalid route definition value.
    /// </summary>
    InvalidRouteDefinition,
    /// <summary>
    /// Represents the duplicate index route value.
    /// </summary>
    DuplicateIndexRoute,
    /// <summary>
    /// Represents the duplicate extension point value.
    /// </summary>
    DuplicateExtensionPoint,
    /// <summary>
    /// Represents the missing extension point value.
    /// </summary>
    MissingExtensionPoint,
    /// <summary>
    /// Represents the missing redirect target value.
    /// </summary>
    MissingRedirectTarget,
    /// <summary>
    /// Represents the circular redirect value.
    /// </summary>
    CircularRedirect,
    /// <summary>
    /// Represents the invalid version value.
    /// </summary>
    InvalidVersion,
}
