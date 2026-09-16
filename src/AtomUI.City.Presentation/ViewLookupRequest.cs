namespace AtomUI.City.Presentation;

/// <summary>
/// Represents view lookup request.
/// </summary>
public sealed class ViewLookupRequest
{
    /// <summary>
    /// Initializes a new instance of the <c>ViewLookupRequest</c> type.
    /// </summary>
    public ViewLookupRequest(
        Type viewModelType,
        string? viewKey = null,
        string? routeId = null,
        string? ownerId = null)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        ViewModelType = viewModelType;
        ViewKey = string.IsNullOrWhiteSpace(viewKey) ? null : viewKey;
        RouteId = string.IsNullOrWhiteSpace(routeId) ? null : routeId;
        OwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId;
    }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type ViewModelType { get; }

    /// <summary>
    /// Gets view key.
    /// </summary>
    public string? ViewKey { get; }

    /// <summary>
    /// Gets route id.
    /// </summary>
    public string? RouteId { get; }

    /// <summary>
    /// Gets owner id.
    /// </summary>
    public string? OwnerId { get; }
}
