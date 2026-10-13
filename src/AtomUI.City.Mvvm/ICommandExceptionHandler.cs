namespace AtomUI.City.Mvvm;

/// <summary>
/// Handles command exceptions raised through the non-awaitable ICommand.Execute path.
/// </summary>
public interface ICommandExceptionHandler
{
    /// <summary>
    /// Handles the original command exception exactly once.
    /// </summary>
    ValueTask HandleAsync(
        CommandExceptionContext context,
        CancellationToken cancellationToken = default);
}
