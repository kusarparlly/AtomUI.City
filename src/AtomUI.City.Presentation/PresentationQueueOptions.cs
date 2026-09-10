namespace AtomUI.City.Presentation;

public sealed class PresentationQueueOptions
{
    public const int DefaultOutletPendingCapacity = 32;
    public const int DefaultModalInteractionPendingCapacity = 8;

    public int OutletPendingCapacity { get; init; } = DefaultOutletPendingCapacity;

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
