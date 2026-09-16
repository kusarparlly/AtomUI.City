namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event context&lt;tevent&gt;.
/// </summary>
public sealed class EventContext<TEvent>
{
    /// <summary>
    /// Executes the event context operation.
    /// </summary>
    public EventContext(
        TEvent eventData,
        EventContractId contractId,
        Guid eventId,
        string correlationId,
        string? causationId,
        DateTimeOffset publishedAt,
        int publishDepth,
        EventSubscriptionId subscriptionId,
        EventDispatchPolicy dispatchPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event context id cannot be empty.", nameof(eventId));
        }
        correlationId = EventCorrelationIds.ValidateRequired(correlationId, nameof(correlationId));
        causationId = EventCorrelationIds.ValidateOptional(causationId, nameof(causationId));
        if (publishDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishDepth),
                publishDepth,
                "Event context publish depth cannot be negative.");
        }
        EventSubscriptionId.ThrowIfDefault(subscriptionId, nameof(subscriptionId));
        if (!Enum.IsDefined(dispatchPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchPolicy),
                dispatchPolicy,
                "Event context dispatch policy is not supported.");
        }

        Event = eventData;
        ContractId = contractId;
        EventId = eventId;
        CorrelationId = correlationId;
        CausationId = causationId;
        PublishedAt = publishedAt;
        PublishDepth = publishDepth;
        SubscriptionId = subscriptionId;
        DispatchPolicy = dispatchPolicy;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Occurs when event.
    /// </summary>
    public TEvent Event { get; }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }

    /// <summary>
    /// Gets event id.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets correlation id.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets causation id.
    /// </summary>
    public string? CausationId { get; }

    /// <summary>
    /// Gets published at.
    /// </summary>
    public DateTimeOffset PublishedAt { get; }

    /// <summary>
    /// Gets publish depth.
    /// </summary>
    public int PublishDepth { get; }

    /// <summary>
    /// Gets subscription id.
    /// </summary>
    public EventSubscriptionId SubscriptionId { get; }

    /// <summary>
    /// Gets dispatch policy.
    /// </summary>
    public EventDispatchPolicy DispatchPolicy { get; }

    /// <summary>
    /// Gets a value indicating whether cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
