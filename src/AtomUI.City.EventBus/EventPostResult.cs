namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event post result.
/// </summary>
public sealed record EventPostResult(
    Guid EventId,
    EventContractId ContractId,
    bool Accepted,
    string? RejectionReason = null)
{
    private Guid _eventId = ValidateEventId(EventId);

    /// <summary>
    /// Represents the event id value.
    /// </summary>
    public Guid EventId
    {
        get => _eventId;
        init => _eventId = ValidateEventId(value);
    }

    private EventContractId _contractId = ValidateContractId(ContractId);

    /// <summary>
    /// Represents the contract id value.
    /// </summary>
    public EventContractId ContractId
    {
        get => _contractId;
        init => _contractId = ValidateContractId(value);
    }

    private bool _accepted = ValidateAccepted(Accepted, RejectionReason);

    /// <summary>
    /// Represents the accepted value.
    /// </summary>
    public bool Accepted
    {
        get => _accepted;
        init => _accepted = ValidateAccepted(value, RejectionReason);
    }

    private string? _rejectionReason = ValidateRejectionReason(Accepted, RejectionReason);

    /// <summary>
    /// Represents the rejection reason value.
    /// </summary>
    public string? RejectionReason
    {
        get => _rejectionReason;
        init => _rejectionReason = ValidateRejectionReason(Accepted, value);
    }

    private static Guid ValidateEventId(Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event post result id cannot be empty.", nameof(EventId));
        }

        return eventId;
    }

    private static EventContractId ValidateContractId(EventContractId contractId)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(ContractId));

        return contractId;
    }

    private static bool ValidateAccepted(bool accepted, string? rejectionReason)
    {
        if (accepted && rejectionReason is not null)
        {
            throw new ArgumentException("Accepted event post result cannot include a rejection reason.", nameof(Accepted));
        }

        if (!accepted && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejected event post result must include a rejection reason.", nameof(Accepted));
        }

        return accepted;
    }

    private static string? ValidateRejectionReason(
        bool accepted,
        string? rejectionReason)
    {
        if (accepted && rejectionReason is not null)
        {
            throw new ArgumentException("Accepted event post result cannot include a rejection reason.", nameof(RejectionReason));
        }

        if (!accepted && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejected event post result must include a rejection reason.", nameof(RejectionReason));
        }

        return rejectionReason;
    }
}
