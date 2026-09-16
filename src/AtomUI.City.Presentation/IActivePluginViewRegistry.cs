namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iactive plugin view registry.
/// </summary>
public interface IActivePluginViewRegistry
{
    /// <summary>
    /// Gets active views.
    /// </summary>
    IReadOnlyList<ActivePluginView> ActiveViews { get; }

    /// <summary>
    /// Executes the track operation.
    /// </summary>
    IActivePluginViewLease Track(ActivePluginView view);

    /// <summary>
    /// Executes the close plugin views async operation.
    /// </summary>
    ValueTask<int> ClosePluginViewsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the close contribution views async operation.
    /// </summary>
    ValueTask<int> CloseContributionViewsAsync(
        string contributionId,
        CancellationToken cancellationToken = default);
}
