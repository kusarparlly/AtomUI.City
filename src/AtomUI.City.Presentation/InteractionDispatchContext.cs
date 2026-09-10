using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public enum InteractionHandlerScope
{
    Presentation,
    Window,
    Route,
    Activation,
}

public sealed record InteractionDispatchContext(
    string? WindowId = null,
    string? RouteId = null,
    IActivationScope? ActivationScope = null,
    bool IsModal = true)
{
    public static InteractionDispatchContext Global { get; } = new(IsModal: false);
}
