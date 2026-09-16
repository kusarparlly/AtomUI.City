namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation queue options.
/// </summary>
public sealed class PresentationQueueOptions
{
    /// <summary>
    /// Represents the default outlet pending capacity value.
    /// </summary>
    public const int DefaultOutletPendingCapacity = 32;
    /// <summary>
    /// Represents the default modal interaction pending capacity value.
    /// </summary>
    public const int DefaultModalInteractionPendingCapacity = 8;

    /// <summary>
    /// Gets or sets outlet pending capacity.
    /// </summary>
    public int OutletPendingCapacity { get; init; } = DefaultOutletPendingCapacity;

    /// <summary>
    /// Gets or sets modal interaction pending capacity.
    /// </summary>
    public int ModalInteractionPendingCapacity { get; init; } = DefaultModalInteractionPendingCapacity;

    internal void Validate()
    {
        if (OutletPendingCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OutletPendingCapacity),
                "Outlet pending capacity must be positive.");
        }

        if (ModalInteractionPendingCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ModalInteractionPendingCapacity),
                "Modal interaction pending capacity must be positive.");
        }
    }
}
