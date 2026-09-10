using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace AtomUI.City.Presentation;

internal sealed class AvaloniaVisualLifecycleSubscription : IDisposable
{
    private readonly Visual _visual;
    private readonly VisualLifecycleHub _hub;
    private readonly VisualIdentity _identity;
    private int _disposed;

    private AvaloniaVisualLifecycleSubscription(
        Visual visual,
        VisualLifecycleHub hub,
        VisualIdentity identity)
    {
        _visual = visual;
        _hub = hub;
        _identity = identity;
        _visual.AttachedToVisualTree += HandleAttached;
        _visual.DetachedFromVisualTree += HandleDetached;
    }

    public static IDisposable? TryCreate(
        object view,
        VisualLifecycleHub? hub,
        VisualIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(identity);

        if (view is Visual && !Dispatcher.UIThread.CheckAccess())
        {
            throw new PresentationException(
                PresentationError.DispatcherUnavailable,
                "Avalonia visual lifecycle events must be subscribed on the UI dispatcher thread.");
        }

        return hub is not null && view is Visual visual
            ? new AvaloniaVisualLifecycleSubscription(visual, hub, identity)
            : null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _visual.AttachedToVisualTree -= HandleAttached;
        _visual.DetachedFromVisualTree -= HandleDetached;
    }

    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _hub.Notify(_identity, VisualLifecycleEventKind.Attached);
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _hub.Notify(_identity, VisualLifecycleEventKind.Detached);
    }
}
