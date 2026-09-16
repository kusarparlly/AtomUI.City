namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus metrics snapshot.
/// </summary>
public sealed record EventBusMetricsSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventBusMetricsSnapshot"/> type.
    /// </summary>
    public EventBusMetricsSnapshot(
        int activeSubscriptionCount,
        long publicationCount,
        long deliverySucceededCount,
        long deliveryFailedCount,
        long deliveryCanceledCount,
        long deliveryTimedOutCount,
        long deliverySkippedCount,
        TimeSpan totalHandlerDuration,
        long diagnosticWriteFailureCount)
    {
        ActiveSubscriptionCount = NonNegative(activeSubscriptionCount, nameof(activeSubscriptionCount));
        PublicationCount = NonNegative(publicationCount, nameof(publicationCount));
        DeliverySucceededCount = NonNegative(deliverySucceededCount, nameof(deliverySucceededCount));
        DeliveryFailedCount = NonNegative(deliveryFailedCount, nameof(deliveryFailedCount));
        DeliveryCanceledCount = NonNegative(deliveryCanceledCount, nameof(deliveryCanceledCount));
        DeliveryTimedOutCount = NonNegative(deliveryTimedOutCount, nameof(deliveryTimedOutCount));
        DeliverySkippedCount = NonNegative(deliverySkippedCount, nameof(deliverySkippedCount));
        TotalHandlerDuration = NonNegative(totalHandlerDuration, nameof(totalHandlerDuration));
        DiagnosticWriteFailureCount = NonNegative(diagnosticWriteFailureCount, nameof(diagnosticWriteFailureCount));
    }

    /// <summary>
    /// Gets active subscription count.
    /// </summary>
    public int ActiveSubscriptionCount { get; }
    /// <summary>
    /// Gets publication count.
    /// </summary>
    public long PublicationCount { get; }
    /// <summary>
    /// Gets delivery succeeded count.
    /// </summary>
    public long DeliverySucceededCount { get; }
    /// <summary>
    /// Gets delivery failed count.
    /// </summary>
    public long DeliveryFailedCount { get; }
    /// <summary>
    /// Gets delivery canceled count.
    /// </summary>
    public long DeliveryCanceledCount { get; }
    /// <summary>
    /// Gets delivery timed out count.
    /// </summary>
    public long DeliveryTimedOutCount { get; }
    /// <summary>
    /// Gets delivery skipped count.
    /// </summary>
    public long DeliverySkippedCount { get; }
    /// <summary>
    /// Gets total handler duration.
    /// </summary>
    public TimeSpan TotalHandlerDuration { get; }
    /// <summary>
    /// Gets diagnostic write failure count.
    /// </summary>
    public long DiagnosticWriteFailureCount { get; }

    private static int NonNegative(int value, string parameterName) => value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric values cannot be negative.");

    private static long NonNegative(long value, string parameterName) => value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric values cannot be negative.");

    private static TimeSpan NonNegative(TimeSpan value, string parameterName) => value >= TimeSpan.Zero
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric durations cannot be negative.");
}

/// <summary>
/// Defines the contract for ievent bus monitor.
/// </summary>
public interface IEventBusMonitor
{
    /// <summary>
    /// Executes the get snapshot operation.
    /// </summary>
    EventBusMetricsSnapshot GetSnapshot();
}
