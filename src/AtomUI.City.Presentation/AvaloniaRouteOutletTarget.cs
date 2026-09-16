using Avalonia.Controls;
using Avalonia.Threading;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents avalonia route outlet target.
/// </summary>
public sealed class AvaloniaRouteOutletTarget : IRouteOutletTarget
{
    private readonly ContentControl _control;

    /// <summary>
    /// Initializes a new instance of the <c>AvaloniaRouteOutletTarget</c> type.
    /// </summary>
    public AvaloniaRouteOutletTarget(ContentControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    /// <summary>
    /// Represents the content value.
    /// </summary>
    public object? Content
    {
        get
        {
            EnsureAccess();
            return _control.Content;
        }
    }

    /// <summary>
    /// Executes the set content operation.
    /// </summary>
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
