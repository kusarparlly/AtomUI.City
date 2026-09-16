namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation plugin unload request.
/// </summary>
public sealed class PresentationPluginUnloadRequest
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationPluginUnloadRequest</c> type.
    /// </summary>
    public PresentationPluginUnloadRequest(
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
