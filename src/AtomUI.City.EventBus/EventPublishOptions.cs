namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event publish options.
/// </summary>
public sealed class EventPublishOptions
{
    private string? _correlationId;
    private string? _causationId;
    private int _publishDepth;
    private string? _partitionKey;

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventPublishOptions Default { get; } = new();

    /// <summary>
    /// Represents the correlation id value.
    /// </summary>
    public string? CorrelationId
    {
        get => _correlationId;
        init => _correlationId = EventCorrelationIds.ValidateOptional(value, nameof(CorrelationId));
    }

    /// <summary>
    /// Represents the causation id value.
    /// </summary>
    public string? CausationId
    {
        get => _causationId;
        init => _causationId = EventCorrelationIds.ValidateOptional(value, nameof(CausationId));
    }

    /// <summary>
    /// Represents the publish depth value.
    /// </summary>
    public int PublishDepth
    {
        get => _publishDepth;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Event publish depth cannot be negative.");
            }

            _publishDepth = value;
        }
    }

    /// <summary>
    /// Represents the partition key value.
    /// </summary>
    public string? PartitionKey
    {
        get => _partitionKey;
        init => _partitionKey = EventCorrelationIds.ValidateOptional(value, nameof(PartitionKey));
    }
}
