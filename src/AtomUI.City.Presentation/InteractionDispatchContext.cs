using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported interaction handler scope values.
/// </summary>
public enum InteractionHandlerScope
{
    /// <summary>
    /// Represents the presentation value.
    /// </summary>
    Presentation,
    /// <summary>
    /// Represents the window value.
    /// </summary>
    Window,
    /// <summary>
    /// Represents the route value.
    /// </summary>
    Route,
    /// <summary>
    /// Represents the activation value.
    /// </summary>
    Activation,
}

/// <summary>
/// Represents interaction dispatch context.
/// </summary>
/// <param name="WindowId">The window id value.</param>
/// <param name="RouteId">The route id value.</param>
/// <param name="ActivationScope">The activation scope value.</param>
/// <param name="IsModal">The is modal value.</param>
public sealed record InteractionDispatchContext(
    string? WindowId = null,
    string? RouteId = null,
    IActivationScope? ActivationScope = null,
    bool IsModal = true)
{
    /// <summary>
    /// Gets global.
    /// </summary>
    public static InteractionDispatchContext Global { get; } = new(IsModal: false);
}
