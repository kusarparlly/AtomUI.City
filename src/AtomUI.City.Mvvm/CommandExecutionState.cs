namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents command execution state.
/// </summary>
public sealed class CommandExecutionState
{
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutionState"/> type.
    /// </summary>
    public CommandExecutionState(
        string? commandName = null,
        Type? ownerType = null)
    {
        CommandName = commandName;
        OwnerType = ownerType;
    }

    /// <summary>
    /// Gets command name.
    /// </summary>
    public string? CommandName { get; }

    /// <summary>
    /// Gets owner type.
    /// </summary>
    public Type? OwnerType { get; }

    /// <summary>
    /// Gets or sets is executing.
    /// </summary>
    public bool IsExecuting { get; private set; }

    /// <summary>
    /// Gets or sets last result.
    /// </summary>
    public OperationResult? LastResult { get; private set; }

    /// <summary>
    /// Gets or sets last rejected result.
    /// </summary>
    public OperationResult? LastRejectedResult { get; private set; }

    /// <summary>
    /// Gets or sets rejected execution count.
    /// </summary>
    public int RejectedExecutionCount { get; private set; }

    /// <summary>
    /// Gets or sets last error.
    /// </summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Gets or sets cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; }

    internal bool TryBegin(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (IsExecuting)
            {
                return false;
            }

            IsExecuting = true;
            LastResult = null;
            LastError = null;
            CancellationToken = cancellationToken;

            return true;
        }
    }

    internal void Complete(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            LastResult = result;
            LastError = result.Error;
            IsExecuting = false;
        }
    }

    internal void Reject(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            LastRejectedResult = result;
            RejectedExecutionCount++;
        }
    }
}
