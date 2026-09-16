using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents interaction handler registration options.
/// </summary>
public sealed class InteractionHandlerRegistrationOptions
{
    /// <summary>
    /// Gets or sets activation scope.
    /// </summary>
    public IActivationScope? ActivationScope { get; init; }

    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public InteractionHandlerScope Scope { get; init; } = InteractionHandlerScope.Presentation;

    /// <summary>
    /// Gets or sets window id.
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// Gets or sets route id.
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// Gets or sets plugin id.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Gets or sets contribution id.
    /// </summary>
    public string? ContributionId { get; init; }
}
