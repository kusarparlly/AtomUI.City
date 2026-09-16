namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event channel.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class EventChannelAttribute : Attribute
{
    private int _capacity = EventChannelOptions.DefaultCapacity;
    private EventChannelBackpressurePolicy _backpressurePolicy = EventChannelBackpressurePolicy.Wait;
    private EventChannelExecutionMode _executionMode = EventChannelExecutionMode.Serialized;
    private int _maximumConcurrency = 1;
    private int _queueWaitTimeoutMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventChannelAttribute"/> type.
    /// </summary>
    public EventChannelAttribute(string name)
    {
        Name = EventAttributeValidation.ValidateName(name, nameof(name));
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets capacity.
    /// </summary>
    public int Capacity { get => _capacity; init => _capacity = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }

    /// <summary>
    /// Represents the backpressure policy value.
    /// </summary>
    public EventChannelBackpressurePolicy BackpressurePolicy
    {
        get => _backpressurePolicy;
        init => _backpressurePolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Represents the execution mode value.
    /// </summary>
    public EventChannelExecutionMode ExecutionMode
    {
        get => _executionMode;
        init => _executionMode = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets maximum concurrency.
    /// </summary>
    public int MaximumConcurrency { get => _maximumConcurrency; init => _maximumConcurrency = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }

    /// <summary>
    /// Represents the queue wait timeout milliseconds value.
    /// </summary>
    public int QueueWaitTimeoutMilliseconds
    {
        get => _queueWaitTimeoutMilliseconds;
        init => _queueWaitTimeoutMilliseconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
