namespace AtomUI.City.Mvvm;

/// <summary>
/// Describes one execution admitted by a <see cref="CommandExecutionDomain"/>.
/// </summary>
public sealed class CommandExecutionLease
{
    private readonly CommandExecutionDomain _owner;
    private int _isCancellationRequested;

    internal CommandExecutionLease(
        CommandExecutionDomain owner,
        long generation,
        CancellationToken cancellationToken)
    {
        _owner = owner;
        Generation = generation;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the monotonically increasing generation assigned by the domain.
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// Gets the token canceled when this execution is replaced, its owner stops, or the domain closes.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets a value indicating whether this lease still represents the domain's current execution.
    /// </summary>
    public bool IsCurrent => _owner.IsCurrent(this);

    internal bool IsCancellationRequested => Volatile.Read(ref _isCancellationRequested) != 0;

    internal void MarkCancellationRequested()
    {
        Interlocked.Exchange(ref _isCancellationRequested, 1);
    }
}
