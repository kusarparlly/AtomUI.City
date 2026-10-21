using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Coordinates latest-wins execution ownership across explicitly associated commands.
/// </summary>
public sealed class CommandExecutionDomain : IAsyncDisposable
{
    private static readonly TimeSpan DefaultExecutionDrainTimeout = TimeSpan.FromSeconds(30);

    private readonly Dictionary<long, ExecutionEntry> _activeExecutions = [];
    private readonly IHostDiagnostics? _diagnostics;
    private readonly TimeSpan _executionDrainTimeout;
    private readonly object _syncRoot = new();
    private ExecutionEntry? _currentExecution;
    private Task? _disposeTask;
    private long _nextGeneration;
    private bool _isClosing;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new command execution domain.
    /// </summary>
    public CommandExecutionDomain(
        IHostDiagnostics? diagnostics = null,
        TimeSpan? executionDrainTimeout = null)
    {
        var configuredTimeout = executionDrainTimeout ?? DefaultExecutionDrainTimeout;
        if (configuredTimeout <= TimeSpan.Zero && configuredTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionDrainTimeout),
                configuredTimeout,
                "Execution drain timeout must be positive or infinite.");
        }

        _diagnostics = diagnostics;
        _executionDrainTimeout = configuredTimeout;
    }

    /// <summary>
    /// Gets the stable identifier for this domain.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the most recently assigned generation, or zero before the first execution.
    /// </summary>
    public long CurrentGeneration
    {
        get
        {
            lock (_syncRoot)
            {
                return _nextGeneration;
            }
        }
    }

    /// <summary>
    /// Gets the number of executions that have not yet completed.
    /// </summary>
    public int ActiveExecutionCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeExecutions.Count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the domain no longer accepts executions.
    /// </summary>
    public bool IsClosing
    {
        get
        {
            lock (_syncRoot)
            {
                return _isClosing;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether domain disposal has completed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _isDisposed;
            }
        }
    }

    /// <summary>
    /// Permanently closes the domain, cancels active executions, and waits for them to drain.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;
        ExecutionEntry[] executions = [];

        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_isDisposed)
            {
                return ValueTask.CompletedTask;
            }

            _isClosing = true;
            _currentExecution = null;
            executions = _activeExecutions.Values.ToArray();
            foreach (var execution in executions)
            {
                execution.Lease.MarkCancellationRequested();
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            disposeTask = _disposeTask;
        }

        foreach (var execution in executions)
        {
            RequestCancellation(execution);
        }

        _ = DisposeCoreAsync(executions, completion);
        return new ValueTask(disposeTask);
    }

    internal bool IsCurrent(CommandExecutionLease lease)
    {
        lock (_syncRoot)
        {
            return !_isClosing
                && ReferenceEquals(_currentExecution?.Lease, lease);
        }
    }

    internal Task RunLatestAsync(
        Func<CommandExecutionLease, CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execute);
        cancellationToken.ThrowIfCancellationRequested();

        ExecutionEntry entry;
        ExecutionEntry? previous;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CommandExecutionDomain));
            }

            if (_isClosing)
            {
                throw new InvalidOperationException("The command execution domain is closing.");
            }

            var generation = checked(++_nextGeneration);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var lease = new CommandExecutionLease(this, generation, cancellation.Token);
            entry = new ExecutionEntry(lease, cancellation);
            _activeExecutions.Add(generation, entry);
            previous = _currentExecution;
            previous?.Lease.MarkCancellationRequested();
            _currentExecution = entry;
        }

        if (previous is not null)
        {
            RequestCancellation(previous);
        }

        return RunExecutionAsync(entry, execute);
    }

    private async Task RunExecutionAsync(
        ExecutionEntry entry,
        Func<CommandExecutionLease, CancellationToken, Task> execute)
    {
        try
        {
            await execute(entry.Lease, entry.Lease.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeExecutions.Remove(entry.Lease.Generation);
                if (ReferenceEquals(_currentExecution, entry))
                {
                    _currentExecution = null;
                }
            }

            await entry.DisposeAsync().ConfigureAwait(false);
            entry.Completion.TrySetResult();
        }
    }

    private void RequestCancellation(ExecutionEntry entry)
    {
        var cancellationTask = entry.RequestCancellation(out var firstRequest);
        if (firstRequest && !cancellationTask.IsCompletedSuccessfully)
        {
            _ = ObserveCancellationAsync(entry, cancellationTask);
        }
    }

    private async Task ObserveCancellationAsync(ExecutionEntry entry, Task cancellationTask)
    {
        try
        {
            await cancellationTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
                MvvmDiagnosticIds.CommandExecutionDomainCancellationFailed,
                $"Command execution domain '{Id}' could not cancel generation '{entry.Lease.Generation}': {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["domainId"] = Id.ToString(),
                    ["generation"] = entry.Lease.Generation.ToString(),
                    ["exceptionType"] = exception.GetType().FullName,
                }
            });
        }
    }

    private async Task DisposeCoreAsync(
        IReadOnlyCollection<ExecutionEntry> executions,
        TaskCompletionSource completion)
    {
        try
        {
            if (executions.Count != 0)
            {
                var drain = Task.WhenAll(executions.Select(static execution => execution.Completion.Task));
                try
                {
                    if (_executionDrainTimeout == Timeout.InfiniteTimeSpan)
                    {
                        await drain.ConfigureAwait(false);
                    }
                    else
                    {
                        await drain.WaitAsync(_executionDrainTimeout).ConfigureAwait(false);
                    }
                }
                catch (TimeoutException)
                {
                    WriteDrainTimedOutDiagnostic();
                }
            }

            lock (_syncRoot)
            {
                _isDisposed = true;
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void WriteDrainTimedOutDiagnostic()
    {
        var activeExecutionCount = ActiveExecutionCount;
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.CommandExecutionDomainDrainTimedOut,
            $"Command execution domain '{Id}' still had '{activeExecutionCount}' active execution(s) after '{_executionDrainTimeout}'.",
            HostDiagnosticSeverity.Warning)
        {
            Context = new Dictionary<string, string?>
            {
                ["domainId"] = Id.ToString(),
                ["activeExecutionCount"] = activeExecutionCount.ToString(),
                ["timeout"] = _executionDrainTimeout.ToString(),
            }
        });
    }

    private sealed class ExecutionEntry
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly object _syncRoot = new();
        private Task? _cancellationTask;
        private bool _disposeStarted;

        public ExecutionEntry(
            CommandExecutionLease lease,
            CancellationTokenSource cancellation)
        {
            Lease = lease;
            _cancellation = cancellation;
        }

        public CommandExecutionLease Lease { get; }

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestCancellation(out bool firstRequest)
        {
            Lease.MarkCancellationRequested();
            lock (_syncRoot)
            {
                if (_disposeStarted)
                {
                    firstRequest = false;
                    return Task.CompletedTask;
                }

                if (_cancellationTask is not null)
                {
                    firstRequest = false;
                    return _cancellationTask;
                }

                firstRequest = true;
                try
                {
                    _cancellationTask = _cancellation.CancelAsync();
                }
                catch (Exception exception)
                {
                    _cancellationTask = Task.FromException(exception);
                }

                return _cancellationTask;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task? cancellationTask;
            lock (_syncRoot)
            {
                _disposeStarted = true;
                cancellationTask = _cancellationTask;
            }

            if (cancellationTask is not null)
            {
                try
                {
                    await cancellationTask.ConfigureAwait(false);
                }
                catch
                {
                    // The domain observes and diagnoses cancellation callback failures.
                }
            }

            _cancellation.Dispose();
        }
    }
}
