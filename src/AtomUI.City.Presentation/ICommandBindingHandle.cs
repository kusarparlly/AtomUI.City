namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for icommand binding handle.
/// </summary>
public interface ICommandBindingHandle : IDisposable
{
    /// <summary>
    /// Executes the refresh async operation.
    /// </summary>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
