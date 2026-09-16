namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation resource contribution.
/// </summary>
public sealed class PresentationResourceContribution
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationResourceContribution</c> type.
    /// </summary>
    public PresentationResourceContribution(
        string kind,
        object resource,
        string? pluginId = null,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(resource);

        Kind = kind;
        Resource = resource;
        PluginId = string.IsNullOrWhiteSpace(pluginId) ? null : pluginId;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets resource.
    /// </summary>
    public object Resource { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }
}
