using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Presentation;

public sealed class VisualLifecycleHub
{
    private readonly object _gate = new();
    private readonly List<Subscription> _subscribers = new();
    private readonly IHostDiagnostics? _diagnostics;

    public VisualLifecycleHub()
    {
    }

    public VisualLifecycleHub(IHostDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _diagnostics = diagnostics;
    }

    public IDisposable Subscribe(Action<VisualLifecycleEvent> handler)
    {
        return Subscribe(handler, new VisualLifecycleSubscriptionOptions());
    }

    public IDisposable Subscribe(
        Action<VisualLifecycleEvent> handler,
        VisualLifecycleSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        var subscription = new Subscription(this, handler, options);

        lock (_gate)
        {
            _subscribers.Add(subscription);
        }

        options.ActivationScope?.Add(subscription);
        return subscription;
    }

    public void Notify(object view, VisualLifecycleEventKind kind)
    {
        ArgumentNullException.ThrowIfNull(view);
        Notify(new VisualLifecycleEvent(view, kind));
    }

    public void Notify(VisualIdentity identity, VisualLifecycleEventKind kind)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Notify(new VisualLifecycleEvent(identity, kind));
    }

    public void Notify(VisualLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        if (!Enum.IsDefined(lifecycleEvent.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleEvent), "Visual lifecycle event kind must be defined.");
        }

        Subscription[] subscribers;

        lock (_gate)
        {
            subscribers = _subscribers.ToArray();
        }

        foreach (var subscriber in subscribers)
        {
            if (!subscriber.Matches(lifecycleEvent.Identity))
            {
                continue;
            }

            try
            {
                subscriber.Invoke(lifecycleEvent);
                WriteAdapterExecutedDiagnostic(lifecycleEvent);
            }
            catch (Exception exception)
            {
                WriteAdapterFailedDiagnostic(lifecycleEvent, exception);
            }
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscription);
        }
    }

    private void WriteAdapterExecutedDiagnostic(VisualLifecycleEvent lifecycleEvent)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.VisualLifecycleAdapterExecuted,
            $"Visual lifecycle adapter handled {lifecycleEvent.Kind} for view '{lifecycleEvent.View.GetType().FullName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(lifecycleEvent, exception: null),
        });
    }

    private void WriteAdapterFailedDiagnostic(
        VisualLifecycleEvent lifecycleEvent,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.VisualLifecycleAdapterFailed,
            $"Visual lifecycle adapter failed while handling {lifecycleEvent.Kind} for view '{lifecycleEvent.View.GetType().FullName}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(lifecycleEvent, exception),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        VisualLifecycleEvent lifecycleEvent,
        Exception? exception)
    {
        var viewType = lifecycleEvent.View.GetType();
        var viewModel = lifecycleEvent.View is IViewDataContextAware dataContextAware
            ? dataContextAware.DataContext
            : null;

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["viewType"] = viewType.FullName,
            ["viewModelType"] = viewModel?.GetType().FullName,
            ["eventKind"] = lifecycleEvent.Kind.ToString(),
            ["windowId"] = lifecycleEvent.Identity.WindowId,
            ["outletName"] = lifecycleEvent.Identity.OutletName,
            ["operationId"] = lifecycleEvent.Identity.OperationId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["entryId"] = lifecycleEvent.Identity.EntryId,
            ["error"] = exception?.GetType().FullName,
        };
    }

    private sealed class Subscription : IDisposable
    {
        private readonly VisualLifecycleHub _hub;
        private Action<VisualLifecycleEvent>? _handler;
        private readonly VisualLifecycleSubscriptionOptions _options;
        private int _disposed;

        public Subscription(
            VisualLifecycleHub hub,
            Action<VisualLifecycleEvent> handler,
            VisualLifecycleSubscriptionOptions options)
        {
            _hub = hub;
            _handler = handler;
            _options = options;
        }

        public bool Matches(VisualIdentity identity)
        {
            return !IsDisposed &&
                Matches(_options.WindowId, identity.WindowId) &&
                Matches(_options.OutletName, identity.OutletName) &&
                (!_options.OperationId.HasValue || _options.OperationId == identity.OperationId) &&
                Matches(_options.EntryId, identity.EntryId);
        }

        public void Invoke(VisualLifecycleEvent lifecycleEvent) => _handler?.Invoke(lifecycleEvent);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            var handler = _handler;
            _handler = null;
            if (handler is not null)
            {
                _hub.Unsubscribe(this);
            }
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private static bool Matches(string? expected, string? actual) =>
            expected is null || string.Equals(expected, actual, StringComparison.Ordinal);
    }
}
