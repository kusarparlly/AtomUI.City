namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event channel metrics snapshot.
/// </summary>
public sealed record EventChannelMetricsSnapshot
{
    private TimeSpan _totalQueueWaitDuration;
    private TimeSpan _maximumQueueWaitDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventChannelMetricsSnapshot"/> type.
    /// </summary>
    public EventChannelMetricsSnapshot(
        EventContractId contractId,
        string channelName,
        EventChannelExecutionMode executionMode,
        int capacity,
        int pendingCount,
        int inFlightCount,
        long acceptedCount,
        long rejectedCount,
        long droppedCount,
        long completedCount,
        long failedCount)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ContractId = contractId;
        ChannelName = EventAttributeValidation.ValidateName(channelName, nameof(channelName));
        ExecutionMode = Enum.IsDefined(executionMode)
            ? executionMode
            : throw new ArgumentOutOfRangeException(nameof(executionMode), executionMode, "Unknown channel execution mode.");
        Capacity = capacity > 0
            ? capacity
            : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Channel capacity must be positive.");
        PendingCount = NonNegative(pendingCount, nameof(pendingCount));
        InFlightCount = NonNegative(inFlightCount, nameof(inFlightCount));
        AcceptedCount = NonNegative(acceptedCount, nameof(acceptedCount));
        RejectedCount = NonNegative(rejectedCount, nameof(rejectedCount));
        DroppedCount = NonNegative(droppedCount, nameof(droppedCount));
        CompletedCount = NonNegative(completedCount, nameof(completedCount));
        FailedCount = NonNegative(failedCount, nameof(failedCount));
    }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }
    /// <summary>
    /// Gets channel name.
    /// </summary>
    public string ChannelName { get; }
    /// <summary>
    /// Gets execution mode.
    /// </summary>
    public EventChannelExecutionMode ExecutionMode { get; }
    /// <summary>
    /// Gets capacity.
    /// </summary>
    public int Capacity { get; }
    /// <summary>
    /// Gets pending count.
    /// </summary>
    public int PendingCount { get; }
    /// <summary>
    /// Gets in flight count.
    /// </summary>
    public int InFlightCount { get; }
    /// <summary>
    /// Gets accepted count.
    /// </summary>
    public long AcceptedCount { get; }
    /// <summary>
    /// Gets rejected count.
    /// </summary>
    public long RejectedCount { get; }
    /// <summary>
    /// Gets dropped count.
    /// </summary>
    public long DroppedCount { get; }
    /// <summary>
    /// Gets completed count.
    /// </summary>
    public long CompletedCount { get; }
    /// <summary>
    /// Gets failed count.
    /// </summary>
    public long FailedCount { get; }

    /// <summary>
    /// Represents the total queue wait duration value.
    /// </summary>
    public TimeSpan TotalQueueWaitDuration
    {
        get => _totalQueueWaitDuration;
        init => _totalQueueWaitDuration = NonNegative(value, nameof(TotalQueueWaitDuration));
    }

    /// <summary>
    /// Represents the maximum queue wait duration value.
    /// </summary>
    public TimeSpan MaximumQueueWaitDuration
    {
        get => _maximumQueueWaitDuration;
        init => _maximumQueueWaitDuration = NonNegative(value, nameof(MaximumQueueWaitDuration));
    }

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
/// Defines the contract for ievent channel monitor.
/// </summary>
public interface IEventChannelMonitor
{
    /// <summary>
    /// Executes the get channel snapshots operation.
    /// </summary>
    IReadOnlyList<EventChannelMetricsSnapshot> GetChannelSnapshots();
}
