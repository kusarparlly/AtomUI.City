namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation queue snapshot.
/// </summary>
/// <param name="Capacity">The capacity value.</param>
/// <param name="PendingCount">The pending count value.</param>
/// <param name="InFlightCount">The in flight count value.</param>
/// <param name="PeakPendingCount">The peak pending count value.</param>
/// <param name="RejectedCount">The rejected count value.</param>
public sealed record PresentationQueueSnapshot(
    int Capacity,
    int PendingCount,
    int InFlightCount,
    int PeakPendingCount,
    long RejectedCount);
