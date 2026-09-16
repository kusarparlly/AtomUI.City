namespace AtomUI.City.Security;

/// <summary>
/// Represents account session changed event args.
/// </summary>
public sealed class AccountSessionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>AccountSessionChangedEventArgs</c> type.
    /// </summary>
    public AccountSessionChangedEventArgs(
        AccountSessionSnapshot previous,
        AccountSessionSnapshot current,
        Guid operationId)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        OperationId = operationId;
    }

    /// <summary>
    /// Gets previous.
    /// </summary>
    public AccountSessionSnapshot Previous { get; }

    /// <summary>
    /// Gets current.
    /// </summary>
    public AccountSessionSnapshot Current { get; }

    /// <summary>
    /// Gets operation id.
    /// </summary>
    public Guid OperationId { get; }
}
