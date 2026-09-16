namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event publish result.
/// </summary>
public sealed class EventPublishResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPublishResult"/> type.
    /// </summary>
    public EventPublishResult(
        Guid eventId,
        EventContractId contractId,
        IReadOnlyList<EventDeliveryResult> deliveries)
        : this(eventId, contractId, deliveries, TimeSpan.Zero)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPublishResult"/> type.
    /// </summary>
    public EventPublishResult(
        Guid eventId,
        EventContractId contractId,
        IReadOnlyList<EventDeliveryResult> deliveries,
        TimeSpan duration)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event publish result id cannot be empty.", nameof(eventId));
        }

        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentNullException.ThrowIfNull(deliveries);
        if (deliveries.Any(delivery => delivery is null))
        {
            throw new ArgumentException("Event publish result deliveries cannot contain null entries.", nameof(deliveries));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Event publication duration cannot be negative.");
        }

        EventId = eventId;
        ContractId = contractId;
        Deliveries = Array.AsReadOnly(deliveries.ToArray());
        Duration = duration;
    }

    /// <summary>
    /// Gets event id.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }

    /// <summary>
    /// Gets deliveries.
    /// </summary>
    public IReadOnlyList<EventDeliveryResult> Deliveries { get; }

    /// <summary>
    /// Gets duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets subscription count.
    /// </summary>
    public int SubscriptionCount => Deliveries.Count;

    /// <summary>
    /// Gets delivered count.
    /// </summary>
    public int DeliveredCount => Deliveries.Count(delivery => !delivery.Skipped);

    /// <summary>
    /// Gets failed count.
    /// </summary>
    public int FailedCount => Deliveries.Count(delivery =>
        !delivery.Succeeded && !delivery.Canceled && !delivery.Skipped && !delivery.TimedOut);

    /// <summary>
    /// Gets a value indicating whether canceled count.
    /// </summary>
    public int CanceledCount => Deliveries.Count(delivery =>
        delivery.Canceled && !delivery.Skipped && !delivery.TimedOut);

    /// <summary>
    /// Gets timed out count.
    /// </summary>
    public int TimedOutCount => Deliveries.Count(delivery => delivery.TimedOut);

    /// <summary>
    /// Gets skipped count.
    /// </summary>
    public int SkippedCount => Deliveries.Count(delivery => delivery.Skipped);

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => FailedCount == 0 && CanceledCount == 0 && TimedOutCount == 0 && SkippedCount == 0;
}

/// <summary>
/// Represents event delivery result.
/// </summary>
public sealed record EventDeliveryResult(
    EventSubscriptionId SubscriptionId,
    EventDispatchPolicy DispatchPolicy,
    bool Succeeded,
    string? ErrorMessage = null,
    bool Canceled = false)
{
    private TimeSpan _duration;

    /// <summary>
    /// Represents the duration value.
    /// </summary>
    public TimeSpan Duration
    {
        get => _duration;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Duration), value, "Event delivery duration cannot be negative.");
            }

            _duration = value;
        }
    }

    private bool _timedOut;

    /// <summary>
    /// Represents the timed out value.
    /// </summary>
    public bool TimedOut
    {
        get => _timedOut;
        init
        {
            if (value && Succeeded)
            {
                throw new ArgumentException("Successful event delivery result cannot be timed out.", nameof(TimedOut));
            }

            if (value && Skipped)
            {
                throw new ArgumentException("Event delivery result cannot be both timed out and skipped.", nameof(TimedOut));
            }

            _timedOut = value;
        }
    }

    private bool _skipped;

    /// <summary>
    /// Represents the skipped value.
    /// </summary>
    public bool Skipped
    {
        get => _skipped;
        init
        {
            if (value && Succeeded)
            {
                throw new ArgumentException("Successful event delivery result cannot be skipped.", nameof(Skipped));
            }

            if (value && TimedOut)
            {
                throw new ArgumentException("Event delivery result cannot be both skipped and timed out.", nameof(Skipped));
            }

            _skipped = value;
        }
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public EventDeliveryStatus Status => Skipped
        ? EventDeliveryStatus.Skipped
        : TimedOut
            ? EventDeliveryStatus.TimedOut
            : Succeeded
                ? EventDeliveryStatus.Succeeded
                : Canceled
                    ? EventDeliveryStatus.Canceled
                    : EventDeliveryStatus.Failed;

    private EventSubscriptionId _subscriptionId = ValidateSubscriptionId(SubscriptionId);

    /// <summary>
    /// Represents the subscription id value.
    /// </summary>
    public EventSubscriptionId SubscriptionId
    {
        get => _subscriptionId;
        init => _subscriptionId = ValidateSubscriptionId(value);
    }

    private EventDispatchPolicy _dispatchPolicy = ValidateDispatchPolicy(DispatchPolicy);

    /// <summary>
    /// Represents the dispatch policy value.
    /// </summary>
    public EventDispatchPolicy DispatchPolicy
    {
        get => _dispatchPolicy;
        init => _dispatchPolicy = ValidateDispatchPolicy(value);
    }

    private bool _succeeded = ValidateSucceeded(Succeeded, Canceled, ErrorMessage, timedOut: false, skipped: false);

    /// <summary>
    /// Represents the succeeded value.
    /// </summary>
    public bool Succeeded
    {
        get => _succeeded;
        init => _succeeded = ValidateSucceeded(value, Canceled, ErrorMessage, TimedOut, Skipped);
    }

    private string? _errorMessage = ValidateErrorMessage(Succeeded, ErrorMessage);

    /// <summary>
    /// Represents the error message value.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        init => _errorMessage = ValidateErrorMessage(Succeeded, value);
    }

    private bool _canceled = ValidateCanceled(Succeeded, Canceled);

    /// <summary>
    /// Represents the canceled value.
    /// </summary>
    public bool Canceled
    {
        get => _canceled;
        init => _canceled = ValidateCanceled(Succeeded, value);
    }

    private static EventSubscriptionId ValidateSubscriptionId(EventSubscriptionId subscriptionId)
    {
        EventSubscriptionId.ThrowIfDefault(subscriptionId, nameof(SubscriptionId));

        return subscriptionId;
    }

    private static EventDispatchPolicy ValidateDispatchPolicy(EventDispatchPolicy dispatchPolicy)
    {
        if (!Enum.IsDefined(dispatchPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DispatchPolicy),
                dispatchPolicy,
                "Event delivery dispatch policy is not supported.");
        }

        return dispatchPolicy;
    }

    private static string? ValidateErrorMessage(bool succeeded, string? errorMessage)
    {
        if (succeeded && errorMessage is not null)
        {
            throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
        }

        return errorMessage;
    }

    private static bool ValidateCanceled(bool succeeded, bool canceled)
    {
        if (succeeded && canceled)
        {
            throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Canceled));
        }

        return canceled;
    }

    private static bool ValidateSucceeded(
        bool succeeded,
        bool canceled,
        string? errorMessage,
        bool timedOut,
        bool skipped)
    {
        if (succeeded && canceled)
        {
            throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Succeeded));
        }

        if (succeeded && errorMessage is not null)
        {
            throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
        }

        if (succeeded && timedOut)
        {
            throw new ArgumentException("Successful event delivery result cannot be timed out.", nameof(Succeeded));
        }

        if (succeeded && skipped)
        {
            throw new ArgumentException("Successful event delivery result cannot be skipped.", nameof(Succeeded));
        }

        return succeeded;
    }
}
