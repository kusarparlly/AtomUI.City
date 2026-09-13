using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUICityApplication;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static IApplicationHost CreateHost(string[] args)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUICityApplication";
            options.ApplicationName = "AtomUICityApplication";
        });
        builder.UseModule<PresentationModule>();
        builder.ConfigureServices(services => services.AddSingleton<MainWindow>());

        return builder.Build();
    }

    internal static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect();

    private static int Run(string[] args)
    {
        using var host = CreateHost(args);
        host.StartAsync().GetAwaiter().GetResult();

        var attached = false;
        try
        {
            DesktopBootstrap.Attach(host);
            attached = true;
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            if (attached)
            {
                DesktopBootstrap.Detach(host);
            }

            Task.Run(async () => await host.StopAsync().ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();
        }
    }
}
