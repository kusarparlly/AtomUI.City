namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation plugin unload coordinator.
/// </summary>
public interface IPresentationPluginUnloadCoordinator
{
    /// <summary>
    /// Executes the cleanup async operation.
    /// </summary>
    ValueTask<PresentationPluginUnloadResult> CleanupAsync(
        PresentationPluginUnloadRequest request,
        CancellationToken cancellationToken = default);
}
