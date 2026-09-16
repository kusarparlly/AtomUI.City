namespace AtomUI.City.Routing;

/// <summary>
/// Represents route metadata descriptor.
/// </summary>
public sealed class RouteMetadataDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteMetadataDescriptor</c> type.
    /// </summary>
    public RouteMetadataDescriptor(
        string? titleKey = null,
        string? descriptionKey = null,
        string? breadcrumbKey = null,
        string? groupKey = null,
        string? errorTitleKey = null)
    {
        TitleKey = NormalizeKey(titleKey);
        DescriptionKey = NormalizeKey(descriptionKey);
        BreadcrumbKey = NormalizeKey(breadcrumbKey);
        GroupKey = NormalizeKey(groupKey);
        ErrorTitleKey = NormalizeKey(errorTitleKey);
    }

    /// <summary>
    /// Gets empty.
    /// </summary>
    public static RouteMetadataDescriptor Empty { get; } = new();

    /// <summary>
    /// Gets title key.
    /// </summary>
    public string? TitleKey { get; }

    /// <summary>
    /// Gets description key.
    /// </summary>
    public string? DescriptionKey { get; }

    /// <summary>
    /// Gets breadcrumb key.
    /// </summary>
    public string? BreadcrumbKey { get; }

    /// <summary>
    /// Gets group key.
    /// </summary>
    public string? GroupKey { get; }

    /// <summary>
    /// Gets error title key.
    /// </summary>
    public string? ErrorTitleKey { get; }

    private static string? NormalizeKey(string? key)
    {
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
