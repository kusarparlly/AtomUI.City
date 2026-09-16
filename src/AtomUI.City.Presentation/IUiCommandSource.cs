namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iui command source.
/// </summary>
public interface IUiCommandSource
{
    /// <summary>
    /// Occurs when execute requested.
    /// </summary>
    event EventHandler? ExecuteRequested;

    /// <summary>
    /// Gets command parameter.
    /// </summary>
    object? CommandParameter { get; }

    /// <summary>
    /// Executes the apply command state operation.
    /// </summary>
    void ApplyCommandState(UiCommandState state);
}
