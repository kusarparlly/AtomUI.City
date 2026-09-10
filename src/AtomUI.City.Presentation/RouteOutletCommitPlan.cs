namespace AtomUI.City.Presentation;

public sealed class RouteOutletCommitPlan
{
    private int _ownershipState = (int)CandidateOwnershipState.AdapterOwned;
    private long _ownerOperationId;

    private RouteOutletCommitPlan(
        string outletName,
        RouteOutletOperation operation,
        BoundViewHandle? handle,
        ViewModelLease? viewModelLease,
        string? routeId,
        string? reuseKey,
        CancellationToken lifecycleToken,
        bool leaveApproved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outletName);

        OutletName = outletName;
        Operation = operation;
        Handle = handle;
        ViewModelLease = viewModelLease;
        RouteId = string.IsNullOrWhiteSpace(routeId) ? null : routeId;
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        LifecycleToken = lifecycleToken;
        LeaveApproved = leaveApproved;
    }

    public string OutletName { get; }

    public RouteOutletOperation Operation { get; }

    public BoundViewHandle? Handle { get; }

    public ViewModelLease? ViewModelLease { get; }

    public string? RouteId { get; }

    public string? ReuseKey { get; }

    public CancellationToken LifecycleToken { get; }

    internal bool LeaveApproved { get; }

    internal CandidateOwnershipState OwnershipState =>
        (CandidateOwnershipState)Volatile.Read(ref _ownershipState);

    internal long OwnerOperationId => Interlocked.Read(ref _ownerOperationId);

    internal void TransferToOutlet(long operationId)
    {
        if (operationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        if (Interlocked.CompareExchange(
                ref _ownershipState,
                (int)CandidateOwnershipState.OutletTransactionOwned,
                (int)CandidateOwnershipState.AdapterOwned) !=
            (int)CandidateOwnershipState.AdapterOwned)
        {
            throw new InvalidOperationException("A route outlet commit plan can only be submitted once.");
        }

        Interlocked.Exchange(ref _ownerOperationId, operationId);
    }

    internal void TransferToEntry(long operationId)
    {
        EnsureOwner(operationId);
        if (Interlocked.CompareExchange(
                ref _ownershipState,
                (int)CandidateOwnershipState.EntryOwned,
                (int)CandidateOwnershipState.OutletTransactionOwned) !=
            (int)CandidateOwnershipState.OutletTransactionOwned)
        {
            throw new InvalidOperationException("The route outlet candidate is not owned by its transaction.");
        }
    }

    internal void Release(long operationId)
    {
        EnsureOwner(operationId);

        while (true)
        {
            var current = Volatile.Read(ref _ownershipState);
            if (current == (int)CandidateOwnershipState.Released)
            {
                return;
            }

            if (current is not ((int)CandidateOwnershipState.OutletTransactionOwned) and
                not ((int)CandidateOwnershipState.EntryOwned))
            {
                throw new InvalidOperationException("The route outlet candidate cannot be released by this owner.");
            }

            if (Interlocked.CompareExchange(
                    ref _ownershipState,
                    (int)CandidateOwnershipState.Released,
                    current) == current)
            {
                return;
            }
        }
    }

    private void EnsureOwner(long operationId)
    {
        if (operationId <= 0 || OwnerOperationId != operationId)
        {
            throw new InvalidOperationException("The route outlet candidate owner does not match the operation.");
        }
    }

    public static RouteOutletCommitPlan Replace(
        string outletName,
        BoundViewHandle handle,
        ViewModelLease? viewModelLease = null,
        string? routeId = null,
        string? reuseKey = null,
        CancellationToken lifecycleToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return new RouteOutletCommitPlan(
            outletName,
            RouteOutletOperation.Replace,
            handle,
            viewModelLease,
            routeId,
            reuseKey,
            lifecycleToken,
            leaveApproved: false);
    }

    public static RouteOutletCommitPlan Clear(string outletName)
    {
        return new RouteOutletCommitPlan(
            outletName,
            RouteOutletOperation.Clear,
            handle: null,
            viewModelLease: null,
            routeId: null,
            reuseKey: null,
            lifecycleToken: default,
            leaveApproved: false);
    }

    internal static RouteOutletCommitPlan ClearApproved(string outletName, CancellationToken lifecycleToken)
    {
        return new RouteOutletCommitPlan(
            outletName,
            RouteOutletOperation.Clear,
            handle: null,
            viewModelLease: null,
            routeId: null,
            reuseKey: null,
            lifecycleToken,
            leaveApproved: true);
    }
}
