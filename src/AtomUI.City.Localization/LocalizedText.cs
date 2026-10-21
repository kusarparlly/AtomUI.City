using System.Globalization;

namespace AtomUI.City.Localization;

internal sealed class LocalizedText : ILocalizedText
{
    private static readonly AsyncLocal<NotificationExecution?> CurrentNotification = new();
    private readonly LocalizationService _owner;
    private readonly LocalizationLookupContext _context;
    private readonly object _gate = new();
    private LocalizedString _current;
    private Task _refreshTail = Task.CompletedTask;
    private long _revision;
    private int _activeNotifications;
    private bool _disposed;

    private LocalizedText(
        LocalizationService owner,
        LocalizationLookupContext context,
        LocalizedString current,
        long revision)
    {
        _owner = owner;
        _context = context;
        _current = current;
        _revision = revision;
    }

    public event EventHandler<LocalizedTextChangedEventArgs>? Changed;

    public string Key
    {
        get
        {
            lock (_gate)
            {
                return _current.Key;
            }
        }
    }

    public string Value
    {
        get
        {
            lock (_gate)
            {
                return _current.Value;
            }
        }
    }

    public CultureInfo Culture
    {
        get
        {
            lock (_gate)
            {
                return _current.Culture;
            }
        }
    }

    public long Revision
    {
        get
        {
            lock (_gate)
            {
                return _revision;
            }
        }
    }

    public bool IsFallback
    {
        get
        {
            lock (_gate)
            {
                return _current.IsFallback;
            }
        }
    }

    public bool IsMissing
    {
        get
        {
            lock (_gate)
            {
                return _current.IsMissing;
            }
        }
    }

    public static async ValueTask<LocalizedText> CreateAsync(
        LocalizationService owner,
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(context);

        LocalizedString current;
        long revision;
        do
        {
            revision = owner.CultureRevision;
            current = await owner.GetStringAsync(key, context, cancellationToken).ConfigureAwait(false);
        }
        while (revision != owner.CultureRevision);

        return new LocalizedText(owner, context, current, revision);
    }

    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentNotification.Value is { IsActive: true } notification
            && ReferenceEquals(notification.Owner, this))
        {
            return ValueTask.CompletedTask;
        }

        Task predecessor;
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            predecessor = _refreshTail;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _refreshTail = completion.Task;
        }

        _ = Task.Run(() => CompleteRefreshAfterAsync(predecessor, cancellationToken, completion));
        return new ValueTask(completion.Task);
    }

    private async Task CompleteRefreshAfterAsync(
        Task predecessor,
        CancellationToken cancellationToken,
        TaskCompletionSource completion)
    {
        try
        {
            await RunRefreshAfterAsync(predecessor, cancellationToken).ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task RunRefreshAfterAsync(
        Task predecessor,
        CancellationToken cancellationToken)
    {
        try
        {
            await predecessor.ConfigureAwait(false);
        }
        catch
        {
            // A failed refresh does not poison later refresh requests.
        }

        string key;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            key = _current.Key;
        }

        LocalizedString next;
        long nextRevision;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            nextRevision = _owner.CultureRevision;
            next = await _owner.GetStringAsync(key, _context, cancellationToken).ConfigureAwait(false);
        }
        while (nextRevision != _owner.CultureRevision);

        LocalizedTextChangedEventArgs args;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (IsSameText(_current, next) && _revision == nextRevision)
            {
                return;
            }

            _current = next;
            _revision = nextRevision;
            args = new LocalizedTextChangedEventArgs(next, nextRevision);
        }

        OnChanged(args);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (CurrentNotification.Value is not { IsActive: true } notification
                || !ReferenceEquals(notification.Owner, this))
            {
                while (_activeNotifications > 0)
                {
                    Monitor.Wait(_gate);
                }
            }
        }

        _owner.UnregisterLocalizedText(this);
    }

    private void OnChanged(LocalizedTextChangedEventArgs args)
    {
        EventHandler<LocalizedTextChangedEventArgs>? handlers;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            handlers = Changed;
            if (handlers is null)
            {
                return;
            }

            _activeNotifications++;
        }

        var previous = CurrentNotification.Value;
        var execution = new NotificationExecution(this);
        CurrentNotification.Value = execution;
        try
        {
            foreach (EventHandler<LocalizedTextChangedEventArgs> handler in handlers.GetInvocationList())
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        break;
                    }
                }

                try
                {
                    handler(this, args);
                }
                catch (Exception exception)
                {
                    _owner.WriteTextRefreshFailed(Key, exception);
                }
            }
        }
        finally
        {
            execution.Deactivate();
            CurrentNotification.Value = previous;
            lock (_gate)
            {
                _activeNotifications--;
                Monitor.PulseAll(_gate);
            }
        }
    }

    private static bool IsSameText(LocalizedString left, LocalizedString right)
    {
        return string.Equals(left.Key, right.Key, StringComparison.Ordinal)
            && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && string.Equals(left.Culture.Name, right.Culture.Name, StringComparison.OrdinalIgnoreCase)
            && left.IsFallback == right.IsFallback
            && left.IsMissing == right.IsMissing;
    }

    private sealed class NotificationExecution(LocalizedText owner)
    {
        private int _active = 1;

        public LocalizedText Owner { get; } = owner;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Deactivate() => Volatile.Write(ref _active, 0);
    }
}
