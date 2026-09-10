namespace AtomUI.City.Presentation;

public sealed record PresentationQueueSnapshot(
    int Capacity,
    int PendingCount,
    int InFlightCount,
    int PeakPendingCount,
    long RejectedCount);
