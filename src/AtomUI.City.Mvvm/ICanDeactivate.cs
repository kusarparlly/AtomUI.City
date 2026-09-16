namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines the contract for ican deactivate.
/// </summary>
public interface ICanDeactivate
{
    /// <summary>
    /// Executes the can deactivate async operation.
    /// </summary>
    ValueTask<DeactivationResult> CanDeactivateAsync(CancellationToken cancellationToken);
}
