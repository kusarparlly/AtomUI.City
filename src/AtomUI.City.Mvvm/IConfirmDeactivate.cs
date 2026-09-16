namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines the contract for iconfirm deactivate.
/// </summary>
public interface IConfirmDeactivate
{
    /// <summary>
    /// Executes the confirm deactivate async operation.
    /// </summary>
    ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken);
}
