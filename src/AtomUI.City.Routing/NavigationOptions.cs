namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation options.
/// </summary>
public sealed class NavigationOptions
{
    /// <summary>
    /// Gets default.
    /// </summary>
    public static NavigationOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets mode.
    /// </summary>
    public NavigationMode Mode { get; init; } = NavigationMode.Push;

    /// <summary>
    /// Gets or sets history behavior.
    /// </summary>
    public NavigationHistoryBehavior HistoryBehavior { get; init; } = NavigationHistoryBehavior.Record;

    /// <summary>
    /// Gets or sets concurrency policy.
    /// </summary>
    public NavigationConcurrencyPolicy ConcurrencyPolicy { get; init; } = NavigationConcurrencyPolicy.CancelPrevious;

    /// <summary>
    /// Gets or sets outlet name.
    /// </summary>
    public string OutletName { get; init; } = "primary";

    internal bool RestoreState { get; init; }

    /// <summary>
    /// Gets or sets force reload.
    /// </summary>
    public bool ForceReload { get; init; }

    /// <summary>
    /// Gets or sets allow redirect.
    /// </summary>
    public bool AllowRedirect { get; init; } = true;

    /// <summary>
    /// Gets or sets timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets journal capacity.
    /// </summary>
    public int JournalCapacity { get; init; } = 64;
}
