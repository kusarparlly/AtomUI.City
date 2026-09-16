namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation resource dictionary revocation.
/// </summary>
public sealed class PresentationResourceDictionaryRevocation
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationResourceDictionaryRevocation</c> type.
    /// </summary>
    public PresentationResourceDictionaryRevocation(
        string pluginId,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        PluginId = pluginId;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }
}
