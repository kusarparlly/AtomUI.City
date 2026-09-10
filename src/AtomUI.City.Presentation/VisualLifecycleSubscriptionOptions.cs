using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public sealed class VisualLifecycleSubscriptionOptions
{
    public string? WindowId { get; init; }

    public string? OutletName { get; init; }

    public long? OperationId { get; init; }

    public string? EntryId { get; init; }

    public IActivationScope? ActivationScope { get; init; }
}
