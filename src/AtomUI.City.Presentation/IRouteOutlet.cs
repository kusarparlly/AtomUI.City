namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iroute outlet.
/// </summary>
public interface IRouteOutlet
{
    /// <summary>
    /// Gets name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets current content.
    /// </summary>
    object? CurrentContent { get; }

    /// <summary>
    /// Gets current entry.
    /// </summary>
    PresentationEntry? CurrentEntry { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    RouteOutletState State { get; }

    /// <summary>
    /// Gets queue snapshot.
    /// </summary>
    PresentationQueueSnapshot QueueSnapshot => new(
        PresentationQueueOptions.DefaultOutletPendingCapacity,
        PendingCount: 0,
        InFlightCount: 0,
        PeakPendingCount: 0,
        RejectedCount: 0);

    /// <summary>
    /// Executes the commit async operation.
    /// </summary>
    ValueTask<RouteOutletCommitResult> CommitAsync(
        RouteOutletCommitPlan plan,
        CancellationToken cancellationToken = default);
}
