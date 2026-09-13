namespace AtomUI.City.Security;

public sealed class AccountSessionChangedEventArgs : EventArgs
{
    public AccountSessionChangedEventArgs(
        AccountSessionSnapshot previous,
        AccountSessionSnapshot current,
        Guid operationId)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        OperationId = operationId;
    }

    public AccountSessionSnapshot Previous { get; }

    public AccountSessionSnapshot Current { get; }

    public Guid OperationId { get; }
}
