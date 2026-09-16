namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event channel options.
/// </summary>
public sealed class EventChannelOptions
{
    /// <summary>
    /// Represents the default capacity value.
    /// </summary>
    public const int DefaultCapacity = 256;
    private static readonly TimeSpan MaximumQueueWaitTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventChannelOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets capacity.
    /// </summary>
    public int Capacity { get; init; } = DefaultCapacity;

    /// <summary>
    /// Gets or sets backpressure policy.
    /// </summary>
    public EventChannelBackpressurePolicy BackpressurePolicy { get; init; } =
        EventChannelBackpressurePolicy.Wait;

    /// <summary>
    /// Gets or sets execution mode.
    /// </summary>
    public EventChannelExecutionMode ExecutionMode { get; init; } =
        EventChannelExecutionMode.Serialized;

    /// <summary>
    /// Gets or sets maximum concurrency.
    /// </summary>
    public int MaximumConcurrency { get; init; } = 1;

    /// <summary>
    /// Gets or sets queue wait timeout.
    /// </summary>
    public TimeSpan? QueueWaitTimeout { get; init; }

    internal void Validate()
    {
        if (Capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Capacity),
                Capacity,
                "Event channel capacity must be greater than zero.");
        }

        if (!Enum.IsDefined(BackpressurePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackpressurePolicy),
                BackpressurePolicy,
                "Event channel backpressure policy is not supported.");
        }

        if (!Enum.IsDefined(ExecutionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExecutionMode),
                ExecutionMode,
                "Event channel execution mode is not supported.");
        }

        if (MaximumConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumConcurrency),
                MaximumConcurrency,
                "Event channel maximum concurrency must be greater than zero.");
        }

        if (ExecutionMode == EventChannelExecutionMode.Serialized && MaximumConcurrency != 1)
        {
            throw new ArgumentException(
                "Serialized event channels require MaximumConcurrency to be exactly one.",
                nameof(MaximumConcurrency));
        }

        if (QueueWaitTimeout is { } timeout &&
            (timeout <= TimeSpan.Zero || timeout > MaximumQueueWaitTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueWaitTimeout),
                timeout,
                $"Event channel queue wait timeout must be greater than zero and no greater than {MaximumQueueWaitTimeout} when specified.");
        }
    }
}
