namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event subscription id.
/// </summary>
public readonly record struct EventSubscriptionId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubscriptionId"/> type.
    /// </summary>
    public EventSubscriptionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Subscription id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Gets new.
    /// </summary>
    public static EventSubscriptionId New() => new(Guid.NewGuid());

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => Value.ToString("D");

    internal static void ThrowIfDefault(EventSubscriptionId subscriptionId, string? paramName = null)
    {
        if (subscriptionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Subscription id must be created with a non-empty value.", paramName);
        }
    }
}
