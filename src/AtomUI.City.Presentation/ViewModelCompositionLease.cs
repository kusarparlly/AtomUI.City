using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Owns one explicitly composed logical ViewModel activation session.
/// </summary>
public sealed class ViewModelCompositionLease : IAsyncDisposable
{
    internal ViewModelCompositionLease(
        ViewModelCompositionOwner owner,
        string compositionId,
        ViewModelCompositionLease? parent,
        ViewModelLease viewModelLease,
        IActivationScope activationScope,
        long attachSequence)
    {
        Owner = owner;
        CompositionId = compositionId;
        Parent = parent;
        ViewModelLease = viewModelLease;
        ActivationScope = activationScope;
        AttachSequence = attachSequence;
        AttachCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _ = AttachCompletion.Task.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Gets the composition identity, unique within the owner.
    /// </summary>
    public string CompositionId { get; }

    /// <summary>
    /// Gets the owner identity.
    /// </summary>
    public string OwnerId => Owner.OwnerId;

    /// <summary>
    /// Gets the parent composition identity, or null for a root.
    /// </summary>
    public string? ParentId => Parent?.CompositionId;

    /// <summary>
    /// Gets the composed ViewModel.
    /// </summary>
    public object ViewModel => ViewModelLease.Instance;

    /// <summary>
    /// Gets the independent activation scope owned by this lease.
    /// </summary>
    public IActivationScope ActivationScope { get; }

    /// <summary>
    /// Gets a value indicating whether activation completed and cleanup has not started.
    /// </summary>
    public bool IsActive => Owner.IsLeaseActive(this);

    /// <summary>
    /// Gets a value indicating whether cleanup completed.
    /// </summary>
    public bool IsDisposed => Owner.IsLeaseDisposed(this);

    /// <summary>
    /// Gets a snapshot of owned children in attach order.
    /// </summary>
    public IReadOnlyCollection<ViewModelCompositionLease> Children => Owner.SnapshotChildren(this);

    internal ViewModelCompositionOwner Owner { get; }

    internal ViewModelCompositionLease? Parent { get; }

    internal ViewModelLease ViewModelLease { get; }

    internal List<ViewModelCompositionLease> ChildrenInternal { get; } = [];

    internal TaskCompletionSource AttachCompletion { get; }

    internal long AttachSequence { get; }

    internal Task? DisposeTask { get; set; }

    internal bool IsActiveInternal { get; set; }

    internal bool IsClosing { get; set; }

    internal bool IsDisposedInternal { get; set; }

    /// <summary>
    /// Attaches and activates an explicitly owned child ViewModel.
    /// </summary>
    public ValueTask<ViewModelCompositionLease> AttachChildAsync(
        string compositionId,
        ViewModelLease viewModelLease,
        CancellationToken cancellationToken = default)
    {
        return Owner.AttachChildAsync(this, compositionId, viewModelLease, cancellationToken);
    }

    /// <summary>
    /// Stops children, deactivates this ViewModel, and releases its ownership lease.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return Owner.DisposeLeaseAsync(this);
    }
}
