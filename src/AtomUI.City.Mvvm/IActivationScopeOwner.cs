namespace AtomUI.City.Mvvm;

/// <summary>
/// Exposes the current activation session of an object without assigning a presentation role.
/// </summary>
public interface IActivationScopeOwner
{
    /// <summary>
    /// Occurs when the current activation state or scope changes.
    /// </summary>
    event EventHandler? ActivationStateChanged;

    /// <summary>
    /// Gets the current activation state.
    /// </summary>
    ActivationState ActivationState { get; }

    /// <summary>
    /// Gets a value indicating whether the owner is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the current activation scope, if one is active.
    /// </summary>
    IActivationScope? CurrentActivationScope { get; }
}
