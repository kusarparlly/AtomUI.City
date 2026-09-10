using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public sealed class InteractionHandlerRegistrationOptions
{
    public IActivationScope? ActivationScope { get; init; }

    public InteractionHandlerScope Scope { get; init; } = InteractionHandlerScope.Presentation;

    public string? WindowId { get; init; }

    public string? RouteId { get; init; }

    public string? PluginId { get; init; }

    public string? ContributionId { get; init; }
}
