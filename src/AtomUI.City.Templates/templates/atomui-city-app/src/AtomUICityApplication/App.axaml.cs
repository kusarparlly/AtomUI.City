using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AtomUICityApplication;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            throw new InvalidOperationException(
                "This application requires the Avalonia classic desktop lifetime.");
        }

        DesktopBootstrap.Initialize(desktopLifetime);
        base.OnFrameworkInitializationCompleted();
    }
}
