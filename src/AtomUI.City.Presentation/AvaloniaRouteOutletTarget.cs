using Avalonia.Controls;
using Avalonia.Threading;

namespace AtomUI.City.Presentation;

public sealed class AvaloniaRouteOutletTarget : IRouteOutletTarget
{
    private readonly ContentControl _control;

    public AvaloniaRouteOutletTarget(ContentControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public object? Content
    {
        get
        {
            EnsureAccess();
            return _control.Content;
        }
    }

    public void SetContent(object? content)
    {
        EnsureAccess();
        _control.Content = content;
    }

    private static void EnsureAccess()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw new PresentationException(
                PresentationError.DispatcherUnavailable,
                "Avalonia outlet content must be accessed on the UI dispatcher thread.");
        }
    }
}
