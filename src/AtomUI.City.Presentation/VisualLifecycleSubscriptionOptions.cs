using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents visual lifecycle subscription options.
/// </summary>
public sealed class VisualLifecycleSubscriptionOptions
{
    /// <summary>
    /// Gets or sets window id.
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// Gets or sets outlet name.
    /// </summary>
    public string? OutletName { get; init; }

    /// <summary>
    /// Gets or sets operation id.
    /// </summary>
    public long? OperationId { get; init; }

    /// <summary>
    /// Gets or sets entry id.
    /// </summary>
    public string? EntryId { get; init; }

    /// <summary>
    /// Gets or sets activation scope.
    /// </summary>
    public IActivationScope? ActivationScope { get; init; }
}
