namespace AtomUI.City.Presentation;

/// <summary>
/// Represents active plugin view.
/// </summary>
public sealed class ActivePluginView
{
    /// <summary>
    /// Initializes a new instance of the <c>ActivePluginView</c> type.
    /// </summary>
    public ActivePluginView(
        string pluginId,
        IRouteOutlet outlet,
        BoundViewHandle handle,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(outlet);
        ArgumentNullException.ThrowIfNull(handle);

        PluginId = pluginId;
        Outlet = outlet;
        Handle = handle;
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

    /// <summary>
    /// Gets outlet.
    /// </summary>
    public IRouteOutlet Outlet { get; }

    /// <summary>
    /// Gets handle.
    /// </summary>
    public BoundViewHandle Handle { get; }
}
