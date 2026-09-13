using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Fixtures;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;
using AtomUI.City.Fixtures.StressCli.DataIntegration;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Func<Task<int>> operation = DogfoodExternalDataServerProcess.IsChildMode(args)
            ? () => DogfoodExternalDataServerProcess.RunChildAsync(args)
            : () => Task.FromResult(Run(args));
        return ProcessEntryPoint.RunAsync(operation)
            .GetAwaiter()
            .GetResult();
    }

    private static int Run(string[] args)
    {
        Environment.SetEnvironmentVariable("AVALONIA_TELEMETRY_OPTOUT", "1");

        var options = DogfoodRunOptions.Parse(args);
        var dataServer = StressDataServer.StartAsync().GetAwaiter().GetResult();
        DogfoodExternalDataServerProcess externalDataServer;
        try
        {
            externalDataServer = DogfoodExternalDataServerProcess.StartAsync().GetAwaiter().GetResult();
        }
        catch
        {
            dataServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }

        using var securityWorkspace = new DogfoodSecurityWorkspace();

        try
        {
            var builder = DogfoodHost.CreateBuilder(
                args,
                dataServer,
                externalDataServer,
                securityWorkspace,
                options);
            using var host = builder.Build();
            var ledger = host.Services.GetRequiredService<DogfoodRunLedger>();
            var resourceMonitor = host.Services.GetRequiredService<DogfoodResourceMonitor>();
            var exitCode = 1;
            var resourcesReleased = false;
            host.StartAsync().GetAwaiter().GetResult();

            try
            {
                ModuleRuntimeLedger.VerifyStarted();
                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT modules-started");
                DogfoodServiceCatalog.Verify(host.Services);
                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT services-verified");
                host.Services.GetRequiredService<DogfoodStateWorkload>()
                    .InitializeAsync()
                    .GetAwaiter()
                    .GetResult();
                DogfoodBootstrap.Attach(host, options);
                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT avalonia-starting");

                exitCode = options.UsesHeadlessUi
                    ? RunHeadless(host, options)
                    : AppBuilder.Configure<DogfoodApplication>()
                        .UsePlatformDetect()
                        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
                Console.WriteLine($"DESKTOP_DOGFOOD_CHECKPOINT avalonia-stopped exit={exitCode}");
                return exitCode;
            }
            catch (Exception exception)
            {
                ledger.RecordFailure(exception);
                Console.Error.WriteLine($"DESKTOP_DOGFOOD_FAILED {exception}");
                throw;
            }
            finally
            {
                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT host-stopping");
                DogfoodBootstrap.Detach(host);
                if (options.UsesHeadlessUi)
                {
                    PumpUntil(host.StopAsync(), TimeSpan.FromMinutes(1));
                }
                else
                {
                    Task.Run(async () => await host.StopAsync().ConfigureAwait(false))
                        .GetAwaiter()
                        .GetResult();
                }
                ModuleRuntimeLedger.VerifyStopped();
                resourcesReleased = true;
                Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT host-stopped");
                Task.Run(async () => await resourceMonitor.CompleteAsync(resourcesReleased).ConfigureAwait(false))
                    .GetAwaiter()
                    .GetResult();
                Task.Run(async () => await ledger.WriteReportAsync(exitCode, resourcesReleased).ConfigureAwait(false))
                    .GetAwaiter()
                    .GetResult();
            }
        }
        finally
        {
            // Avalonia leaves its dispatcher synchronization context installed after the UI loop.
            // Begin async infrastructure shutdown on the thread pool so no continuation targets a stopped loop.
            Task.Run(async () =>
                {
                    await externalDataServer.DisposeAsync().ConfigureAwait(false);
                    await dataServer.DisposeAsync().ConfigureAwait(false);
                })
                .GetAwaiter()
                .GetResult();
        }
    }

    private static int RunHeadless(IApplicationHost host, DogfoodRunOptions options)
    {
        Console.WriteLine("DESKTOP_DOGFOOD_HEADLESS setup-starting");
        AppBuilder.Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            })
            .SetupWithoutStarting();
        Application.Current!.Styles.Add(new FluentTheme());
        Console.WriteLine("DESKTOP_DOGFOOD_HEADLESS setup-complete");

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
        runtime.Attach(lifetime, host.HostScope);
        Console.WriteLine("DESKTOP_DOGFOOD_HEADLESS runtime-attached");

        var mainWindow = host.Services.GetRequiredService<DogfoodWindowFactory>().CreateMainWindow();
        var session = runtime.RegisterWindow(mainWindow, "main");
        lifetime.MainWindow = mainWindow;
        mainWindow.Show();
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine("DESKTOP_DOGFOOD_HEADLESS window-shown");

        var coordinator = host.Services.GetRequiredService<DogfoodDesktopCoordinator>();
        Console.WriteLine("DESKTOP_DOGFOOD_HEADLESS initialization-starting");
        var initializationTimeout = options.MinimumDuration > TimeSpan.Zero
            ? options.MinimumDuration + TimeSpan.FromMinutes(15)
            : TimeSpan.FromMinutes(10);
        PumpUntil(
            coordinator.InitializeAsync(mainWindow, session),
            initializationTimeout);
        PumpUntil(
            host.Services.GetRequiredService<DogfoodHeadlessControlWorkload>()
                .RunAsync(mainWindow, session, session.Scope.CancellationToken),
            TimeSpan.FromMinutes(2));
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_READY profile={options.Profile.ToString().ToLowerInvariant()} windows=4 outlets=12 modules=48 services=110");

        PumpUntil(coordinator.CloseAuxiliaryWindowsAsync(), TimeSpan.FromSeconds(30));
        PumpUntil(coordinator.CloseMainWindowAsync(session), TimeSpan.FromSeconds(30));

        Console.WriteLine("DESKTOP_DOGFOOD_CHECKPOINT window-session-closed");
        return 0;
    }

    private static void PumpUntil(Task task, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!task.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
        }

        task.WaitAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static T PumpUntil<T>(Task<T> task, TimeSpan timeout)
    {
        PumpUntil((Task)task, timeout);
        return task.GetAwaiter().GetResult();
    }
}
