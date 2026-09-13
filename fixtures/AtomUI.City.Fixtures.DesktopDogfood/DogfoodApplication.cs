using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using AtomUI.City.Core.Threading;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodApplication : Application
{
    private int _shutdownRequested;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        base.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT avalonia-lifetime-ready");
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            throw new InvalidOperationException("DesktopDogfood requires the classic desktop lifetime.");
        }

        var host = DogfoodBootstrap.Host;
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT host-acquired");
        var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT runtime-resolved");
        var dispatcher = host.Services.GetRequiredService<IUiDispatcher>();
        if (dispatcher is not AvaloniaUiDispatcher)
        {
            throw new InvalidOperationException(
                $"Presentation did not replace the Core dispatcher fallback; actual={dispatcher.GetType().FullName}.");
        }
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT avalonia-dispatcher-resolved");
        runtime.Attach(desktopLifetime, host.HostScope);
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT runtime-attached");

        var mainWindow = host.Services.GetRequiredService<DogfoodWindowFactory>().CreateMainWindow();
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT window-created");
        var session = runtime.RegisterWindow(mainWindow, "main");
        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT window-registered");
        var coordinator = host.Services.GetRequiredService<DogfoodDesktopCoordinator>();

        mainWindow.Opened += (_, _) =>
        {
            _ = StartDesktopAsync(coordinator, mainWindow, session, desktopLifetime);
        };
        mainWindow.Closed += (_, _) =>
        {
            _ = CompleteShutdownAfterWindowCleanupAsync(coordinator, session, desktopLifetime);
        };
        desktopLifetime.MainWindow = mainWindow;

        base.OnFrameworkInitializationCompleted();
        Dispatcher.UIThread.Post(() =>
        {
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }
        });
    }

    private async Task StartDesktopAsync(
        DogfoodDesktopCoordinator coordinator,
        DogfoodMainWindow mainWindow,
        WindowSession session,
        IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        try
        {
            Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT window-opened");
            await coordinator.InitializeAsync(mainWindow, session).ConfigureAwait(true);
            if (DogfoodBootstrap.Options.IsAutomated || DogfoodBootstrap.Options.IsExternallyDriven)
            {
                Console.WriteLine(
                    $"DESKTOP_DOGFOOD_READY profile={DogfoodBootstrap.Options.Profile.ToString().ToLowerInvariant()} windows=4 outlets=12 modules=48 services=110");
            }

            if (DogfoodBootstrap.Options.IsAutomated)
            {
                await coordinator.CloseAuxiliaryWindowsAsync().ConfigureAwait(true);
                await coordinator.CloseMainWindowAsync(session).ConfigureAwait(true);

                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT window-session-closed");
                RequestShutdown(desktopLifetime, 0);
            }
        }
        catch (Exception exception)
        {
            DogfoodBootstrap.Host.Services.GetRequiredService<DogfoodRunLedger>().RecordFailure(exception);
            Console.Error.WriteLine($"DESKTOP_DOGFOOD_FAILED {exception}");
            mainWindow.SetStatus("Startup failed", exception.Message, isError: true);

            if (!DogfoodBootstrap.Options.KeepOpenOnFailure)
            {
                try
                {
                    await coordinator.CloseAuxiliaryWindowsAsync().ConfigureAwait(true);
                    await session.DisposeAsync().ConfigureAwait(true);
                }
                catch (Exception cleanupException)
                {
                    Console.Error.WriteLine($"DESKTOP_DOGFOOD_CLEANUP_FAILED {cleanupException}");
                }

                RequestShutdown(desktopLifetime, 1);
            }
        }
    }

    private async Task CompleteShutdownAfterWindowCleanupAsync(
        DogfoodDesktopCoordinator coordinator,
        WindowSession session,
        IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        await coordinator.CloseAuxiliaryWindowsAsync().ConfigureAwait(false);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (session.State is not (WindowSessionState.Closed or WindowSessionState.Faulted) &&
               DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }

        RequestShutdown(
            desktopLifetime,
            session.State == WindowSessionState.Closed ? 0 : 1);
    }

    private void RequestShutdown(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        int exitCode)
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
        {
            var hasAccess = Dispatcher.UIThread.CheckAccess();
            Console.WriteLine($"DESKTOP_DOGFOOD_CHECKPOINT avalonia-shutdown-requested exit={exitCode} ui={hasAccess}");
            if (hasAccess)
            {
                desktopLifetime.Shutdown(exitCode);
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT avalonia-shutdown-dispatched");
                    desktopLifetime.Shutdown(exitCode);
                });
            }
        }
    }
}
