namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines the contract for iactivatable.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    ValueTask ActivateAsync(IActivationScope scope);

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    ValueTask DeactivateAsync();

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    ValueTask DeactivateAsync(CancellationToken cancellationToken);
}
