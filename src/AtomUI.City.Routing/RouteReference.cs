namespace AtomUI.City.Routing;

/// <summary>
/// Represents route reference.
/// </summary>
public readonly record struct RouteReference
{
    /// <summary>
    /// Initializes a new instance of the <c>RouteReference</c> type.
    /// </summary>
    public RouteReference(string id)
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
