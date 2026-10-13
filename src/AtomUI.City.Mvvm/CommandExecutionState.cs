namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents command execution state.
/// </summary>
public sealed class CommandExecutionState
{
    private readonly object _gate = new();
    private int _activeExecutionCount;
    private OperationExecutionResult? _lastResult;
    private OperationExecutionResult? _lastRejectedResult;
    private int _rejectedExecutionCount;
    private Exception? _lastError;
    private CancellationToken _cancellationToken;

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
    /// Gets a value indicating whether one or more executions are active.
    /// </summary>
    public bool IsExecuting => ActiveExecutionCount != 0;

    /// <summary>
    /// Gets the number of currently active executions.
    /// </summary>
    public int ActiveExecutionCount
    {
        get
        {
            lock (_gate)
            {
                return _activeExecutionCount;
            }
        }
    }

    /// <summary>
    /// Gets or sets last result.
    /// </summary>
    public OperationExecutionResult? LastResult
    {
        get
        {
            lock (_gate)
            {
                return _lastResult;
            }
        }
    }

    /// <summary>
    /// Gets or sets last rejected result.
    /// </summary>
    public OperationExecutionResult? LastRejectedResult
    {
        get
        {
            lock (_gate)
            {
                return _lastRejectedResult;
            }
        }
    }

    /// <summary>
    /// Gets or sets rejected execution count.
    /// </summary>
    public int RejectedExecutionCount
    {
        get
        {
            lock (_gate)
            {
                return _rejectedExecutionCount;
            }
        }
    }

    /// <summary>
    /// Gets or sets last error.
    /// </summary>
    public Exception? LastError
    {
        get
        {
            lock (_gate)
            {
                return _lastError;
            }
        }
    }

    /// <summary>
    /// Gets or sets cancellation token.
    /// </summary>
    public CancellationToken CancellationToken
    {
        get
        {
            lock (_gate)
            {
                return _cancellationToken;
            }
        }
    }

    internal bool TryBegin(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_activeExecutionCount != 0)
            {
                return false;
            }

            _activeExecutionCount++;
            _lastResult = null;
            _lastError = null;
            _cancellationToken = cancellationToken;

            return true;
        }
    }

    internal void Begin(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _activeExecutionCount++;
            _lastResult = null;
            _lastError = null;
            _cancellationToken = cancellationToken;
        }
    }

    internal void Complete(OperationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _lastResult = result;
            _lastError = result.Error;
            if (_activeExecutionCount > 0)
            {
                _activeExecutionCount--;
            }
        }
    }

    internal void Reject(OperationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _lastRejectedResult = result;
            _rejectedExecutionCount++;
        }
    }
}
