using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Explicitly owns logical root and child ViewModels outside RouteOutlet entries.
/// </summary>
public sealed class ViewModelCompositionOwner : IAsyncDisposable
{
    private static readonly Dictionary<object, List<InstanceRegistration>> AttachedInstances =
        new(ReferenceEqualityComparer.Instance);
    private static readonly object AttachedInstancesGate = new();

    private readonly Dictionary<string, ViewModelCompositionLease> _leases = new(StringComparer.Ordinal);
    private readonly HashSet<object> _ownedInstances = new(ReferenceEqualityComparer.Instance);
    private readonly List<ViewModelCompositionLease> _roots = [];
    private readonly object _gate = new();
    private readonly CancellationToken _ownerCancellationToken;
    private readonly IHostDiagnostics? _diagnostics;
    private Task? _disposeTask;
    private bool _closing;
    private bool _disposed;
    private long _attachSequence;

    /// <summary>
    /// Initializes a logical ViewModel composition owner.
    /// </summary>
    public ViewModelCompositionOwner(
        string ownerId,
        CancellationToken ownerCancellationToken = default,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        OwnerId = ownerId;
        _ownerCancellationToken = ownerCancellationToken;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the stable owner identity.
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// Gets a snapshot of current logical roots in attach order.
    /// </summary>
    public IReadOnlyCollection<ViewModelCompositionLease> Roots
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_roots.ToArray());
            }
        }
    }

    /// <summary>
    /// Attaches and activates a logical root ViewModel.
    /// </summary>
    public ValueTask<ViewModelCompositionLease> AttachRootAsync(
        string compositionId,
        ViewModelLease viewModelLease,
        CancellationToken cancellationToken = default)
    {
        return AttachAsync(
            parent: null,
            compositionId,
            viewModelLease,
            cancellationToken);
    }

    /// <summary>
    /// Stops all roots in reverse attach order.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;

        lock (_gate)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _closing = true;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            disposeTask = _disposeTask;
        }

        _ = RunDisposeAsync(completion);
        return new ValueTask(disposeTask);
    }

    internal ValueTask<ViewModelCompositionLease> AttachChildAsync(
        ViewModelCompositionLease parent,
        string compositionId,
        ViewModelLease viewModelLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return AttachAsync(parent, compositionId, viewModelLease, cancellationToken);
    }

    internal ValueTask DisposeLeaseAsync(ViewModelCompositionLease lease)
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;

        lock (_gate)
        {
            if (!ReferenceEquals(lease.Owner, this))
            {
                throw new PresentationException(
                    PresentationError.ViewModelCompositionRejected,
                    "The ViewModel composition lease belongs to another owner.");
            }

            if (lease.DisposeTask is null)
            {
                lease.IsClosing = true;
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lease.DisposeTask = completion.Task;
            }

            disposeTask = lease.DisposeTask;
        }

        if (completion is not null)
        {
            _ = RunDisposeLeaseAsync(lease, completion);
        }

        return new ValueTask(disposeTask);
    }

    internal IReadOnlyCollection<ViewModelCompositionLease> SnapshotChildren(
        ViewModelCompositionLease lease)
    {
        lock (_gate)
        {
            return Array.AsReadOnly(lease.ChildrenInternal.ToArray());
        }
    }

    internal bool IsLeaseActive(ViewModelCompositionLease lease)
    {
        lock (_gate)
        {
            return lease.IsActiveInternal && !lease.IsClosing && !lease.IsDisposedInternal;
        }
    }

    internal bool IsLeaseDisposed(ViewModelCompositionLease lease)
    {
        lock (_gate)
        {
            return lease.IsDisposedInternal;
        }
    }

    private async ValueTask<ViewModelCompositionLease> AttachAsync(
        ViewModelCompositionLease? parent,
        string compositionId,
        ViewModelLease viewModelLease,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compositionId);
        ArgumentNullException.ThrowIfNull(viewModelLease);
        cancellationToken.ThrowIfCancellationRequested();

        ViewModelCompositionLease? lease = null;
        string? rejectionReason = null;
        lock (_gate)
        {
            if (_closing || _disposed)
            {
                rejectionReason = "owner-closing";
            }
            else if (parent is not null
                && (!ReferenceEquals(parent.Owner, this)
                    || parent.IsClosing
                    || parent.IsDisposedInternal
                    || !parent.IsActiveInternal
                    || !_leases.ContainsKey(parent.CompositionId)))
            {
                rejectionReason = "invalid-parent";
            }
            else if (_leases.ContainsKey(compositionId))
            {
                rejectionReason = "duplicate-identity";
            }
            else if (!_ownedInstances.Add(viewModelLease.Instance))
            {
                rejectionReason = "duplicate-instance";
            }
            else if (!TryRegisterAttachedInstance(viewModelLease))
            {
                _ownedInstances.Remove(viewModelLease.Instance);
                rejectionReason = "cross-owner-owned-instance";
            }
            else
            {
                var parentToken = parent?.ActivationScope.CancellationToken ?? _ownerCancellationToken;
                lease = new ViewModelCompositionLease(
                    this,
                    compositionId,
                    parent,
                    viewModelLease,
                    new ActivationScope(
                        _diagnostics,
                        taskDrainTimeout: null,
                        parentCancellationToken: parentToken),
                    ++_attachSequence);
                _leases.Add(compositionId, lease);

                if (parent is null)
                {
                    _roots.Add(lease);
                }
                else
                {
                    parent.ChildrenInternal.Add(lease);
                }
            }
        }

        if (rejectionReason is not null)
        {
            Reject(compositionId, parent, viewModelLease.Instance, rejectionReason);
        }

        System.Diagnostics.Debug.Assert(lease is not null);

        try
        {
            using var activationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.ActivationScope.CancellationToken);

            if (lease.ViewModel is IActivatable activatable)
            {
                await activatable
                    .ActivateAsync(lease.ActivationScope, activationCancellation.Token)
                    .ConfigureAwait(false);
            }

            activationCancellation.Token.ThrowIfCancellationRequested();

            var closing = false;
            lock (_gate)
            {
                closing = lease.IsClosing || _closing || _disposed;
                if (!closing)
                {
                    lease.IsActiveInternal = true;
                }
            }

            if (closing)
            {
                throw new OperationCanceledException(
                    $"ViewModel composition '{compositionId}' closed during activation.",
                    lease.ActivationScope.CancellationToken);
            }

            lease.AttachCompletion.TrySetResult();
            WriteAttachedDiagnostic(lease);
            return lease;
        }
        catch (Exception exception)
        {
            lease.AttachCompletion.TrySetException(exception);
            try
            {
                await DisposeLeaseAsync(lease).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                exception.Data["AtomUI.City.Presentation.CompositionCleanupException"] = cleanupException;
            }

            throw;
        }
    }

    private async Task RunDisposeAsync(TaskCompletionSource completion)
    {
        ViewModelCompositionLease[] roots;
        lock (_gate)
        {
            roots = _roots
                .OrderByDescending(static lease => lease.AttachSequence)
                .ToArray();
        }

        var failures = new List<Exception>();
        foreach (var root in roots)
        {
            try
            {
                await root.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        lock (_gate)
        {
            _disposed = true;
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(new AggregateException(
                $"ViewModel composition owner '{OwnerId}' cleanup failed.",
                failures));
        }
    }

    private async Task RunDisposeLeaseAsync(
        ViewModelCompositionLease lease,
        TaskCompletionSource completion)
    {
        var failures = new List<Exception>();
        Task? earlyScopeDisposal = null;
        bool disposeScopeEarly;

        lock (_gate)
        {
            disposeScopeEarly = !lease.IsActiveInternal;
        }

        if (disposeScopeEarly)
        {
            earlyScopeDisposal = lease.ActivationScope.DisposeAsync().AsTask();
        }

        try
        {
            await lease.AttachCompletion.Task.ConfigureAwait(false);
        }
        catch
        {
            // The attach caller owns the original activation exception. Cleanup continues.
        }

        ViewModelCompositionLease[] children;
        lock (_gate)
        {
            children = lease.ChildrenInternal
                .OrderByDescending(static child => child.AttachSequence)
                .ToArray();
        }

        foreach (var child in children)
        {
            try
            {
                await child.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
                WriteCleanupFailedDiagnostic(lease, "DisposeChild", exception);
            }
        }

        if (lease.IsActiveInternal && lease.ViewModel is IActivatable activatable)
        {
            try
            {
                await activatable.DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
                WriteCleanupFailedDiagnostic(lease, "Deactivate", exception);
            }
        }

        try
        {
            if (earlyScopeDisposal is not null)
            {
                await earlyScopeDisposal.ConfigureAwait(false);
            }
            else
            {
                await lease.ActivationScope.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
            WriteCleanupFailedDiagnostic(lease, "DisposeActivationScope", exception);
        }

        try
        {
            await lease.ViewModelLease.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
            WriteCleanupFailedDiagnostic(lease, "DisposeViewModelLease", exception);
        }

        lock (_gate)
        {
            lease.IsActiveInternal = false;
            lease.IsDisposedInternal = true;
            _leases.Remove(lease.CompositionId);
            _ownedInstances.Remove(lease.ViewModel);
            UnregisterAttachedInstance(lease.ViewModelLease);
            if (lease.Parent is null)
            {
                _roots.Remove(lease);
            }
            else
            {
                lease.Parent.ChildrenInternal.Remove(lease);
            }
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(new AggregateException(
                $"ViewModel composition '{lease.CompositionId}' cleanup failed.",
                failures));
        }
    }

    private void Reject(
        string compositionId,
        ViewModelCompositionLease? parent,
        object viewModel,
        string reason)
    {
        WriteDiagnosticSafely(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewModelCompositionRejected,
            $"ViewModel composition '{compositionId}' was rejected ({reason}).",
            HostDiagnosticSeverity.Warning)
        {
            Context = new Dictionary<string, string?>
            {
                ["ownerId"] = OwnerId,
                ["compositionId"] = compositionId,
                ["parentId"] = parent?.CompositionId,
                ["viewModelType"] = viewModel.GetType().FullName,
                ["reason"] = reason,
            }
        });

        throw new PresentationException(
            PresentationError.ViewModelCompositionRejected,
            $"ViewModel composition '{compositionId}' was rejected ({reason}).");
    }

    private bool TryRegisterAttachedInstance(ViewModelLease viewModelLease)
    {
        lock (AttachedInstancesGate)
        {
            if (!AttachedInstances.TryGetValue(viewModelLease.Instance, out var registrations))
            {
                registrations = [];
                AttachedInstances.Add(viewModelLease.Instance, registrations);
            }

            if (registrations.Count != 0)
            {
                if (registrations.Count == 0)
                {
                    AttachedInstances.Remove(viewModelLease.Instance);
                }

                return false;
            }

            registrations.Add(new InstanceRegistration(this));
            return true;
        }
    }

    private void UnregisterAttachedInstance(ViewModelLease viewModelLease)
    {
        lock (AttachedInstancesGate)
        {
            if (!AttachedInstances.TryGetValue(viewModelLease.Instance, out var registrations))
            {
                return;
            }

            registrations.RemoveAll(registration => ReferenceEquals(registration.Owner, this));
            if (registrations.Count == 0)
            {
                AttachedInstances.Remove(viewModelLease.Instance);
            }
        }
    }

    private void WriteAttachedDiagnostic(ViewModelCompositionLease lease)
    {
        WriteDiagnosticSafely(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewModelCompositionAttached,
            $"ViewModel composition '{lease.CompositionId}' was attached.",
            HostDiagnosticSeverity.Info)
        {
            Context = new Dictionary<string, string?>
            {
                ["ownerId"] = OwnerId,
                ["compositionId"] = lease.CompositionId,
                ["parentId"] = lease.ParentId,
                ["viewModelType"] = lease.ViewModel.GetType().FullName,
                ["role"] = lease.Parent is null ? "Root" : "Child",
            }
        });
    }

    private void WriteCleanupFailedDiagnostic(
        ViewModelCompositionLease lease,
        string stage,
        Exception exception)
    {
        WriteDiagnosticSafely(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewModelCompositionCleanupFailed,
            $"ViewModel composition '{lease.CompositionId}' cleanup failed at '{stage}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["ownerId"] = OwnerId,
                ["compositionId"] = lease.CompositionId,
                ["viewModelType"] = lease.ViewModel.GetType().FullName,
                ["stage"] = stage,
                ["exceptionType"] = exception.GetType().FullName,
            }
        });
    }

    private void WriteDiagnosticSafely(HostDiagnosticRecord record)
    {
        try
        {
            _diagnostics?.Write(record);
        }
        catch
        {
            // Diagnostics cannot change composition ownership or lifecycle progress.
        }
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                failures.Add(inner);
            }

            return;
        }

        failures.Add(exception);
    }

    private sealed record InstanceRegistration(ViewModelCompositionOwner Owner);
}
