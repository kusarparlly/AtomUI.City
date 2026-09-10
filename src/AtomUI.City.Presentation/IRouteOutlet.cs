namespace AtomUI.City.Presentation;

public interface IRouteOutlet
{
    string Name { get; }

    object? CurrentContent { get; }

    PresentationEntry? CurrentEntry { get; }

    RouteOutletState State { get; }

    PresentationQueueSnapshot QueueSnapshot => new(
        PresentationQueueOptions.DefaultOutletPendingCapacity,
        PendingCount: 0,
        InFlightCount: 0,
        PeakPendingCount: 0,
        RejectedCount: 0);

    ValueTask<RouteOutletCommitResult> CommitAsync(
        RouteOutletCommitPlan plan,
        CancellationToken cancellationToken = default);
}
