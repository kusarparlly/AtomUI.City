using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Fixtures;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.HeadlessApp;

internal static class Program
{
    public static Task<int> Main()
    {
        return ProcessEntryPoint.RunAsync(RunAsync);
    }

    private static async Task<int> RunAsync()
    {
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();

        var scenario = RunScenarioAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!scenario.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
        }

        await scenario.WaitAsync(TimeSpan.FromSeconds(2));
        Console.WriteLine("Presentation headless industrial scenario passed.");
        return 0;
    }

    private static async Task RunScenarioAsync()
    {
    Console.WriteLine("stage:setup");
    var diagnostics = new InMemoryHostDiagnostics();
    var services = new ServiceCollection();
    services.AddSingleton<IHostDiagnostics>(diagnostics);
    services.AddPresentation(new PresentationQueueOptions
    {
        OutletPendingCapacity = 32,
        ModalInteractionPendingCapacity = 8,
    });
    await using var provider = services.BuildServiceProvider();
    await using var hostScope = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
    var runtime = provider.GetRequiredService<IPresentationRuntime>();
    var dispatcher = provider.GetRequiredService<AtomUI.City.Core.Threading.IUiDispatcher>();
    runtime.Attach(new ClassicDesktopStyleApplicationLifetime(), hostScope);

    var confirmFixture = CreateWindowFixture(runtime, "confirm", outletCount: 2);
    var detachFixture = CreateWindowFixture(runtime, "detach-race", outletCount: 1);
    var stressFixture = CreateWindowFixture(runtime, "stress", outletCount: 1);
    var multiWindowFixtures = Enumerable.Range(0, 8)
        .Select(index => CreateWindowFixture(runtime, $"multi-{index}", outletCount: 4))
        .ToArray();
    Console.WriteLine("stage:windows-created");

    await VerifyRealVisualReceiptsAndSingleConfirmationAsync(
        confirmFixture,
        provider.GetRequiredService<VisualLifecycleHub>(),
        diagnostics);
    Console.WriteLine("stage:confirmation-complete");
    await VerifyDetachedOutletCleanupIsJoinedByWindowCloseAsync(detachFixture, dispatcher);
    Console.WriteLine("stage:detach-race-complete");
    await RunTenThousandCommitStressAsync(stressFixture, dispatcher);
    Console.WriteLine("stage:stress-complete");
    await PopulateMultiWindowFixturesAsync(multiWindowFixtures, dispatcher);
    Console.WriteLine("stage:multi-populated");
    await VerifyConcurrentWindowCloseAsync(multiWindowFixtures);
    Console.WriteLine("stage:multi-closed");

    Ensure(runtime.Windows.Count == 1, "Only the stress Window should remain before Runtime.StopAsync.");
    await runtime.StopAsync();
    Console.WriteLine("stage:runtime-stopped");
    Ensure(runtime.State == PresentationRuntimeState.Stopped, "Runtime did not reach Stopped.");
    Ensure(runtime.Windows.Count == 0, "Stopped Runtime retained WindowSession instances.");
    Ensure(stressFixture.Session.State == WindowSessionState.Closed, "Runtime stop did not close the stress Window.");
}

    private static async Task VerifyDetachedOutletCleanupIsJoinedByWindowCloseAsync(
    WindowFixture fixture,
    AtomUI.City.Core.Threading.IUiDispatcher dispatcher)
{
    var viewModel = new BlockingAsyncDisposable();
    var view = await dispatcher.InvokeAsync(() => new TextBlock { Text = "detach-race" });
    Ensure((await fixture.Outlets[0].CommitAsync(RouteOutletCommitPlan.Replace(
        "primary",
        BoundViewHandle.FromExisting(view, viewModel),
        ViewModelLease.EntryOwned(viewModel)))).Succeeded,
        "Detach-race View failed to commit.");

    await dispatcher.InvokeAsync(() =>
    {
        var panel = (StackPanel)fixture.Window.Content!;
        panel.Children.Remove(fixture.Controls[0]);
    });
    await viewModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var closeTask = fixture.Session.CloseAsync(WindowCloseOrigin.Application).AsTask();
    await Task.Delay(20);
    Ensure(!closeTask.IsCompleted,
        "Window close did not join an asynchronously disposing detached Outlet.");

    viewModel.Release();
    Ensure(await closeTask, "Detach-race Window failed to close.");
    Ensure(viewModel.DisposeCount == 1,
        "Detached Outlet ViewModel was not disposed exactly once.");
}

    private static WindowFixture CreateWindowFixture(
    IPresentationRuntime runtime,
    string windowId,
    int outletCount)
{
    var panel = new StackPanel();
    var controls = new List<ContentControl>(outletCount);
    for (var index = 0; index < outletCount; index++)
    {
        var control = new ContentControl();
        RouteOutletProperties.SetName(control, index == 0 ? "primary" : $"outlet-{index}");
        panel.Children.Add(control);
        controls.Add(control);
    }

    var window = new Window { Content = panel };
    var session = runtime.RegisterWindow(window, windowId);
    window.Show();
    var outlets = controls
        .Select((_, index) => session.GetOutlet(index == 0 ? "primary" : $"outlet-{index}"))
        .ToArray();
    return new WindowFixture(window, session, outlets, controls);
}

    private static async Task VerifyRealVisualReceiptsAndSingleConfirmationAsync(
    WindowFixture fixture,
    VisualLifecycleHub lifecycleHub,
    InMemoryHostDiagnostics diagnostics)
{
    var receipts = new List<VisualLifecycleEvent>();
    using var subscription = lifecycleHub.Subscribe(
        receipts.Add,
        new VisualLifecycleSubscriptionOptions
        {
            WindowId = fixture.Session.Id,
            OutletName = "primary",
        });
    var primaryViewModel = new ConfirmingViewModel();
    var secondaryViewModel = new ConfirmingViewModel();
    var primaryView = new TextBlock { Text = "Primary" };
    var secondaryView = new TextBlock { Text = "Secondary" };

    Ensure((await fixture.Outlets[0].CommitAsync(RouteOutletCommitPlan.Replace(
        "primary",
        BoundViewHandle.FromExisting(primaryView, primaryViewModel)))).Succeeded,
        "Primary confirmation View failed to commit.");
    Ensure((await fixture.Outlets[1].CommitAsync(RouteOutletCommitPlan.Replace(
        "outlet-1",
        BoundViewHandle.FromExisting(secondaryView, secondaryViewModel)))).Succeeded,
        "Secondary confirmation View failed to commit.");
    Ensure(receipts.Any(item =>
            item.Kind == VisualLifecycleEventKind.Attached &&
            ReferenceEquals(item.View, primaryView)),
        "A real AttachedToVisualTree receipt was not observed.");

    var rejected = await fixture.Session.CloseAsync(WindowCloseOrigin.User);
    Ensure(!rejected, "A Window with multiple confirmation owners was allowed to close.");
    Ensure(primaryViewModel.ConfirmationCount == 0 && secondaryViewModel.ConfirmationCount == 0,
        "WindowSession invoked a confirmation despite the multiple-owner invariant.");
    Ensure(fixture.Session.State == WindowSessionState.Ready,
        "Rejected close did not return WindowSession to Ready.");
    Ensure(diagnostics.Records.Any(record =>
            record.Code == PresentationDiagnosticIds.MultipleCloseConfirmations &&
            record.Context["windowId"] == fixture.Session.Id),
        "Multiple confirmation diagnostic was not recorded.");

    Ensure((await fixture.Outlets[1].CommitAsync(RouteOutletCommitPlan.Clear("outlet-1"))).Succeeded,
        "Secondary Outlet failed to clear after rejected close.");
    Ensure(secondaryViewModel.ConfirmationCount == 1,
        "Clearing the secondary Outlet did not run its normal leave confirmation exactly once.");

    var closeWaiters = Enumerable.Range(0, 16)
        .Select(_ => fixture.Session.CloseAsync(WindowCloseOrigin.User).AsTask())
        .ToArray();
    var closeResults = await Task.WhenAll(closeWaiters);
    Ensure(closeResults.All(static item => item), "Concurrent user close waiters did not share success.");
    Ensure(primaryViewModel.ConfirmationCount == 1,
        "Concurrent close invoked the visible confirmation more than once.");
    Ensure(fixture.Session.ActiveCloseOrigin == WindowCloseOrigin.User,
        "WindowSession did not retain the User close origin.");
    Ensure(receipts.Any(item =>
            item.Kind == VisualLifecycleEventKind.Detached &&
            ReferenceEquals(item.View, primaryView)),
        "A real DetachedFromVisualTree receipt was not observed.");
}

    private static async Task RunTenThousandCommitStressAsync(
    WindowFixture fixture,
    AtomUI.City.Core.Threading.IUiDispatcher dispatcher)
{
    var outlet = new RouteOutlet(
        "stress-probe",
        dispatcher,
        new AvaloniaRouteOutletTarget(fixture.Controls[0]),
        diagnostics: null,
        failurePresenter: null,
        queueOptions: new PresentationQueueOptions { OutletPendingCapacity = 32 });
    for (var index = 0; index < 128; index++)
    {
        var warmView = await dispatcher.InvokeAsync(() => new TextBlock { Text = $"warm-{index}" });
        Ensure((await outlet.CommitAsync(RouteOutletCommitPlan.Replace(
            "stress-probe",
            BoundViewHandle.FromExisting(warmView, new object())))).Succeeded,
            "Warm-up commit failed.");
    }

    Ensure((await outlet.CommitAsync(RouteOutletCommitPlan.Clear("stress-probe"))).Succeeded,
        "Warm-up cleanup failed.");
    ForceFullGc();
    var retainedBaseline = GC.GetTotalMemory(forceFullCollection: true);
    var disposeCount = 0;
    var stopwatch = Stopwatch.StartNew();

    for (var index = 0; index < 10_000; index++)
    {
        if (index % 1000 == 0)
        {
            Console.WriteLine($"stage:stress-{index}");
        }

        var view = await dispatcher.InvokeAsync(() => new TextBlock { Text = index.ToString() });
        var handle = BoundViewHandle.FromExisting(
            view,
            new object(),
            () => Interlocked.Increment(ref disposeCount));
        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Replace("stress-probe", handle));
        Ensure(result.Succeeded, $"Stress commit {index} failed: {result.Error}.");
    }

    var weakProbeOutlet = new RouteOutlet("weak-probe", dispatcher);
    var weakReference = await CommitWeaklyHeldViewAsync(weakProbeOutlet, dispatcher);
    Ensure((await weakProbeOutlet.CommitAsync(RouteOutletCommitPlan.Clear("weak-probe"))).Succeeded,
        "Weak-reference probe cleanup failed.");
    await weakProbeOutlet.DisposeAsync();
    Ensure((await outlet.CommitAsync(RouteOutletCommitPlan.Clear("stress-probe"))).Succeeded,
        "Stress cleanup failed.");
    await outlet.DisposeAsync();
    stopwatch.Stop();

    Ensure(disposeCount == 10_000,
        $"Expected 10,000 exactly-once handle disposals, observed {disposeCount}.");
    Ensure(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
        $"10,000 commit stress exceeded 30 seconds: {stopwatch.Elapsed}.");
    Ensure(outlet.QueueSnapshot.PendingCount == 0 && outlet.QueueSnapshot.InFlightCount == 0,
        "Stress Outlet retained pending or in-flight work.");

    ForceFullGc();
    Ensure(!weakReference.IsAlive, "A replaced View remained strongly reachable after three full GC cycles.");
    var retainedBytes = GC.GetTotalMemory(forceFullCollection: true) - retainedBaseline;
    Ensure(retainedBytes <= 8L * 1024 * 1024,
        $"Stress retained {retainedBytes} bytes, exceeding the 8 MiB gate.");
}

[MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CommitWeaklyHeldViewAsync(
    RouteOutlet outlet,
    AtomUI.City.Core.Threading.IUiDispatcher dispatcher)
{
    var view = await dispatcher.InvokeAsync(() => new TextBlock { Text = "weak" });
    var weakReference = new WeakReference(view);
    Ensure((await outlet.CommitAsync(RouteOutletCommitPlan.Replace(
        "weak-probe",
        BoundViewHandle.FromExisting(view, new object())))).Succeeded,
        "Weak-reference probe commit failed.");
    return weakReference;
}

    private static async Task PopulateMultiWindowFixturesAsync(
    IReadOnlyList<WindowFixture> fixtures,
    AtomUI.City.Core.Threading.IUiDispatcher dispatcher)
{
    foreach (var fixture in fixtures)
    {
        foreach (var outlet in fixture.Outlets)
        {
            var view = await dispatcher.InvokeAsync(() => new TextBlock { Text = $"{fixture.Session.Id}:{outlet.Name}" });
            Ensure((await outlet.CommitAsync(RouteOutletCommitPlan.Replace(
                outlet.Name,
                BoundViewHandle.FromExisting(view, new object())))).Succeeded,
                $"Multi-window commit failed for {fixture.Session.Id}/{outlet.Name}.");
        }
    }
}

    private static async Task VerifyConcurrentWindowCloseAsync(IReadOnlyList<WindowFixture> fixtures)
{
    var closeTasks = fixtures
        .SelectMany((fixture, index) => Enumerable.Range(0, 4).Select(_ =>
            fixture.Session.CloseAsync(
                index == 0 ? WindowCloseOrigin.OperatingSystem : WindowCloseOrigin.Application).AsTask()))
        .ToArray();
    var results = await Task.WhenAll(closeTasks);

    Ensure(results.All(static item => item), "A non-rejectable/application Window close failed.");
    foreach (var fixture in fixtures)
    {
        Ensure(fixture.Session.State == WindowSessionState.Closed,
            $"Window '{fixture.Session.Id}' did not reach Closed.");
        Ensure(fixture.Session.Scope.State == LifecycleScopeState.Disposed,
            $"Window '{fixture.Session.Id}' scope was not disposed.");
    }

    Ensure(fixtures[0].Session.ActiveCloseOrigin == WindowCloseOrigin.OperatingSystem,
        "OperatingSystem close origin was not retained.");
    Ensure(fixtures.Skip(1).All(fixture =>
            fixture.Session.ActiveCloseOrigin == WindowCloseOrigin.Application),
        "Application close origin was not retained.");
}

[MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceFullGc()
{
    for (var index = 0; index < 3; index++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

    private static void Ensure(bool condition, string message)
    {
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
    }
}

internal sealed record WindowFixture(
    Window Window,
    WindowSession Session,
    IReadOnlyList<IRouteOutlet> Outlets,
    IReadOnlyList<ContentControl> Controls);

internal sealed class ConfirmingViewModel : IConfirmDeactivate
{
    private int _confirmationCount;

    public int ConfirmationCount => Volatile.Read(ref _confirmationCount);

    public ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _confirmationCount);
        return ValueTask.FromResult(DeactivationResult.Allow());
    }
}

internal sealed class BlockingAsyncDisposable : IAsyncDisposable
{
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeCount;

    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        Started.TrySetResult();
        await _release.Task;
    }

    public void Release() => _release.TrySetResult();
}
