using AtomUI.City.Routing;

namespace AtomUI.City.Security;

/// <summary>
/// Represents security route guard options.
/// </summary>
public sealed class SecurityRouteGuardOptions
{
    /// <summary>
    /// Gets or sets login route id.
    /// </summary>
    public string? LoginRouteId { get; init; }

    /// <summary>
    /// Gets or sets login navigation options.
    /// </summary>
    public NavigationOptions LoginNavigationOptions { get; init; } = NavigationOptions.Default;
}
