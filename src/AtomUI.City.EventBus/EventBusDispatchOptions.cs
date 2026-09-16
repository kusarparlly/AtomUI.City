namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus dispatch options.
/// </summary>
public sealed class EventBusDispatchOptions
{
    /// <summary>
    /// Represents the default maximum concurrent deliveries per publication value.
    /// </summary>
    public const int DefaultMaximumConcurrentDeliveriesPerPublication = 16;

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventBusDispatchOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets maximum concurrent deliveries per publication.
    /// </summary>
    public int MaximumConcurrentDeliveriesPerPublication { get; init; } =
        DefaultMaximumConcurrentDeliveriesPerPublication;

    internal void Validate()
    {
        if (MaximumConcurrentDeliveriesPerPublication is <= 0 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumConcurrentDeliveriesPerPublication),
                MaximumConcurrentDeliveriesPerPublication,
                "Maximum concurrent deliveries per publication must be between 1 and 1024.");
        }
    }
}
