namespace AtomUI.City.Presentation;

internal sealed class BoundedSerialExecutionLane
{
    private readonly object _gate = new();
    private readonly Queue<Func<Task>> _pending = [];
    private readonly int _capacity;
    private readonly Action? _becameIdle;
    private bool _running;
    private int _peakPendingCount;
    private long _rejectedCount;

    public BoundedSerialExecutionLane(int capacity, Action? becameIdle = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _becameIdle = becameIdle;
    }

    public PresentationQueueSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new PresentationQueueSnapshot(
                    _capacity,
                    _pending.Count,
                    _running ? 1 : 0,
                    _peakPendingCount,
                    _rejectedCount);
            }
        }
    }

    public bool IsIdle
    {
        get
        {
            lock (_gate)
            {
                return !_running && _pending.Count == 0;
            }
        }
    }

    public bool TrySchedule(Func<Task> work)
    {
        return Schedule(work, bypassCapacity: false);
    }

    public void ScheduleControl(Func<Task> work)
    {
        Schedule(work, bypassCapacity: true);
    }

    private bool Schedule(Func<Task> work, bool bypassCapacity)
    {
        ArgumentNullException.ThrowIfNull(work);

        Func<Task>? first = null;
        lock (_gate)
        {
            if (!_running)
            {
                _running = true;
                first = work;
            }
            else
            {
                if (!bypassCapacity && _pending.Count >= _capacity)
                {
                    _rejectedCount++;
                    return false;
                }

                _pending.Enqueue(work);
                _peakPendingCount = Math.Max(_peakPendingCount, _pending.Count);
            }
        }

        if (first is not null)
        {
            _ = Task.Run(() => RunAsync(first));
        }

        return true;
    }

    private async Task RunAsync(Func<Task> first)
    {
        var current = first;

        while (true)
        {
            try
            {
                await current().ConfigureAwait(false);
            }
            catch
            {
                // Work items own their completion and error reporting. The lane must
                // continue so one faulty item cannot strand all later accepted work.
            }

            lock (_gate)
            {
                if (_pending.Count != 0)
                {
                    current = _pending.Dequeue();
                    continue;
                }

                _running = false;
            }

            _becameIdle?.Invoke();
            return;
        }
    }
}
