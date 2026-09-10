using System.Globalization;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public sealed class RouteOutlet : IRouteOutlet, IAsyncDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly IRouteOutletTarget? _target;
    private readonly IPresentationFailurePresenter _failurePresenter;
    private readonly VisualLifecycleHub? _lifecycleHub;
    private readonly string? _windowId;
    private readonly BoundedSerialExecutionLane _commitLane;
    private readonly object _stateGate = new();
    private PresentationEntry? _currentEntry;
    private RouteOutletState _state = RouteOutletState.Empty;
    private Task? _disposeTask;
    private bool _admissionFrozen;
    private long _operationSequence;

    public RouteOutlet(string name, IUiDispatcher dispatcher)
        : this(name, dispatcher, target: null, diagnostics: null, failurePresenter: null, queueOptions: null)
    {
    }

    public RouteOutlet(string name, IUiDispatcher dispatcher, IHostDiagnostics? diagnostics)
        : this(name, dispatcher, target: null, diagnostics, failurePresenter: null, queueOptions: null)
    {
    }

    public RouteOutlet(
        string name,
        IUiDispatcher dispatcher,
        IRouteOutletTarget? target,
        IHostDiagnostics? diagnostics = null,
        IPresentationFailurePresenter? failurePresenter = null,
        PresentationQueueOptions? queueOptions = null,
        VisualLifecycleHub? lifecycleHub = null,
        string? windowId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dispatcher);

        queueOptions ??= new PresentationQueueOptions();
        queueOptions.Validate();

        Name = name;
        _dispatcher = dispatcher;
        _target = target;
        _diagnostics = diagnostics;
        _failurePresenter = failurePresenter ?? new NullPresentationFailurePresenter();
        _lifecycleHub = lifecycleHub;
        _windowId = string.IsNullOrWhiteSpace(windowId) ? null : windowId;
        _commitLane = new BoundedSerialExecutionLane(queueOptions.OutletPendingCapacity);
    }

    public string Name { get; }

    public object? CurrentContent => CurrentEntry?.View;

    public PresentationEntry? CurrentEntry
    {
        get
        {
            lock (_stateGate)
            {
                return _currentEntry;
            }
        }
    }

    public RouteOutletState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public PresentationQueueSnapshot QueueSnapshot => _commitLane.Snapshot;

    public ValueTask<RouteOutletCommitResult> CommitAsync(
        RouteOutletCommitPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var operationId = Interlocked.Increment(ref _operationSequence);
        try
        {
            plan.TransferToOutlet(operationId);
        }
        catch (Exception exception)
        {
            _diagnostics?.Write(new HostDiagnosticRecord(
                PresentationDiagnosticIds.CandidateOwnershipViolation,
                $"Route outlet '{Name}' rejected a reused or corrupted commit plan.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["outletName"] = Name,
                    ["requestedOutletName"] = plan.OutletName,
                    ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                    ["candidateState"] = plan.OwnershipState.ToString(),
                    ["candidateOwnerOperationId"] = plan.OwnerOperationId.ToString(CultureInfo.InvariantCulture),
                    ["error"] = exception.GetType().FullName,
                },
            });

            throw new PresentationException(
                PresentationError.CandidateOwnershipViolation,
                "A route outlet commit plan can only be submitted once.",
                exception);
        }
        WriteDiagnostic(
            PresentationDiagnosticIds.OutletCommitPlanned,
            HostDiagnosticSeverity.Info,
            plan,
            result: null,
            operationId,
            stage: "Admission");

        var completion = new TaskCompletionSource<RouteOutletCommitResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = false;
        var stopping = false;

        if (cancellationToken.IsCancellationRequested)
        {
            _ = CompleteRejectedAdmissionAsync(
                plan,
                operationId,
                stopping: false,
                callerCanceled: true,
                completion);
            return new ValueTask<RouteOutletCommitResult>(completion.Task);
        }

        lock (_stateGate)
        {
            stopping = _admissionFrozen || _disposeTask is not null || _state is RouteOutletState.Stopping or
                RouteOutletState.Stopped or RouteOutletState.Faulted;
            if (!stopping)
            {
                accepted = _commitLane.TrySchedule(
                    () => ExecuteScheduledCommitAsync(plan, operationId, completion));
            }
        }

        if (!accepted)
        {
            _ = CompleteRejectedAdmissionAsync(
                plan,
                operationId,
                stopping,
                callerCanceled: false,
                completion);
        }

        return new ValueTask<RouteOutletCommitResult>(
            AwaitAcceptedCommitAsync(completion.Task, cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;

        lock (_stateGate)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            _admissionFrozen = true;
            TransitionStateUnderLock(RouteOutletState.Stopping);
            disposeTask = _disposeTask;
        }

        _commitLane.ScheduleControl(() => RunDisposeAsync(completion));
        return new ValueTask(disposeTask);
    }

    internal ValueTask FreezeAdmissionsAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_stateGate)
        {
            _admissionFrozen = true;
        }

        _commitLane.ScheduleControl(
            () =>
            {
                completion.TrySetResult();
                return Task.CompletedTask;
            });
        return new ValueTask(completion.Task);
    }

    internal void ResumeAdmissions()
    {
        lock (_stateGate)
        {
            if (_disposeTask is null && _state is not RouteOutletState.Stopped and not RouteOutletState.Faulted)
            {
                _admissionFrozen = false;
            }
        }
    }

    private async Task ExecuteScheduledCommitAsync(
        RouteOutletCommitPlan plan,
        long operationId,
        TaskCompletionSource<RouteOutletCommitResult> completion)
    {
        try
        {
            completion.TrySetResult(await ExecuteCommitCoreAsync(plan, operationId).ConfigureAwait(false));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task CompleteRejectedAdmissionAsync(
        RouteOutletCommitPlan plan,
        long operationId,
        bool stopping,
        bool callerCanceled,
        TaskCompletionSource<RouteOutletCommitResult> completion)
    {
        try
        {
            await DisposeRejectedCandidateAsync(plan, operationId).ConfigureAwait(false);

            var error = stopping || callerCanceled
                ? PresentationError.OutletCommitFailed
                : PresentationError.OutletQueueFull;
            var message = callerCanceled
                ? $"Outlet '{Name}' rejected operation '{operationId}' because the caller was already canceled."
                : stopping
                    ? $"Outlet '{Name}' is stopping, stopped, or faulted."
                    : $"Outlet '{Name}' rejected operation '{operationId}' because its pending queue is full.";

            if (!stopping && !callerCanceled)
            {
                WriteQueueRejectedDiagnostic(plan, operationId);
            }

            completion.TrySetResult(await FailAsync(
                plan,
                operationId,
                error,
                message,
                exception: null,
                stage: "Admission").ConfigureAwait(false));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async ValueTask<RouteOutletCommitResult> ExecuteCommitCoreAsync(
        RouteOutletCommitPlan plan,
        long operationId)
    {
        if (State is RouteOutletState.Stopping or RouteOutletState.Stopped or RouteOutletState.Faulted)
        {
            await DisposeRejectedCandidateAsync(plan, operationId).ConfigureAwait(false);
            return await FailAsync(
                plan,
                operationId,
                PresentationError.OutletCommitFailed,
                $"Outlet '{Name}' is stopping, stopped, or faulted.",
                exception: null,
                stage: "Admission").ConfigureAwait(false);
        }

        if (!string.Equals(plan.OutletName, Name, StringComparison.Ordinal))
        {
            await DisposeRejectedCandidateAsync(plan, operationId).ConfigureAwait(false);
            return await FailAsync(
                plan,
                operationId,
                PresentationError.OutletNotFound,
                $"Outlet '{plan.OutletName}' was not found.",
                exception: null,
                stage: "Admission").ConfigureAwait(false);
        }

        var previous = CurrentEntry;
        PresentationEntry? candidate = null;
        var stage = "Prepare";

        try
        {
            plan.LifecycleToken.ThrowIfCancellationRequested();
            SetState(RouteOutletState.Preparing);

            if (previous is not null && !plan.LeaveApproved)
            {
                stage = "LeaveGuard";
                var leave = await DeactivationGuard
                    .CanDeactivateAsync(previous.ViewModel, plan.LifecycleToken)
                    .ConfigureAwait(false);
                if (leave.Status != DeactivationStatus.Allow)
                {
                    throw leave.Exception ?? new PresentationException(
                        PresentationError.OutletCommitFailed,
                        leave.Reason ?? "The current ViewModel rejected deactivation.");
                }
            }

            if (plan.Operation == RouteOutletOperation.Clear)
            {
                stage = "FinalCommit";
                await SetTargetContentAsync(null, plan.LifecycleToken).ConfigureAwait(false);
                SetCurrentEntry(null, RouteOutletState.Empty);
                plan.Release(operationId);
                await DisposeEntryAfterCommitAsync(previous, operationId).ConfigureAwait(false);
                WriteCommitSucceededDiagnostic(plan, operationId, stage);
                return RouteOutletCommitResult.Success(operationId);
            }

            if (plan.Handle is null)
            {
                throw new PresentationException(
                    PresentationError.OutletCommitFailed,
                    "Route outlet replace commit requires a bound view handle.");
            }

            if (ReferenceEquals(previous?.Handle, plan.Handle))
            {
                plan.Release(operationId);
                SetState(RouteOutletState.Committed);
                WriteCommitSucceededDiagnostic(plan, operationId, "Reuse");
                return RouteOutletCommitResult.Success(operationId);
            }

            var identity = new VisualIdentity(
                plan.Handle.View,
                _windowId,
                Name,
                operationId,
                $"{Name}:{operationId.ToString(CultureInfo.InvariantCulture)}");
            IDisposable? visualSubscription = null;

            stage = "TemporaryAttach";
            await _dispatcher.InvokeAsync(
                () =>
                {
                    visualSubscription = AvaloniaVisualLifecycleSubscription.TryCreate(
                        plan.Handle.View,
                        _lifecycleHub,
                        identity);
                    try
                    {
                        _target?.SetContent(plan.Handle.View);
                    }
                    catch
                    {
                        visualSubscription?.Dispose();
                        visualSubscription = null;
                        throw;
                    }
                },
                plan.LifecycleToken).ConfigureAwait(false);
            var activationScope = new ActivationScope(_diagnostics);
            candidate = new PresentationEntry(
                operationId,
                plan.Handle,
                activationScope,
                plan.ViewModelLease,
                plan.RouteId,
                plan.ReuseKey,
                _dispatcher,
                plan,
                visualSubscription);
            SetState(RouteOutletState.TemporaryAttached);

            if (candidate.ViewModel is IActivatable activatable)
            {
                stage = "Activate";
                await activatable
                    .ActivateAsync(candidate.ActivationScope, plan.LifecycleToken)
                    .ConfigureAwait(false);
                candidate.IsActivated = true;
            }

            plan.LifecycleToken.ThrowIfCancellationRequested();
            stage = "FinalCommit";
            plan.TransferToEntry(operationId);
            SetCurrentEntry(candidate, RouteOutletState.Committed);
        }
        catch (Exception exception)
        {
            var rollbackFailure = await RollBackAsync(previous, candidate, plan, operationId)
                .ConfigureAwait(false);
            if (rollbackFailure is not null)
            {
                SetState(RouteOutletState.Faulted);
                WriteRollbackFailedDiagnostic(plan, operationId, stage, rollbackFailure);
                return await FailAsync(
                    plan,
                    operationId,
                    PresentationError.OutletCommitFailed,
                    $"Outlet '{Name}' failed during '{stage}' and rollback also failed.",
                    new AggregateException(exception, rollbackFailure),
                    stage).ConfigureAwait(false);
            }

            SetState(RouteOutletState.OutOfSync);
            return await FailAsync(
                plan,
                operationId,
                PresentationError.OutletCommitFailed,
                exception.Message,
                exception,
                stage).ConfigureAwait(false);
        }

        await DisposeEntryAfterCommitAsync(previous, operationId).ConfigureAwait(false);
        WriteCommitSucceededDiagnostic(plan, operationId, stage);
        return RouteOutletCommitResult.Success(operationId);
    }

    private async ValueTask<Exception?> RollBackAsync(
        PresentationEntry? previous,
        PresentationEntry? candidate,
        RouteOutletCommitPlan plan,
        long operationId)
    {
        var failures = new List<Exception>();

        try
        {
            await SetTargetContentAsync(previous?.View, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            if (candidate is not null)
            {
                await candidate.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await DisposeRejectedCandidateAsync(plan, operationId).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        return failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException("Presentation rollback failed.", failures),
        };
    }

    private async Task RunDisposeAsync(TaskCompletionSource completion)
    {
        try
        {
            var current = CurrentEntry;
            await SetTargetContentAsync(null, CancellationToken.None).ConfigureAwait(false);
            SetCurrentEntry(null, RouteOutletState.Stopped);
            if (current is not null)
            {
                await current.DisposeAsync().ConfigureAwait(false);
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            SetState(RouteOutletState.Faulted);
            WriteEntryCleanupFailedDiagnostic(operationId: 0, exception);
            completion.TrySetException(exception);
        }
    }

    private ValueTask SetTargetContentAsync(object? content, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => _target?.SetContent(content), cancellationToken);
    }

    private async ValueTask DisposeEntryAfterCommitAsync(PresentationEntry? entry, long operationId)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            await entry.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteEntryCleanupFailedDiagnostic(operationId, exception);
        }
    }

    private async ValueTask DisposeRejectedCandidateAsync(RouteOutletCommitPlan plan, long operationId)
    {
        if (plan.OwnershipState == CandidateOwnershipState.Released)
        {
            return;
        }

        if (plan.Handle is null || ReferenceEquals(plan.Handle, CurrentEntry?.Handle))
        {
            plan.Release(operationId);
            return;
        }

        var candidate = new PresentationEntry(
            operationId,
            plan.Handle,
            new ActivationScope(_diagnostics),
            plan.ViewModelLease,
            plan.RouteId,
            plan.ReuseKey,
            _dispatcher,
            plan);
        await candidate.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<RouteOutletCommitResult> AwaitAcceptedCommitAsync(
        Task<RouteOutletCommitResult> completion,
        CancellationToken cancellationToken)
    {
        return await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<RouteOutletCommitResult> FailAsync(
        RouteOutletCommitPlan plan,
        long operationId,
        PresentationError error,
        string message,
        Exception? exception,
        string stage)
    {
        var result = RouteOutletCommitResult.Failed(error, message, operationId);
        WriteDiagnostic(
            PresentationDiagnosticIds.OutletCommitFailed,
            HostDiagnosticSeverity.Error,
            plan,
            result,
            operationId,
            stage,
            exception);

        try
        {
            await _failurePresenter.PresentAsync(
                new PresentationFailure(
                    PresentationFailureLevel.Outlet,
                    error,
                    message,
                    operationId,
                    OutletName: Name,
                    RouteId: plan.RouteId,
                    Exception: exception),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception presenterException)
        {
            SetState(RouteOutletState.Faulted);
            _diagnostics?.Write(new HostDiagnosticRecord(
                PresentationDiagnosticIds.FailurePresenterFailed,
                $"Failure presenter failed for outlet '{Name}': {presenterException.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["outletName"] = Name,
                    ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                    ["stage"] = stage,
                    ["error"] = presenterException.GetType().FullName,
                },
            });
        }

        return result;
    }

    private void WriteCommitSucceededDiagnostic(
        RouteOutletCommitPlan plan,
        long operationId,
        string stage)
    {
        WriteDiagnostic(
            PresentationDiagnosticIds.OutletCommitSucceeded,
            HostDiagnosticSeverity.Info,
            plan,
            RouteOutletCommitResult.Success(operationId),
            operationId,
            stage);
    }

    private void WriteRollbackFailedDiagnostic(
        RouteOutletCommitPlan plan,
        long operationId,
        string stage,
        Exception exception)
    {
        WriteDiagnostic(
            PresentationDiagnosticIds.OutletRollbackFailed,
            HostDiagnosticSeverity.Error,
            plan,
            RouteOutletCommitResult.Failed(
                PresentationError.OutletCommitFailed,
                exception.Message,
                operationId),
            operationId,
            stage,
            exception);
    }

    private void WriteEntryCleanupFailedDiagnostic(long operationId, Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.PresentationEntryCleanupFailed,
            $"Route outlet '{Name}' entry cleanup failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["outletName"] = Name,
                ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                ["error"] = exception.GetType().FullName,
            },
        });
    }

    private void WriteQueueRejectedDiagnostic(RouteOutletCommitPlan plan, long operationId)
    {
        var snapshot = QueueSnapshot;
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.OutletQueueRejected,
            $"Route outlet '{Name}' rejected operation '{operationId}' because its queue is full.",
            HostDiagnosticSeverity.Warning)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["outletName"] = Name,
                ["requestedOutletName"] = plan.OutletName,
                ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                ["capacity"] = snapshot.Capacity.ToString(CultureInfo.InvariantCulture),
                ["pendingCount"] = snapshot.PendingCount.ToString(CultureInfo.InvariantCulture),
                ["rejectedCount"] = snapshot.RejectedCount.ToString(CultureInfo.InvariantCulture),
            },
        });
    }

    private void SetCurrentEntry(PresentationEntry? entry, RouteOutletState state)
    {
        lock (_stateGate)
        {
            _currentEntry = entry;
            if (_disposeTask is null || state is RouteOutletState.Stopped or RouteOutletState.Faulted)
            {
                TransitionStateUnderLock(state);
            }
        }
    }

    private void SetState(RouteOutletState state)
    {
        lock (_stateGate)
        {
            if (_disposeTask is null || state is RouteOutletState.Stopped or RouteOutletState.Faulted)
            {
                TransitionStateUnderLock(state);
            }
        }
    }

    private void TransitionStateUnderLock(RouteOutletState next)
    {
        PresentationStateTransitions.Ensure(_state, next);
        _state = next;
    }

    private void WriteDiagnostic(
        string code,
        HostDiagnosticSeverity severity,
        RouteOutletCommitPlan plan,
        RouteOutletCommitResult? result,
        long operationId,
        string stage,
        Exception? exception = null)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            code,
            result is null
                ? $"Route outlet '{Name}' planned {plan.Operation} operation '{operationId}' for requested outlet '{plan.OutletName}'."
                : $"Route outlet '{Name}' {plan.Operation} operation '{operationId}' completed with '{result.Error?.ToString() ?? "Success"}'.",
            severity)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                ["outletName"] = Name,
                ["requestedOutletName"] = plan.OutletName,
                ["operation"] = plan.Operation.ToString(),
                ["stage"] = stage,
                ["currentViewType"] = CurrentEntry?.View.GetType().FullName,
                ["newViewType"] = plan.Handle?.View.GetType().FullName,
                ["routeId"] = plan.RouteId,
                ["middlewareType"] = null,
                ["error"] = exception?.GetType().FullName ?? result?.Error?.ToString(),
            },
        });
    }
}
