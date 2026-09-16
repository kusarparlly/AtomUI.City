namespace AtomUI.City.Testing;

/// <summary>
/// Represents deterministic scheduler.
/// </summary>
public sealed class DeterministicScheduler : IDisposable
{
    private readonly PriorityQueue<DeterministicScheduledWorkItem, ScheduledWorkPriority> _scheduledWork = new();
    private readonly TestDiagnostics _diagnostics;
    private long _nextWorkItemId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>DeterministicScheduler</c> type.
    /// </summary>
    public DeterministicScheduler()
        : this(new TestDiagnostics())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>DeterministicScheduler</c> type.
    /// </summary>
    public DeterministicScheduler(TestDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Gets or sets now.
    /// </summary>
    public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets scheduled count.
    /// </summary>
    public int ScheduledCount => _scheduledWork.Count;

    /// <summary>
    /// Executes the schedule operation.
    /// </summary>
    public DeterministicScheduledWorkItem Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        var scheduledWorkItem = new DeterministicScheduledWorkItem(
            Interlocked.Increment(ref _nextWorkItemId),
            callback,
            Now.Add(delay));

        _scheduledWork.Enqueue(
            scheduledWorkItem,
            new ScheduledWorkPriority(scheduledWorkItem.DueAt, scheduledWorkItem.Id));

        return scheduledWorkItem;
    }

    /// <summary>
    /// Executes the advance by operation.
    /// </summary>
    public void AdvanceBy(TimeSpan duration)
    {
        ThrowIfDisposed();

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");
        }

        Now = Now.Add(duration);
        RunDueWork();
    }

    /// <summary>
    /// Executes the run due work operation.
    /// </summary>
    public void RunDueWork()
    {
        ThrowIfDisposed();

        while (_scheduledWork.TryPeek(out var workItem, out var priority) && priority.DueAt <= Now)
        {
            _scheduledWork.Dequeue();
            ExecuteWorkItem(workItem);
        }
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

        _disposed = true;

        while (_scheduledWork.TryDequeue(out var workItem, out _))
        {
            workItem.Cancel();
        }
    }

    private void ExecuteWorkItem(DeterministicScheduledWorkItem workItem)
    {
        try
        {
            workItem.Execute();
        }
        catch (Exception exception)
        {
            _diagnostics.Add(
                "AUCTEST201",
                $"Scheduled work item {workItem.Id} failed at {workItem.DueAt:o}: {exception.Message}");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeterministicScheduler));
        }
    }

    private readonly record struct ScheduledWorkPriority(DateTimeOffset DueAt, long Id)
        : IComparable<ScheduledWorkPriority>
    {
        public int CompareTo(ScheduledWorkPriority other)
        {
            var dueAtComparison = DueAt.CompareTo(other.DueAt);

            return dueAtComparison != 0 ? dueAtComparison : Id.CompareTo(other.Id);
        }
    }
}

/// <summary>
/// Represents deterministic scheduled work item.
/// </summary>
public sealed class DeterministicScheduledWorkItem
{
    private readonly Action _callback;

    internal DeterministicScheduledWorkItem(long id, Action callback, DateTimeOffset dueAt)
    {
        Id = id;
        _callback = callback;
        DueAt = dueAt;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets due at.
    /// </summary>
    public DateTimeOffset DueAt { get; }

    /// <summary>
    /// Gets or sets is canceled.
    /// </summary>
    public bool IsCanceled { get; private set; }

    /// <summary>
    /// Gets or sets is completed.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets or sets is faulted.
    /// </summary>
    public bool IsFaulted { get; private set; }

    /// <summary>
    /// Gets or sets exception.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Executes the cancel operation.
    /// </summary>
    public void Cancel()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCanceled = true;
    }

    internal void Execute()
    {
        if (IsCompleted)
        {
            return;
        }

        if (IsCanceled)
        {
            IsCompleted = true;

            return;
        }

        try
        {
            _callback();
            IsCompleted = true;
        }
        catch (Exception exception)
        {
            Exception = exception;
            IsFaulted = true;
            IsCompleted = true;

            throw;
        }
    }
}
