namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for icommand authorization source.
/// </summary>
public interface ICommandAuthorizationSource
{
    /// <summary>
    /// Occurs when authorization changed.
    /// </summary>
    event EventHandler<CommandAuthorizationChangedEventArgs>? AuthorizationChanged;

    /// <summary>
    /// Executes the get state async operation.
    /// </summary>
    ValueTask<CommandAuthorizationState> GetStateAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the check execution async operation.
    /// </summary>
    ValueTask<AuthorizationResult> CheckExecutionAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default);
}
