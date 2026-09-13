using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUICityApplication;

internal static class DesktopBootstrap
{
    private static IApplicationHost? _host;

    internal static void Attach(IApplicationHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Interlocked.CompareExchange(ref _host, host, null) is not null)
        {
            throw new InvalidOperationException("A desktop application Host is already attached.");
        }
    }

    internal static void Initialize(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        var host = Volatile.Read(ref _host)
            ?? throw new InvalidOperationException("The desktop application Host is not attached.");
        if (lifetime.MainWindow is not null)
        {
            throw new InvalidOperationException("The desktop lifetime already has a main window.");
        }

        var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
        runtime.Attach(lifetime, host.HostScope);
        var mainWindow = host.Services.GetRequiredService<MainWindow>();
        var session = runtime.RegisterWindow(mainWindow, "main");
        mainWindow.Closed += (_, _) => _ = ShutdownWhenSessionCompletesAsync(session, lifetime);
        lifetime.MainWindow = mainWindow;
    }

    private static async Task ShutdownWhenSessionCompletesAsync(
        WindowSession session,
        IClassicDesktopStyleApplicationLifetime lifetime)
    {
        while (session.State is not (WindowSessionState.Closed or WindowSessionState.Faulted))
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        var exitCode = session.State == WindowSessionState.Closed ? 0 : 1;
        Dispatcher.UIThread.Post(() => lifetime.Shutdown(exitCode));
    }

    internal static void Detach(IApplicationHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _host, null, host), host))
        {
            throw new InvalidOperationException("The supplied desktop application Host is not attached.");
        }
    }
}
