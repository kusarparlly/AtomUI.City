using AtomUI.City.Core.Threading;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents fake ui dispatcher.
/// </summary>
public sealed class FakeUiDispatcher : IUiDispatcher, IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<FakeUiWorkItem> _workItems = new();
    private readonly TestDiagnostics _diagnostics;
    private readonly AsyncLocal<int> _uiThreadDepth = new();
    private long _nextWorkItemId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>FakeUiDispatcher</c> type.
    /// </summary>
    public FakeUiDispatcher()
        : this(new TestDiagnostics())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>FakeUiDispatcher</c> type.
    /// </summary>
    public FakeUiDispatcher(TestDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Represents the pending count value.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _workItems.Count;
            }
        }
    }

    /// <summary>
    /// Executes the check access operation.
    /// </summary>
    public bool CheckAccess()
    {
        return _uiThreadDepth.Value > 0;
    }

    /// <summary>
    /// Executes the post operation.
    /// </summary>
    public FakeUiWorkItem Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return Enqueue(_ =>
        {
            callback();

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Executes the drain operation.
    /// </summary>
    public void Drain()
    {
        ThrowIfDisposed();

        while (TryDequeue(out var workItem))
        {
            ExecuteWorkItem(workItem);
        }
    }

    /// <summary>
    /// Executes the invoke async operation.
    /// </summary>
    public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        RunWithUiAccess(callback);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the invoke async&lt;t&gt; operation.
    /// </summary>
    public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(RunWithUiAccess(callback));
    }

    /// <summary>
    /// Executes the post async operation.
    /// </summary>
    public ValueTask PostAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        Enqueue(callback, cancellationToken);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            while (_workItems.TryDequeue(out var workItem))
            {
                workItem.Cancel();
            }
        }
    }

    private FakeUiWorkItem Enqueue(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var workItem = new FakeUiWorkItem(
            Interlocked.Increment(ref _nextWorkItemId),
            callback,
            cancellationToken);

        lock (_gate)
        {
            ThrowIfDisposed();
            _workItems.Enqueue(workItem);
        }

        return workItem;
    }

    private bool TryDequeue(out FakeUiWorkItem workItem)
    {
        lock (_gate)
        {
            return _workItems.TryDequeue(out workItem!);
        }
    }

    private void ExecuteWorkItem(FakeUiWorkItem workItem)
    {
        try
        {
            RunWithUiAccess(workItem.Execute);
        }
        catch (Exception exception)
        {
            _diagnostics.Add(
                "AUCTEST101",
                $"Fake UI work item {workItem.Id} failed: {exception.Message}");
        }
    }

    private void RunWithUiAccess(Action callback)
    {
        _uiThreadDepth.Value++;

        try
        {
            callback();
        }
        finally
        {
            _uiThreadDepth.Value--;
        }
    }

    private T RunWithUiAccess<T>(Func<T> callback)
    {
        _uiThreadDepth.Value++;

        try
        {
            return callback();
        }
        finally
        {
            _uiThreadDepth.Value--;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FakeUiDispatcher));
        }
    }
}
