namespace AtomUI.City.Presentation;

/// <summary>
/// Represents visual lifecycle event.
/// </summary>
public sealed record VisualLifecycleEvent
{
    /// <summary>
    /// Initializes a new instance of the <c>VisualLifecycleEvent</c> type.
    /// </summary>
    public VisualLifecycleEvent(object view, VisualLifecycleEventKind kind)
        : this(new VisualIdentity(view), kind)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>VisualLifecycleEvent</c> type.
    /// </summary>
    public VisualLifecycleEvent(VisualIdentity identity, VisualLifecycleEventKind kind)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Kind = kind;
    }

    /// <summary>
    /// Gets identity.
    /// </summary>
    public VisualIdentity Identity { get; }

    /// <summary>
    /// Gets view.
    /// </summary>
    public object View => Identity.View;

    /// <summary>
    /// Gets kind.
    /// </summary>
    public VisualLifecycleEventKind Kind { get; }
}
