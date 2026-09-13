using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUICityApplication.Tests;

public sealed class ApplicationSmokeTests
{
    [Fact]
    public async Task ApplicationHostStartsAndStops()
    {
        using var host = Program.CreateHost([]);
        await host.StartAsync();
        Assert.Equal(LifecycleScopeState.Running, host.HostScope.State);

        await host.StopAsync();
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
    }

    [Fact]
    public async Task DesktopBootstrapAttachesPresentationAndRegistersMainWindow()
    {
        using var host = Program.CreateHost([]);
        await host.StartAsync();
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        Assert.Throws<InvalidOperationException>(() => DesktopBootstrap.Initialize(lifetime));
        using var duplicateHost = Program.CreateHost([]);
        DesktopBootstrap.Attach(host);
        try
        {
            Assert.Throws<InvalidOperationException>(() => DesktopBootstrap.Attach(duplicateHost));
            DesktopBootstrap.Initialize(lifetime);
            var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
            Assert.True(runtime.IsReady);
            Assert.IsType<MainWindow>(lifetime.MainWindow);
            Assert.Equal("main", Assert.Single(runtime.Windows).Id);
        }
        finally
        {
            DesktopBootstrap.Detach(host);
            PumpUntil(host.StopAsync());
        }

        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
    }

    private static void PumpUntil(Task task)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
        }

        task.WaitAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }
}
