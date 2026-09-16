namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iview data context aware.
/// </summary>
public interface IViewDataContextAware
{
    /// <summary>
    /// Gets or sets data context.
    /// </summary>
    object? DataContext { get; set; }
}
