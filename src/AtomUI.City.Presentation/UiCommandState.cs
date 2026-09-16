namespace AtomUI.City.Presentation;

/// <summary>
/// Represents ui command state.
/// </summary>
/// <param name="CanExecute">The can execute value.</param>
/// <param name="IsExecuting">The is executing value.</param>
public sealed record UiCommandState(
    bool CanExecute,
    bool IsExecuting);
