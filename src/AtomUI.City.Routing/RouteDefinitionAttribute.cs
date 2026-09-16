namespace AtomUI.City.Routing;

/// <summary>
/// Represents route definition.
/// </summary>
public abstract class RouteDefinitionAttribute : Attribute
{
    /// <summary>
    /// Executes the route definition operation.
    /// </summary>
    protected RouteDefinitionAttribute(RouteDefinitionKind kind, string? template, Type? viewModelType)
    {
        Kind = kind;
        Template = template;
        ViewModelType = viewModelType;
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public RouteDefinitionKind Kind { get; }

    /// <summary>
    /// Gets template.
    /// </summary>
    public string? Template { get; }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type? ViewModelType { get; }

    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets parent.
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Gets or sets outlet.
    /// </summary>
    public string Outlet { get; set; } = "primary";

    /// <summary>
    /// Gets or sets extension point.
    /// </summary>
    public string? ExtensionPoint { get; set; }

    /// <summary>
    /// Gets or sets target.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets title key.
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>
    /// Gets or sets description key.
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// Gets or sets breadcrumb key.
    /// </summary>
    public string? BreadcrumbKey { get; set; }

    /// <summary>
    /// Gets or sets group key.
    /// </summary>
    public string? GroupKey { get; set; }

    /// <summary>
    /// Gets or sets error title key.
    /// </summary>
    public string? ErrorTitleKey { get; set; }

    /// <summary>
    /// Gets or sets reuse key.
    /// </summary>
    public string? ReuseKey { get; set; }

    /// <summary>
    /// Gets or sets activation hint.
    /// </summary>
    public string? ActivationHint { get; set; }
}
