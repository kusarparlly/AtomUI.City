namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported presentation plugin unload error kind values.
/// </summary>
public enum PresentationPluginUnloadErrorKind
{
    /// <summary>
    /// Represents the active views remaining value.
    /// </summary>
    ActiveViewsRemaining,
    /// <summary>
    /// Represents the interaction handler revoke failed value.
    /// </summary>
    InteractionHandlerRevokeFailed,
    /// <summary>
    /// Represents the view descriptor revoke failed value.
    /// </summary>
    ViewDescriptorRevokeFailed,
    /// <summary>
    /// Represents the resource dictionary revoke failed value.
    /// </summary>
    ResourceDictionaryRevokeFailed,
    /// <summary>
    /// Represents the resource contribution revoke failed value.
    /// </summary>
    ResourceContributionRevokeFailed,
}
