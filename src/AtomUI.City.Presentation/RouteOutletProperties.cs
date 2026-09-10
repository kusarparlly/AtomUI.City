using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AtomUI.City.Presentation;

public sealed class RouteOutletProperties : AvaloniaObject
{
    public static readonly AttachedProperty<string?> NameProperty =
        AvaloniaProperty.RegisterAttached<RouteOutletProperties, Control, string?>("Name");

    private static readonly AttachedProperty<WindowSession?> WindowSessionProperty =
        AvaloniaProperty.RegisterAttached<RouteOutletProperties, Window, WindowSession?>("WindowSession");

    private static readonly AttachedProperty<IDisposable?> RegistrationProperty =
        AvaloniaProperty.RegisterAttached<RouteOutletProperties, Control, IDisposable?>("Registration");

    static RouteOutletProperties()
    {
        NameProperty.Changed.AddClassHandler<Control>(HandleNameChanged);
    }

    private RouteOutletProperties()
    {
    }

    public static string? GetName(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(NameProperty);
    }

    public static void SetName(Control control, string? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(NameProperty, value);
    }

    internal static void SetWindowSession(Window window, WindowSession? session)
    {
        window.SetValue(WindowSessionProperty, session);
    }

    private static void HandleNameChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.AttachedToVisualTree -= HandleAttached;
        control.DetachedFromVisualTree -= HandleDetached;
        DisposeRegistration(control);

        if (string.IsNullOrWhiteSpace(args.GetNewValue<string?>()))
        {
            return;
        }

        control.AttachedToVisualTree += HandleAttached;
        control.DetachedFromVisualTree += HandleDetached;
        TryRegister(control);
    }

    private static void HandleAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control)
        {
            TryRegister(control);
        }
    }

    private static void HandleDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control)
        {
            DisposeRegistration(control);
        }
    }

    private static void TryRegister(Control control)
    {
        if (control.GetValue(RegistrationProperty) is not null || control is not ContentControl contentControl)
        {
            return;
        }

        var name = GetName(control);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (TopLevel.GetTopLevel(control) is not Window window)
        {
            return;
        }

        var session = window.GetValue(WindowSessionProperty);
        if (session is null)
        {
            return;
        }

        control.SetValue(
            RegistrationProperty,
            session.RegisterOutlet(name, new AvaloniaRouteOutletTarget(contentControl)));
    }

    private static void DisposeRegistration(Control control)
    {
        var registration = control.GetValue(RegistrationProperty);
        control.ClearValue(RegistrationProperty);
        registration?.Dispose();
    }
}
