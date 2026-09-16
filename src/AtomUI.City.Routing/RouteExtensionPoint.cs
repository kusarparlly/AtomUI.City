namespace AtomUI.City.Routing;

/// <summary>
/// Represents route extension point.
/// </summary>
public readonly record struct RouteExtensionPoint
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteExtensionPoint</c> type.
    /// </summary>
    public RouteExtensionPoint(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => Id ?? string.Empty;
}
