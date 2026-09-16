namespace AtomUI.City.Security;

/// <summary>
/// Represents command authorization changed event args.
/// </summary>
public sealed class CommandAuthorizationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationChangedEventArgs</c> type.
    /// </summary>
    public CommandAuthorizationChangedEventArgs(
        CommandAuthorizationChangeReason reason,
        long revision,
        string? commandId = null)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Change reason must be defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (commandId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        }

        Reason = reason;
        Revision = revision;
        CommandId = commandId;
    }

    /// <summary>
    /// Gets reason.
    /// </summary>
    public CommandAuthorizationChangeReason Reason { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets command id.
    /// </summary>
    public string? CommandId { get; }
}
