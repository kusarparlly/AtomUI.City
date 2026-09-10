using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;
using AtomUI.City.Fixtures;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    [STAThread]
    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => Task.FromResult(
            AppBuilder.Configure<PresentationDesktopSelfTestApplication>()
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args)));
    }
}

internal sealed class PresentationDesktopSelfTestApplication : Application
{
    private const string WindowId = "desktop-main";

    public override void Initialize()
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            throw new InvalidOperationException("The Presentation desktop self-test requires a classic desktop lifetime.");
        }

        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var context = Prepare(lifetime);
        base.OnFrameworkInitializationCompleted();
        _ = RunAndShutdownAsync(context, lifetime);
    }

    private static DesktopScenarioContext Prepare(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddScoped<DesktopViewModel>();
        services.AddPresentation(new PresentationQueueOptions
        {
            OutletPendingCapacity = 8,
            ModalInteractionPendingCapacity = 4,
        });

        var provider = services.BuildServiceProvider();
        var hostScope = LifecycleScope.CreateRoot(
            LifecycleScopeKind.Application,
            "desktop-application",
            diagnostics);
        var runtime = provider.GetRequiredService<IPresentationRuntime>();
        runtime.Attach(lifetime, hostScope);

        var primaryOutlet = new ContentControl();
        var detailsOutlet = new ContentControl();
        RouteOutletProperties.SetName(primaryOutlet, "primary");
        RouteOutletProperties.SetName(detailsOutlet, "details");

        var status = new TextBlock { Text = "Starting" };
        var window = new Window
        {
            Title = "AtomUI.City Presentation Desktop Self-Test",
            Width = 720,
            Height = 420,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new TextBlock { Text = "Presentation native desktop verification" },
                    primaryOutlet,
                    detailsOutlet,
                    status,
                },
            },
        };

        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Opened += (_, _) => opened.TrySetResult();
        var session = runtime.RegisterWindow(window, WindowId);
        lifetime.MainWindow = window;

        return new DesktopScenarioContext(
            provider,
            hostScope,
            runtime,
            session,
            window,
            status,
            opened.Task,
            diagnostics);
    }

    private static async Task RunAndShutdownAsync(
        DesktopScenarioContext context,
        IClassicDesktopStyleApplicationLifetime lifetime)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await context.Opened.WaitAsync(TimeSpan.FromSeconds(10), watchdog.Token);
            await RunScenarioAsync(context, watchdog.Token);
            Console.WriteLine("Presentation Windows desktop self-test passed.");
            lifetime.Shutdown(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            lifetime.Shutdown(1);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    private static async Task RunScenarioAsync(
        DesktopScenarioContext context,
        CancellationToken cancellationToken)
    {
        var services = context.Provider;
        var dispatcher = services.GetRequiredService<IUiDispatcher>();
        var lifecycle = services.GetRequiredService<VisualLifecycleHub>();
        var registry = services.GetRequiredService<IViewRegistry>();
        var locator = services.GetRequiredService<IViewLocator>();
        var viewFactory = services.GetRequiredService<ViewFactory>();
        var viewModelFactory = services.GetRequiredService<IViewModelFactory>();
        var binder = services.GetRequiredService<ViewBinder>();
        var interactions = services.GetRequiredService<IInteractionHandlerRegistry>();

        var descriptor = new ViewDescriptor(
            typeof(DesktopViewModel),
            typeof(DesktopView),
            viewKey: null,
            _ => new DesktopView());
        registry.Register(descriptor);
        Ensure(ReferenceEquals(locator.Locate(typeof(DesktopViewModel)), descriptor),
            "The registered desktop View could not be located.");

        var receipts = new List<VisualLifecycleEvent>();
        using var lifecycleSubscription = lifecycle.Subscribe(
            receipts.Add,
            new VisualLifecycleSubscriptionOptions
            {
                WindowId = WindowId,
                OutletName = "primary",
            });

        var viewModelLease = await Task.Run(
            async () => await viewModelFactory.AcquireAsync(
                new ViewModelAcquisitionRequest(typeof(DesktopViewModel), services),
                cancellationToken),
            cancellationToken);
        var viewModel = (DesktopViewModel)viewModelLease.Instance;
        var view = await viewFactory.CreateAsync(descriptor, cancellationToken);
        var handle = await dispatcher.InvokeAsync(
            () => binder.Bind(descriptor, view, viewModel),
            cancellationToken);

        var primary = context.Session.GetOutlet("primary");
        var details = context.Session.GetOutlet("details");
        var primaryCommit = await primary.CommitAsync(
            RouteOutletCommitPlan.Replace(
                "primary",
                handle,
                viewModelLease,
                routeId: "desktop/home"),
            cancellationToken);
        Ensure(primaryCommit.Succeeded, $"Primary Outlet commit failed: {primaryCommit.Error}.");
        Ensure(receipts.Any(item =>
                item.Kind == VisualLifecycleEventKind.Attached &&
                ReferenceEquals(item.View, view)),
            "The real Avalonia AttachedToVisualTree receipt was not observed.");

        var detailsView = await dispatcher.InvokeAsync(
            () => new TextBlock { Text = "Named outlet content" },
            cancellationToken);
        var detailsCommit = await details.CommitAsync(
            RouteOutletCommitPlan.Replace(
                "details",
                BoundViewHandle.FromExisting(detailsView, new object()),
                routeId: "desktop/details"),
            cancellationToken);
        Ensure(detailsCommit.Succeeded, $"Named Outlet commit failed: {detailsCommit.Error}.");

        using var interactionRegistration = interactions.Register<string, string>(
            (interaction, token) =>
            {
                token.ThrowIfCancellationRequested();
                Ensure(dispatcher.CheckAccess(), "The desktop interaction did not run on the UI dispatcher.");
                context.Status.Text = interaction.Request;
                return ValueTask.FromResult("acknowledged");
            },
            new InteractionHandlerRegistrationOptions
            {
                Scope = InteractionHandlerScope.Window,
                WindowId = WindowId,
            });
        var interactionResult = await interactions.HandleAsync<string, string>(
            "Interaction completed",
            new InteractionDispatchContext(WindowId: WindowId, IsModal: true),
            cancellationToken);
        Ensure(interactionResult.Status == InteractionResultStatus.Completed &&
               interactionResult.Value == "acknowledged",
            "The window-scoped modal interaction did not complete.");
        Ensure(context.Status.Text == "Interaction completed",
            "The interaction did not update the real Avalonia control.");

        var closed = await context.Session.CloseAsync(
            WindowCloseOrigin.Application,
            cancellationToken);
        Ensure(closed, "The managed desktop Window rejected application close.");
        Ensure(context.Session.State == WindowSessionState.Closed,
            "The desktop WindowSession did not reach Closed.");
        Ensure(context.Session.ActiveCloseOrigin == WindowCloseOrigin.Application,
            "The desktop WindowSession did not retain the Application close origin.");
        Ensure(viewModel.ConfirmationCount == 1,
            "The visible ViewModel was not confirmed exactly once.");
        Ensure(viewModel.DisposeCount == 1,
            "The scoped ViewModel was not disposed exactly once.");
        Ensure(receipts.Any(item =>
                item.Kind == VisualLifecycleEventKind.Detached &&
                ReferenceEquals(item.View, view)),
            "The real Avalonia DetachedFromVisualTree receipt was not observed.");
        Ensure(primary.QueueSnapshot.PendingCount == 0 && primary.QueueSnapshot.InFlightCount == 0,
            "The primary Outlet retained queued work after Window close.");

        await context.Runtime.StopAsync(cancellationToken);
        Ensure(context.Runtime.State == PresentationRuntimeState.Stopped,
            "The desktop Presentation runtime did not reach Stopped.");
        Ensure(context.Runtime.Windows.Count == 0,
            "The desktop Presentation runtime retained a closed WindowSession.");
        Ensure(!context.Diagnostics.Records.Any(record =>
                record.Severity == HostDiagnosticSeverity.Error),
            "The desktop self-test emitted an unexpected error diagnostic.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed class DesktopView : UserControl, IViewDataContextAware
{
    public DesktopView()
    {
        Content = new TextBlock { Text = "Resolved and bound through Presentation" };
    }
}

internal sealed class DesktopViewModel : IConfirmDeactivate, IAsyncDisposable
{
    private int _confirmationCount;
    private int _disposeCount;

    public int ConfirmationCount => Volatile.Read(ref _confirmationCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _confirmationCount);
        return ValueTask.FromResult(DeactivationResult.Allow());
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        return ValueTask.CompletedTask;
    }
}

internal sealed record DesktopScenarioContext(
    ServiceProvider Provider,
    LifecycleScope HostScope,
    IPresentationRuntime Runtime,
    WindowSession Session,
    Window Window,
    TextBlock Status,
    Task Opened,
    InMemoryHostDiagnostics Diagnostics) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (Runtime.State is PresentationRuntimeState.Ready or PresentationRuntimeState.Stopping)
        {
            try
            {
                await Runtime.StopAsync();
            }
            catch
            {
                // The original self-test failure remains the process result.
            }
        }

        await HostScope.DisposeAsync();
        await Provider.DisposeAsync();
    }
}
