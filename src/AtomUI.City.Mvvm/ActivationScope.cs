using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Owns resources and structured asynchronous work for one activation session.
/// </summary>
public sealed class ActivationScope : IActivationScope
{
    private static readonly TimeSpan DefaultTaskDrainTimeout = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<IAsyncDisposable> _asyncDisposables = [];
    private readonly List<IDisposable> _disposables = [];
    private readonly Dictionary<long, TrackedTask> _tasks = [];
    private readonly Dictionary<string, long> _uniqueTasks = new(StringComparer.Ordinal);
    private readonly IHostDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();
    private readonly TimeSpan _taskDrainTimeout;
    private long _nextTaskId;
    private bool _isClosing;
    private bool _isDisposed;
    private Task? _disposeTask;

    /// <summary>
    /// Initializes a new activation scope.
    /// </summary>
    public ActivationScope(
        IHostDiagnostics? diagnostics = null,
        TimeSpan? taskDrainTimeout = null,
        CancellationToken parentCancellationToken = default)
    {
        var configuredTimeout = taskDrainTimeout ?? DefaultTaskDrainTimeout;
        if (configuredTimeout <= TimeSpan.Zero && configuredTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taskDrainTimeout),
                configuredTimeout,
                "Task drain timeout must be positive or infinite.");
        }

        _cancellationTokenSource = parentCancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken)
            : new CancellationTokenSource();
        CancellationToken = _cancellationTokenSource.Token;
        _diagnostics = diagnostics;
        _taskDrainTimeout = configuredTimeout;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public int ActiveTaskCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _tasks.Count;
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        ArgumentNullException.ThrowIfNull(taskFactory);

        return StartCore(taskName, taskKey: null, taskFactory, cancellationToken);
    }

    /// <inheritdoc />
    public Task StartUniqueAsync(
        string taskKey,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskKey);
        ArgumentNullException.ThrowIfNull(taskFactory);

        return StartCore(taskKey, taskKey, taskFactory, cancellationToken);
    }

    /// <inheritdoc />
    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        lock (_syncRoot)
        {
            if (!_isClosing)
            {
                _disposables.Add(disposable);
                return;
            }
        }

        DisposeResource(disposable, failures: null);
    }

    /// <inheritdoc />
    public void AddAsync(IAsyncDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        lock (_syncRoot)
        {
            if (!_isClosing)
            {
                _asyncDisposables.Add(disposable);
                return;
            }
        }

        ObserveLateAsyncDisposal(disposable);
    }

    /// <summary>
    /// Synchronously disposes a scope only when it has no asynchronous work or resources.
    /// </summary>
    public void Dispose()
    {
        IDisposable[] disposables;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_disposeTask is not null || _tasks.Count != 0 || _asyncDisposables.Count != 0)
            {
                throw new InvalidOperationException(
                    "The activation scope owns asynchronous work or resources and must be disposed with DisposeAsync().");
            }

            _isClosing = true;
            disposables = _disposables.ToArray();
            _disposables.Clear();
        }

        var failures = new List<Exception>();

        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            WriteDisposeFailedDiagnostic(exception);
        }

        for (var index = disposables.Length - 1; index >= 0; index--)
        {
            DisposeResource(disposables[index], failures);
        }

        _cancellationTokenSource.Dispose();

        lock (_syncRoot)
        {
            _isDisposed = true;
            _disposeTask = Task.CompletedTask;
        }

        ThrowIfFailures(failures);
    }

    /// <summary>
    /// Asynchronously closes task admission, cancels and drains tasks, then releases resources.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;

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
            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            disposeTask = _disposeTask;
        }

        if (completion is not null)
        {
            _ = RunDisposeCoreAsync(completion);
        }

        return new ValueTask(disposeTask);
    }

    private Task StartCore(
        string taskName,
        string? taskKey,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TrackedTask? trackedTask = null;
        string? rejectionReason = null;

        lock (_syncRoot)
        {
            if (_isClosing || CancellationToken.IsCancellationRequested)
            {
                rejectionReason = _isClosing ? "scope-closing" : "scope-canceled";
            }
            else if (taskKey is not null && _uniqueTasks.ContainsKey(taskKey))
            {
                rejectionReason = "duplicate-key";
            }
            else
            {
                var taskId = ++_nextTaskId;
                var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    CancellationToken,
                    cancellationToken);
                trackedTask = new TrackedTask(taskId, taskName, taskKey, linkedCancellation);
                _tasks.Add(taskId, trackedTask);
                if (taskKey is not null)
                {
                    _uniqueTasks.Add(taskKey, taskId);
                }
            }
        }

        if (rejectionReason is not null)
        {
            WriteTaskRejectedDiagnostic(taskName, taskKey, rejectionReason);
            throw rejectionReason == "duplicate-key"
                ? ActivationTaskRejection.Create(
                    Id,
                    rejectionReason,
                    $"Activation scope '{Id}' already owns an active task with key '{taskKey}'.")
                : ActivationTaskRejection.Create(
                    Id,
                    rejectionReason,
                    $"Activation scope '{Id}' is closing or canceled and rejects new tasks.");
        }

        System.Diagnostics.Debug.Assert(trackedTask is not null);
        _ = RunTrackedTaskAsync(trackedTask, taskFactory);

        return trackedTask.Completion.Task;
    }

    private async Task RunDisposeCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task RunTrackedTaskAsync(
        TrackedTask trackedTask,
        Func<CancellationToken, Task> taskFactory)
    {
        Exception? failure = null;
        var canceled = false;
        var cancellationToken = trackedTask.Cancellation.Token;

        try
        {
            var task = taskFactory(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Activation task factory '{trackedTask.Name}' returned null.");

            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (trackedTask.Cancellation.IsCancellationRequested)
        {
            canceled = true;
        }
        catch (Exception exception)
        {
            WriteTaskFailedDiagnostic(trackedTask, exception);
            failure = exception;
        }
        finally
        {
            lock (_syncRoot)
            {
                _tasks.Remove(trackedTask.Id);
                if (trackedTask.Key is not null)
                {
                    _uniqueTasks.Remove(trackedTask.Key);
                }
            }

            trackedTask.Cancellation.Dispose();
        }

        if (failure is not null)
        {
            trackedTask.Completion.TrySetException(failure);
        }
        else if (canceled)
        {
            trackedTask.Completion.TrySetCanceled(cancellationToken);
        }
        else
        {
            trackedTask.Completion.TrySetResult();
        }
    }

    private async Task DisposeCoreAsync()
    {
        TrackedTask[] tasks;
        IAsyncDisposable[] asyncDisposables;
        IDisposable[] disposables;
        var failures = new List<Exception>();

        lock (_syncRoot)
        {
            tasks = _tasks.Values.ToArray();
            asyncDisposables = _asyncDisposables.ToArray();
            disposables = _disposables.ToArray();
            _asyncDisposables.Clear();
            _disposables.Clear();
        }

        try
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            WriteDisposeFailedDiagnostic(exception);
        }

        if (tasks.Length != 0)
        {
            var drain = Task.WhenAll(tasks.Select(static task => task.Completion.Task));
            try
            {
                if (_taskDrainTimeout == Timeout.InfiniteTimeSpan)
                {
                    await drain.ConfigureAwait(false);
                }
                else
                {
                    await drain.WaitAsync(_taskDrainTimeout).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                WriteTaskDrainTimedOutDiagnostic();
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation is the normal scope shutdown path.
            }
            catch
            {
                if (drain.Exception is not null)
                {
                    failures.AddRange(drain.Exception.Flatten().InnerExceptions);
                }
            }
        }

        for (var index = asyncDisposables.Length - 1; index >= 0; index--)
        {
            await DisposeResourceAsync(asyncDisposables[index], failures).ConfigureAwait(false);
        }

        for (var index = disposables.Length - 1; index >= 0; index--)
        {
            DisposeResource(disposables[index], failures);
        }

        _cancellationTokenSource.Dispose();

        lock (_syncRoot)
        {
            _isDisposed = true;
        }

        ThrowIfFailures(failures);
    }

    private void ObserveLateAsyncDisposal(IAsyncDisposable disposable)
    {
        var disposal = DisposeResourceAsync(disposable, failures: null);
        if (!disposal.IsCompletedSuccessfully)
        {
            _ = disposal.AsTask();
        }
    }

    private void DisposeResource(IDisposable disposable, List<Exception>? failures)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
            WriteDisposeFailedDiagnostic(exception);
        }
    }

    private async ValueTask DisposeResourceAsync(
        IAsyncDisposable disposable,
        List<Exception>? failures)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
            WriteDisposeFailedDiagnostic(exception);
        }
    }

    private void WriteTaskRejectedDiagnostic(string taskName, string? taskKey, string reason)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationTaskRejected,
            $"Activation scope '{Id}' rejected task '{taskName}' ({reason}).",
            HostDiagnosticSeverity.Warning)
        {
            Context = new Dictionary<string, string?>
            {
                ["scopeId"] = Id.ToString(),
                ["taskName"] = taskName,
                ["taskKey"] = taskKey,
                ["reason"] = reason,
            }
        });
    }

    private void WriteTaskFailedDiagnostic(TrackedTask task, Exception exception)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationTaskFailed,
            $"Activation task '{task.Name}' in scope '{Id}' failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["scopeId"] = Id.ToString(),
                ["taskName"] = task.Name,
                ["taskKey"] = task.Key,
                ["exceptionType"] = exception.GetType().FullName,
            }
        });
    }

    private void WriteTaskDrainTimedOutDiagnostic()
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationTaskDrainTimedOut,
            $"Activation scope '{Id}' did not drain all tasks within '{_taskDrainTimeout}'.",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["scopeId"] = Id.ToString(),
                ["activeTaskCount"] = ActiveTaskCount.ToString(),
                ["timeout"] = _taskDrainTimeout.ToString(),
            }
        });
    }

    private void WriteDisposeFailedDiagnostic(Exception exception)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationScopeDisposeFailed,
            $"Activation scope '{Id}' resource disposal failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["scopeId"] = Id.ToString(),
            }
        });
    }

    private static void ThrowIfFailures(List<Exception> failures)
    {
        if (failures.Count != 0)
        {
            throw new AggregateException("Activation scope disposal failed.", failures);
        }
    }

    private sealed class TrackedTask
    {
        public TrackedTask(
            long id,
            string name,
            string? key,
            CancellationTokenSource cancellation)
        {
            Id = id;
            Name = name;
            Key = key;
            Cancellation = cancellation;
            Completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _ = Completion.Task.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        public long Id { get; }

        public string Name { get; }

        public string? Key { get; }

        public CancellationTokenSource Cancellation { get; }

        public TaskCompletionSource Completion { get; }
    }
}
