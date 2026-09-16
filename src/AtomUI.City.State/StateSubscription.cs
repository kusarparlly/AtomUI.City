using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.State;

internal sealed class StateSubscription : IStateSubscription
{
    private readonly Action<StateChangedEventArgs> _handler;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly StateSubscriptionOptions _options;
    private readonly Queue<StateChangedEventArgs> _queuedNotifications = [];
    private readonly object _queueSyncRoot = new();
    private readonly object _executionSyncRoot = new();
    private int _disposed;
    private bool _isProcessingQueue;

    public StateSubscription(
        Action<StateChangedEventArgs> handler,
        StateSubscriptionOptions options,
        IHostDiagnostics? diagnostics = null)
    {
        _handler = handler;
        _options = options;
        _diagnostics = diagnostics;
    }

    public void Notify(StateChangedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            switch (_options.DispatchPolicy)
            {
                case StateDispatchPolicy.Dispatcher:
                case StateDispatchPolicy.Background:
                case StateDispatchPolicy.Queued:
                    Enqueue(args);
                    break;
                default:
                    NotifyImmediate(args);
                    break;
            }
        }
        catch (Exception exception)
        {
            WriteHandlerFailedDiagnostic(args, exception);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_queueSyncRoot)
        {
            _queuedNotifications.Clear();
            _isProcessingQueue = false;
        }

        lock (_executionSyncRoot)
        {
        }
    }

    private void Enqueue(StateChangedEventArgs args)
    {
        var shouldStartProcessing = false;
        var queueOverflowed = false;

        lock (_queueSyncRoot)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (_queuedNotifications.Count == _options.MaxPendingNotifications)
            {
                _queuedNotifications.Dequeue();
                queueOverflowed = true;
            }

            _queuedNotifications.Enqueue(args);

            if (!_isProcessingQueue)
            {
                _isProcessingQueue = true;
                shouldStartProcessing = true;
            }
        }

        if (queueOverflowed)
        {
            WriteQueueOverflowDiagnostic(args);
        }

        if (shouldStartProcessing)
        {
            if (_options.DispatchPolicy == StateDispatchPolicy.Dispatcher)
            {
                _ = ProcessQueueAsync();
            }
            else
            {
                _ = Task.Run(ProcessQueueAsync);
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            StateChangedEventArgs args;

            lock (_queueSyncRoot)
            {
                if (Volatile.Read(ref _disposed) != 0 || _queuedNotifications.Count == 0)
                {
                    _isProcessingQueue = false;
                    return;
                }

                args = _queuedNotifications.Dequeue();
            }

            if (_options.DispatchPolicy == StateDispatchPolicy.Dispatcher)
            {
                await DispatchQueuedAsync(args).ConfigureAwait(false);
            }
            else
            {
                NotifyQueued(args);
            }
        }
    }

    private async Task DispatchQueuedAsync(StateChangedEventArgs args)
    {
        try
        {
            await _options.UiDispatcher!.PostAsync(
                _ =>
                {
                    NotifyDispatched(args);
                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteHandlerFailedDiagnostic(args, exception);
        }
    }

    private void NotifyQueued(StateChangedEventArgs args)
    {
        lock (_executionSyncRoot)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            try
            {
                _handler(args);
            }
            catch (Exception exception)
            {
                WriteHandlerFailedDiagnostic(args, exception);
            }
        }
    }

    private void NotifyImmediate(StateChangedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _handler(args);
        }
        catch (Exception exception)
        {
            WriteHandlerFailedDiagnostic(args, exception);
        }
    }

    private void NotifyDispatched(StateChangedEventArgs args)
    {
        lock (_executionSyncRoot)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            try
            {
                _handler(args);
            }
            catch (Exception exception)
            {
                WriteHandlerFailedDiagnostic(args, exception);
            }
        }
    }

    private void WriteQueueOverflowDiagnostic(StateChangedEventArgs args)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.SubscriptionQueueOverflow,
            $"State subscription queue reached its capacity of {_options.MaxPendingNotifications}; the oldest notification was discarded.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("dispatchPolicy", _options.DispatchPolicy.ToString()),
                ("maxPendingNotifications", StateDiagnosticContext.Version(_options.MaxPendingNotifications)),
                ("version", StateDiagnosticContext.Version(args.Version)))
        });
    }

    private void WriteHandlerFailedDiagnostic(
        StateChangedEventArgs args,
        Exception exception)
    {
        var context = new List<(string Key, string? Value)>
        {
            ("dispatchPolicy", _options.DispatchPolicy.ToString()),
            ("version", StateDiagnosticContext.Version(args.Version))
        };

        if (_options.UiDispatcher is not null)
        {
            context.Add((
                "dispatcherType",
                StateDiagnosticContext.TypeName(_options.UiDispatcher.GetType())));
        }

        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.SubscriptionHandlerFailed,
            $"State subscription handler failed at version {args.Version}: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = StateDiagnosticContext.Create([.. context])
        });
    }
}

/// <summary>
/// Represents state subscription options.
/// </summary>
public sealed class StateSubscriptionOptions
{
    private StateSubscriptionOptions(
        StateDispatchPolicy dispatchPolicy,
        IUiDispatcher? dispatcher,
        int maxPendingNotifications)
    {
        if (maxPendingNotifications < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPendingNotifications),
                maxPendingNotifications,
                "Pending notification capacity must be greater than 0.");
        }

        DispatchPolicy = dispatchPolicy;
        UiDispatcher = dispatcher;
        MaxPendingNotifications = maxPendingNotifications;
    }

    /// <summary>
    /// Gets immediate.
    /// </summary>
    public static StateSubscriptionOptions Immediate { get; } = new(
        StateDispatchPolicy.Immediate,
        dispatcher: null,
        maxPendingNotifications: 1);

    /// <summary>
    /// Gets dispatch policy.
    /// </summary>
    public StateDispatchPolicy DispatchPolicy { get; }

    /// <summary>
    /// Gets ui dispatcher.
    /// </summary>
    public IUiDispatcher? UiDispatcher { get; }

    /// <summary>
    /// Gets max pending notifications.
    /// </summary>
    public int MaxPendingNotifications { get; }

    /// <summary>
    /// Executes the dispatcher operation.
    /// </summary>
    public static StateSubscriptionOptions Dispatcher(IUiDispatcher dispatcher)
    {
        return Dispatcher(dispatcher, maxPendingNotifications: 1024);
    }

    /// <summary>
    /// Executes the dispatcher operation.
    /// </summary>
    public static StateSubscriptionOptions Dispatcher(
        IUiDispatcher dispatcher,
        int maxPendingNotifications)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return new StateSubscriptionOptions(
            StateDispatchPolicy.Dispatcher,
            dispatcher,
            maxPendingNotifications);
    }

    /// <summary>
    /// Executes the background operation.
    /// </summary>
    public static StateSubscriptionOptions Background()
    {
        return Background(maxPendingNotifications: 1024);
    }

    /// <summary>
    /// Executes the background operation.
    /// </summary>
    public static StateSubscriptionOptions Background(int maxPendingNotifications)
    {
        return new StateSubscriptionOptions(
            StateDispatchPolicy.Background,
            dispatcher: null,
            maxPendingNotifications);
    }

    /// <summary>
    /// Executes the queued operation.
    /// </summary>
    public static StateSubscriptionOptions Queued()
    {
        return Queued(maxPendingNotifications: 1024);
    }

    /// <summary>
    /// Executes the queued operation.
    /// </summary>
    public static StateSubscriptionOptions Queued(int maxPendingNotifications)
    {
        return new StateSubscriptionOptions(
            StateDispatchPolicy.Queued,
            dispatcher: null,
            maxPendingNotifications);
    }
}
