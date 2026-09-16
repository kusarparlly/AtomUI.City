namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event contract id.
/// </summary>
public readonly record struct EventContractId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventContractId"/> type.
    /// </summary>
    public EventContractId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value != value.Trim())
        {
            throw new ArgumentException("Event contract id cannot contain surrounding whitespace.", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Event contract id cannot contain control characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => Value;

    internal static void ThrowIfDefault(EventContractId contractId, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(contractId.Value))
        {
            throw new ArgumentException("Event contract id must be created with a non-empty value.", paramName);
        }
    }
}
