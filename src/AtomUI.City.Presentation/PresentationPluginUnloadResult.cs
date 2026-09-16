namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation plugin unload result.
/// </summary>
public sealed class PresentationPluginUnloadResult
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationPluginUnloadResult</c> type.
    /// </summary>
    public PresentationPluginUnloadResult(
        string pluginId,
        string? contributionId,
        int closedViewCount,
        int revokedInteractionHandlerCount,
        int revokedViewDescriptorCount,
        int revokedResourceContributionCount,
        bool resourceDictionariesRevoked,
        IReadOnlyList<PresentationPluginUnloadError> errors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(errors);

        PluginId = pluginId;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
        ClosedViewCount = closedViewCount;
        RevokedInteractionHandlerCount = revokedInteractionHandlerCount;
        RevokedViewDescriptorCount = revokedViewDescriptorCount;
        RevokedResourceContributionCount = revokedResourceContributionCount;
        ResourceDictionariesRevoked = resourceDictionariesRevoked;
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Gets closed view count.
    /// </summary>
    public int ClosedViewCount { get; }

    /// <summary>
    /// Gets revoked interaction handler count.
    /// </summary>
    public int RevokedInteractionHandlerCount { get; }

    /// <summary>
    /// Gets revoked view descriptor count.
    /// </summary>
    public int RevokedViewDescriptorCount { get; }

    /// <summary>
    /// Gets revoked resource contribution count.
    /// </summary>
    public int RevokedResourceContributionCount { get; }

    /// <summary>
    /// Gets resource dictionaries revoked.
    /// </summary>
    public bool ResourceDictionariesRevoked { get; }

    /// <summary>
    /// Gets errors.
    /// </summary>
    public IReadOnlyList<PresentationPluginUnloadError> Errors { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
