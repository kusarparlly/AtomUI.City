namespace AtomUI.City.Presentation;

public sealed record VisualLifecycleEvent
{
    public VisualLifecycleEvent(object view, VisualLifecycleEventKind kind)
        : this(new VisualIdentity(view), kind)
    {
    }

    public VisualLifecycleEvent(VisualIdentity identity, VisualLifecycleEventKind kind)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Kind = kind;
    }

    public VisualIdentity Identity { get; }

    public object View => Identity.View;

    public VisualLifecycleEventKind Kind { get; }
}
