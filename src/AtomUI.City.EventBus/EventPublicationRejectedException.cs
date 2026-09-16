namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event publication rejected exception.
/// </summary>
public sealed class EventPublicationRejectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPublicationRejectedException"/> type.
    /// </summary>
    public EventPublicationRejectedException(
        Guid eventId,
        EventContractId contractId,
        string reason)
        : base(reason)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Rejected publication event id cannot be empty.", nameof(eventId));
        }

        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        EventId = eventId;
        ContractId = contractId;
    }

    /// <summary>
    /// Gets event id.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public EventContractId ContractId { get; }
}
