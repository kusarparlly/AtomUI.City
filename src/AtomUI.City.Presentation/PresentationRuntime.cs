using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public sealed class PresentationRuntime : IPresentationRuntime
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, WindowSession> _windows = new(StringComparer.Ordinal);
    private readonly IHostDiagnostics? _diagnostics;
    private readonly IUiDispatcher? _dispatcher;
    private readonly Func<IUiDispatcher?>? _dispatcherResolver;
    private readonly PresentationQueueOptions _queueOptions;
    private readonly IPresentationFailurePresenter _failurePresenter;
    private readonly VisualLifecycleHub? _lifecycleHub;
    private LifecycleScope? _presentationScope;
    private IApplicationLifetime? _applicationLifetime;
    private PresentationRuntimeState _state = PresentationRuntimeState.NotReady;
    private Task? _stopTask;

    public PresentationRuntime(IHostDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
        _queueOptions = new PresentationQueueOptions();
        _failurePresenter = new NullPresentationFailurePresenter();
    }

    public PresentationRuntime(IUiDispatcher dispatcher, IHostDiagnostics? diagnostics = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _diagnostics = diagnostics;
        _queueOptions = new PresentationQueueOptions();
        _failurePresenter = new NullPresentationFailurePresenter();
    }

    internal PresentationRuntime(
        Func<IUiDispatcher?> dispatcherResolver,
        IHostDiagnostics? diagnostics,
        PresentationQueueOptions? queueOptions = null,
        IPresentationFailurePresenter? failurePresenter = null,
        VisualLifecycleHub? lifecycleHub = null)
    {
        _dispatcherResolver = dispatcherResolver ?? throw new ArgumentNullException(nameof(dispatcherResolver));
        _diagnostics = diagnostics;
        _queueOptions = queueOptions ?? new PresentationQueueOptions();
        _queueOptions.Validate();
        _failurePresenter = failurePresenter ?? new NullPresentationFailurePresenter();
        _lifecycleHub = lifecycleHub;
    }

    public PresentationRuntimeState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public bool IsReady => State == PresentationRuntimeState.Ready;

    public LifecycleScope? PresentationScope
    {
        get
        {
            lock (_syncRoot)
            {
                return _presentationScope;
            }
        }
    }

    public IApplicationLifetime? ApplicationLifetime
    {
        get
        {
            lock (_syncRoot)
            {
                return _applicationLifetime;
            }
        }
    }

    public IReadOnlyCollection<WindowSession> Windows
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_windows.Values.ToArray());
            }
        }
    }

    public void Attach(
        IApplicationLifetime applicationLifetime,
        LifecycleScope hostScope,
        string presentationScopeId = "presentation")
    {
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(hostScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentationScopeId);

        LifecycleScope scope;
        lock (_syncRoot)
        {
            if (_state == PresentationRuntimeState.Ready)
            {
                if (!ReferenceEquals(_applicationLifetime, applicationLifetime))
                {
                    throw new InvalidOperationException("Presentation is already attached to another Avalonia lifetime.");
                }

                return;
            }

            ThrowIfStopping();
            scope = hostScope.CreateChild(LifecycleScopeKind.Presentation, presentationScopeId);
            _applicationLifetime = applicationLifetime;
            _presentationScope = scope;
            TransitionStateUnderLock(PresentationRuntimeState.Ready);
        }

        WriteDiagnostic(PresentationDiagnosticIds.RuntimeReady, "Presentation runtime is attached and ready.", scope.Id);
    }

    public ValueTask StartAsync(
        LifecycleScope applicationScope,
        string presentationScopeId = "presentation",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentationScopeId);
        cancellationToken.ThrowIfCancellationRequested();

        LifecycleScope scope;
        lock (_syncRoot)
        {
            if (_state == PresentationRuntimeState.Ready)
            {
                return ValueTask.CompletedTask;
            }

            ThrowIfStopping();
            scope = applicationScope.CreateChild(LifecycleScopeKind.Presentation, presentationScopeId);
            _presentationScope = scope;
            TransitionStateUnderLock(PresentationRuntimeState.Ready);
        }

        WriteDiagnostic(PresentationDiagnosticIds.RuntimeReady, "Presentation runtime is ready.", scope.Id);
        return ValueTask.CompletedTask;
    }

    public WindowSession RegisterWindow(Window window, string windowId)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);

        lock (_syncRoot)
        {
            EnsureReady();
            var dispatcher = _dispatcher ?? _dispatcherResolver?.Invoke();
            if (dispatcher is null)
            {
                throw new PresentationException(PresentationError.RuntimeNotReady,
                    "Presentation runtime has no Avalonia UI dispatcher.");
            }

            if (!dispatcher.CheckAccess())
            {
                throw new InvalidOperationException("Windows must be registered synchronously on the Avalonia UI thread.");
            }

            if (window.IsVisible)
            {
                throw new InvalidOperationException("A City managed Window must be registered before Show.");
            }

            if (_windows.ContainsKey(windowId) || _windows.Values.Any(session => ReferenceEquals(session.Window, window)))
            {
                throw new PresentationException(PresentationError.DuplicateWindow,
                    $"Window '{windowId}' is already registered.");
            }

            var session = new WindowSession(
                windowId,
                window,
                _presentationScope!.CreateChild(LifecycleScopeKind.Window, windowId),
                dispatcher,
                _diagnostics,
                _queueOptions,
                _failurePresenter,
                _lifecycleHub);
            session.Closed += HandleWindowSessionClosed;
            _windows.Add(windowId, session);
            return session;
        }
    }

    public LifecycleScope CreateWindowScope(string windowScopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowScopeId);
        lock (_syncRoot)
        {
            EnsureReady();
            return _presentationScope!.CreateChild(LifecycleScopeKind.Window, windowScopeId);
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task stopTask;
        TaskCompletionSource? completion = null;
        lock (_syncRoot)
        {
            if (_stopTask is not null)
            {
                return new ValueTask(_stopTask.WaitAsync(cancellationToken));
            }

            if (_state == PresentationRuntimeState.Stopped)
            {
                _stopTask = Task.CompletedTask;
                return ValueTask.CompletedTask;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopTask = completion.Task;
            TransitionStateUnderLock(PresentationRuntimeState.Stopping);
            stopTask = _stopTask;
        }

        _ = RunStopAsync(completion);
        return new ValueTask(stopTask.WaitAsync(cancellationToken));
    }

    private async Task RunStopAsync(TaskCompletionSource completion)
    {
        LifecycleScope? presentationScope;
        WindowSession[] windows;
        lock (_syncRoot)
        {
            presentationScope = _presentationScope;
            windows = _windows.Values.ToArray();
        }

        var failures = new List<Exception>();
        try
        {
            if (presentationScope is null)
            {
                lock (_syncRoot)
                {
                    TransitionStateUnderLock(PresentationRuntimeState.Stopped);
                }

                completion.TrySetResult();
                return;
            }

            WriteDiagnostic(PresentationDiagnosticIds.RuntimeStopping, "Presentation runtime is stopping.", presentationScope.Id);
            foreach (var window in windows)
            {
                try
                {
                    if (!await window.CloseForHostShutdownAsync(
                            WindowCloseOrigin.Application,
                            CancellationToken.None).ConfigureAwait(false))
                    {
                        failures.Add(new PresentationException(
                            PresentationError.WindowCloseRejected,
                            $"Window '{window.Id}' rejected application shutdown."));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                await presentationScope.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            lock (_syncRoot)
            {
                _windows.Clear();
                TransitionStateUnderLock(failures.Count == 0
                    ? PresentationRuntimeState.Stopped
                    : PresentationRuntimeState.Faulted);
            }

            if (failures.Count == 0)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(new AggregateException(
                    "Presentation runtime shutdown failed.",
                    failures));
            }
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                TransitionStateUnderLock(PresentationRuntimeState.Faulted);
            }

            completion.TrySetException(exception);
        }
    }

    private void HandleWindowSessionClosed(object? sender, EventArgs args)
    {
        if (sender is not WindowSession session)
        {
            return;
        }

        session.Closed -= HandleWindowSessionClosed;
        lock (_syncRoot)
        {
            if (_windows.TryGetValue(session.Id, out var current) && ReferenceEquals(current, session))
            {
                _windows.Remove(session.Id);
            }
        }
    }

    private void EnsureReady()
    {
        if (_state == PresentationRuntimeState.NotReady || _presentationScope is null)
        {
            throw new PresentationException(PresentationError.RuntimeNotReady, "Presentation runtime is not ready.");
        }

        ThrowIfStopping();
    }

    private void ThrowIfStopping()
    {
        if (_state is PresentationRuntimeState.Stopping or PresentationRuntimeState.Stopped or PresentationRuntimeState.Faulted)
        {
            throw new PresentationException(PresentationError.RuntimeStopping, "Presentation runtime is stopping or stopped.");
        }
    }

    private void TransitionStateUnderLock(PresentationRuntimeState next)
    {
        PresentationStateTransitions.Ensure(_state, next);
        _state = next;
    }

    private void WriteDiagnostic(string code, string message, string scopeId)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(code, message, HostDiagnosticSeverity.Info)
        {
            ScopeId = scopeId,
        });
    }
}
