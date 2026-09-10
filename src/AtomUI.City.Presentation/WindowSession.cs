using Avalonia.Controls;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public sealed class WindowSession : IAsyncDisposable
{
    private readonly Dictionary<string, OutletRegistration> _outlets = new(StringComparer.Ordinal);
    private readonly HashSet<Task> _detachedOutletTasks = [];
    private readonly object _gate = new();
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly PresentationQueueOptions _queueOptions;
    private readonly IPresentationFailurePresenter _failurePresenter;
    private readonly VisualLifecycleHub? _lifecycleHub;
    private Task<bool>? _closeTask;
    private bool _closeCommitted;
    private bool _closeCannotBeRejected;
    private bool _nativeCloseInProgress;
    private bool _disposed;
    private WindowCloseOrigin? _activeCloseOrigin;
    private WindowSessionState _state;

    internal WindowSession(
        string id,
        Window window,
        LifecycleScope scope,
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics,
        PresentationQueueOptions? queueOptions = null,
        IPresentationFailurePresenter? failurePresenter = null,
        VisualLifecycleHub? lifecycleHub = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        queueOptions ??= new PresentationQueueOptions();
        queueOptions.Validate();

        Id = id;
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _diagnostics = diagnostics;
        _queueOptions = queueOptions;
        _failurePresenter = failurePresenter ?? new NullPresentationFailurePresenter();
        _lifecycleHub = lifecycleHub;
        _state = WindowSessionState.Registered;

        Window.Closing += HandleWindowClosing;
        Window.Closed += HandleWindowClosed;
        RouteOutletProperties.SetWindowSession(Window, this);
        TransitionStateUnderLock(WindowSessionState.Ready);
    }

    public string Id { get; }

    public Window Window { get; }

    public LifecycleScope Scope { get; }

    public WindowSessionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public WindowCloseOrigin? ActiveCloseOrigin
    {
        get
        {
            lock (_gate)
            {
                return _activeCloseOrigin;
            }
        }
    }

    internal event EventHandler? Closed;

    public IReadOnlyCollection<IRouteOutlet> Outlets
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(
                    _outlets.Values
                        .OrderBy(static item => item.Name, StringComparer.Ordinal)
                        .Select(static item => (IRouteOutlet)item.Outlet)
                        .ToArray());
            }
        }
    }

    public IDisposable RegisterOutlet(string name, IRouteOutletTarget target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(target);

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_state != WindowSessionState.Ready)
            {
                throw new PresentationException(
                    PresentationError.WindowCloseRejected,
                    $"Window '{Id}' is not accepting new outlets while closing.");
            }

            if (_outlets.ContainsKey(name))
            {
                throw new PresentationException(
                    PresentationError.DuplicateOutlet,
                    $"Window '{Id}' already contains outlet '{name}'.");
            }

            var registration = new OutletRegistration(
                this,
                name,
                new RouteOutlet(
                    name,
                    _dispatcher,
                    target,
                    _diagnostics,
                    _failurePresenter,
                    _queueOptions,
                    _lifecycleHub,
                    Id));
            _outlets.Add(name, registration);
            return registration;
        }
    }

    public IRouteOutlet GetOutlet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            ThrowIfDisposed();
            return _outlets.TryGetValue(name, out var registration)
                ? registration.Outlet
                : throw new PresentationException(
                    PresentationError.OutletNotFound,
                    $"Window '{Id}' does not contain outlet '{name}'.");
        }
    }

    public ValueTask<bool> CloseAsync(CancellationToken cancellationToken = default)
    {
        return CloseAsync(WindowCloseOrigin.Application, cancellationToken);
    }

    public ValueTask<bool> CloseAsync(
        WindowCloseOrigin origin,
        CancellationToken cancellationToken = default)
    {
        return CloseCoreAsync(origin, canReject: origin != WindowCloseOrigin.OperatingSystem, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseCoreAsync(
            WindowCloseOrigin.Application,
            canReject: false,
            CancellationToken.None).ConfigureAwait(false);
    }

    internal ValueTask<bool> CloseForHostShutdownAsync(
        WindowCloseOrigin origin,
        CancellationToken cancellationToken)
    {
        return CloseCoreAsync(origin, canReject: false, cancellationToken);
    }

    private ValueTask<bool> CloseCoreAsync(
        WindowCloseOrigin origin,
        bool canReject,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        Task<bool> closeTask;
        TaskCompletionSource<bool>? completion = null;

        lock (_gate)
        {
            if (_disposed || _state == WindowSessionState.Closed)
            {
                return ValueTask.FromResult(true);
            }

            if (!canReject)
            {
                _closeCannotBeRejected = true;
            }

            if (_closeTask is null)
            {
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _closeTask = completion.Task;
                _activeCloseOrigin = origin;
                TransitionStateUnderLock(WindowSessionState.Closing);
            }

            closeTask = _closeTask;
        }

        if (completion is not null)
        {
            _ = Task.Run(() => RunCloseAsync(completion, origin));
        }

        return new ValueTask<bool>(closeTask.WaitAsync(cancellationToken));
    }

    private async Task RunCloseAsync(
        TaskCompletionSource<bool> completion,
        WindowCloseOrigin origin)
    {
        try
        {
            var outlets = SnapshotOutlets();
            await Task.WhenAll(
                outlets.Select(static registration =>
                    registration.Outlet.FreezeAdmissionsAsync().AsTask())).ConfigureAwait(false);

            if (CanRejectClose(origin))
            {
                var closeAllowed = await EvaluateCloseGuardsAsync(outlets).ConfigureAwait(false);
                if (!closeAllowed && CanRejectClose(origin))
                {
                    foreach (var registration in outlets)
                    {
                        registration.Outlet.ResumeAdmissions();
                    }

                    lock (_gate)
                    {
                        TransitionStateUnderLock(WindowSessionState.Ready);
                        _activeCloseOrigin = null;
                        _closeTask = null;
                    }

                    completion.TrySetResult(false);
                    return;
                }
            }

            lock (_gate)
            {
                _closeCommitted = true;
            }

            if (!IsNativeCloseInProgress())
            {
                await _dispatcher.InvokeAsync(Window.Close, CancellationToken.None).ConfigureAwait(false);
            }

            var failures = await CleanupAsync(outlets).ConfigureAwait(false);
            if (failures.Count != 0)
            {
                throw new AggregateException("Window cleanup failed.", failures);
            }

            lock (_gate)
            {
                _outlets.Clear();
                _disposed = true;
                TransitionStateUnderLock(WindowSessionState.Closed);
            }

            RaiseClosed();
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                TransitionStateUnderLock(WindowSessionState.Faulted);
            }

            WriteWindowCleanupFailedDiagnostic(origin, exception);
            completion.TrySetException(exception);
        }
    }

    private OutletRegistration[] SnapshotOutlets()
    {
        lock (_gate)
        {
            return _outlets.Values
                .OrderBy(static item => item.Name, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private async ValueTask<bool> EvaluateCloseGuardsAsync(OutletRegistration[] outlets)
    {
        var currentEntries = outlets
            .Select(static registration => registration.Outlet.CurrentEntry)
            .Where(static entry => entry is not null)
            .Cast<PresentationEntry>()
            .ToArray();

        foreach (var entry in currentEntries)
        {
            if (entry.ViewModel is not ICanDeactivate guard)
            {
                continue;
            }

            DeactivationResult? result;
            try
            {
                result = await guard
                    .CanDeactivateAsync(Scope.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                return false;
            }

            if (result is null || result.Status != DeactivationStatus.Allow)
            {
                return false;
            }
        }

        var confirmations = currentEntries
            .Where(static entry => entry.ViewModel is IConfirmDeactivate)
            .ToArray();
        if (confirmations.Length > 1)
        {
            WriteMultipleConfirmationsDiagnostic(confirmations);
            return false;
        }

        if (confirmations.Length == 0)
        {
            return true;
        }

        try
        {
            var result = await ((IConfirmDeactivate)confirmations[0].ViewModel)
                .ConfirmDeactivateAsync(Scope.CancellationToken)
                .ConfigureAwait(false);
            return result is not null && result.Status == DeactivationStatus.Allow;
        }
        catch
        {
            return false;
        }
    }

    private async ValueTask<IReadOnlyList<Exception>> CleanupAsync(OutletRegistration[] outlets)
    {
        var failures = new List<Exception>();
        Task[] detachedOutletTasks;
        lock (_gate)
        {
            detachedOutletTasks = _detachedOutletTasks.ToArray();
        }

        var outletCleanupTasks = outlets
            .Select(static registration => registration.DisposeOutletAsync().AsTask())
            .Concat(detachedOutletTasks)
            .Distinct()
            .ToArray();
        foreach (var cleanupTask in outletCleanupTasks)
        {
            try
            {
                await cleanupTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            await _dispatcher.InvokeAsync(
                () =>
                {
                    Window.Closing -= HandleWindowClosing;
                    Window.Closed -= HandleWindowClosed;
                    RouteOutletProperties.SetWindowSession(Window, null);
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await Scope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        return failures;
    }

    private void HandleWindowClosing(object? sender, WindowClosingEventArgs args)
    {
        if (IsCloseCommitted())
        {
            return;
        }

        var origin = args.CloseReason switch
        {
            WindowCloseReason.OSShutdown => WindowCloseOrigin.OperatingSystem,
            WindowCloseReason.ApplicationShutdown => WindowCloseOrigin.Application,
            _ when args.IsProgrammatic => WindowCloseOrigin.Application,
            _ => WindowCloseOrigin.User,
        };

        if (origin == WindowCloseOrigin.OperatingSystem)
        {
            lock (_gate)
            {
                _nativeCloseInProgress = true;
            }

            _ = CloseCoreAsync(origin, canReject: false, CancellationToken.None).AsTask();
            return;
        }

        args.Cancel = true;
        _ = CloseAsync(origin, CancellationToken.None).AsTask();
    }

    private void HandleWindowClosed(object? sender, EventArgs args)
    {
        if (IsCloseCommitted())
        {
            return;
        }

        lock (_gate)
        {
            _nativeCloseInProgress = true;
        }

        _ = CloseCoreAsync(
            WindowCloseOrigin.OperatingSystem,
            canReject: false,
            CancellationToken.None).AsTask();
    }

    private void RaiseClosed()
    {
        try
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            WriteWindowCleanupFailedDiagnostic(
                ActiveCloseOrigin ?? WindowCloseOrigin.Application,
                exception);
        }
    }

    private bool CanRejectClose(WindowCloseOrigin origin)
    {
        lock (_gate)
        {
            return origin != WindowCloseOrigin.OperatingSystem && !_closeCannotBeRejected;
        }
    }

    private bool IsNativeCloseInProgress()
    {
        lock (_gate)
        {
            return _nativeCloseInProgress;
        }
    }

    private bool IsCloseCommitted()
    {
        lock (_gate)
        {
            return _closeCommitted;
        }
    }

    private void DetachOutlet(OutletRegistration registration)
    {
        var disposeTask = registration.DisposeOutletAsync().AsTask();
        lock (_gate)
        {
            if (_outlets.TryGetValue(registration.Name, out var current) &&
                ReferenceEquals(current, registration))
            {
                _outlets.Remove(registration.Name);
                _detachedOutletTasks.Add(disposeTask);
            }
        }

        _ = ObserveDetachedOutletDisposeAsync(disposeTask);
    }

    private async Task ObserveDetachedOutletDisposeAsync(Task disposeTask)
    {
        try
        {
            await disposeTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteWindowCleanupFailedDiagnostic(
                ActiveCloseOrigin ?? WindowCloseOrigin.Application,
                exception);
        }
        finally
        {
            lock (_gate)
            {
                _detachedOutletTasks.Remove(disposeTask);
            }
        }
    }

    private void WriteMultipleConfirmationsDiagnostic(PresentationEntry[] confirmations)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.MultipleCloseConfirmations,
            $"Window '{Id}' rejected close because multiple current entries requested visible confirmation.",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["windowId"] = Id,
                ["confirmationCount"] = confirmations.Length.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["viewModelTypes"] = string.Join(
                    ",",
                    confirmations.Select(static entry => entry.ViewModel.GetType().FullName)),
            },
        });
    }

    private void WriteWindowCleanupFailedDiagnostic(WindowCloseOrigin origin, Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.WindowCleanupFailed,
            $"Window session '{Id}' cleanup failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["windowId"] = Id,
                ["closeOrigin"] = origin.ToString(),
                ["error"] = exception.GetType().FullName,
            },
        });
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _state is WindowSessionState.Closed or WindowSessionState.Faulted)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private void TransitionStateUnderLock(WindowSessionState next)
    {
        PresentationStateTransitions.Ensure(_state, next);
        _state = next;
    }

    private sealed class OutletRegistration : IDisposable
    {
        private readonly WindowSession _owner;
        private readonly object _disposeGate = new();
        private int _detached;
        private Task? _disposeTask;

        public OutletRegistration(WindowSession owner, string name, RouteOutlet outlet)
        {
            _owner = owner;
            Name = name;
            Outlet = outlet;
        }

        public string Name { get; }

        public RouteOutlet Outlet { get; }

        public void Dispose()
        {
            if (_owner.IsCloseCommitted())
            {
                return;
            }

            if (Interlocked.Exchange(ref _detached, 1) != 0)
            {
                return;
            }

            _owner.DetachOutlet(this);
        }

        public ValueTask DisposeOutletAsync()
        {
            Task disposeTask;
            TaskCompletionSource? completion = null;
            lock (_disposeGate)
            {
                if (_disposeTask is null)
                {
                    completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _disposeTask = completion.Task;
                }

                disposeTask = _disposeTask;
            }

            if (completion is not null)
            {
                _ = RunDisposeOutletAsync(completion);
            }

            return new ValueTask(disposeTask);
        }

        private async Task RunDisposeOutletAsync(TaskCompletionSource completion)
        {
            try
            {
                await Outlet.DisposeAsync().ConfigureAwait(false);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

    }
}
