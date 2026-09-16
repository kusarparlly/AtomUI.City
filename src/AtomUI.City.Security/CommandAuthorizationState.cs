namespace AtomUI.City.Security;

/// <summary>
/// Represents command authorization state.
/// </summary>
public sealed class CommandAuthorizationState
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationState</c> type.
    /// </summary>
    public CommandAuthorizationState(
        string commandId,
        bool canExecute,
        bool isVisible,
        CommandUnauthorizedBehavior unauthorizedBehavior,
        AuthorizationResult authorization,
        long revision,
        string? deniedMessageKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(authorization);

        if (!Enum.IsDefined(unauthorizedBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unauthorizedBehavior),
                unauthorizedBehavior,
                "Command unauthorized behavior must be defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (deniedMessageKey is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deniedMessageKey);
        }

        CommandId = commandId;
        CanExecute = canExecute;
        IsVisible = isVisible;
        UnauthorizedBehavior = unauthorizedBehavior;
        Authorization = authorization;
        Revision = revision;
        DeniedMessageKey = deniedMessageKey;
    }

    /// <summary>
    /// Gets command id.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets a value indicating whether can execute.
    /// </summary>
    public bool CanExecute { get; }

    /// <summary>
    /// Gets a value indicating whether is visible.
    /// </summary>
    public bool IsVisible { get; }

    /// <summary>
    /// Gets unauthorized behavior.
    /// </summary>
    public CommandUnauthorizedBehavior UnauthorizedBehavior { get; }

    /// <summary>
    /// Gets authorization.
    /// </summary>
    public AuthorizationResult Authorization { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets denied message key.
    /// </summary>
    public string? DeniedMessageKey { get; }
}
