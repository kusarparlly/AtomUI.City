using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Presentation;
using AtomUI.City.Routing;

namespace AtomUI.City.Presentation.Tests;

public sealed class RoutePresentationCoordinatorTests
{
    [Fact]
    public async Task SuccessfulNavigationCommitsRouterAndOutlet()
    {
        var router = CreateRouter();
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var handle = BoundViewHandle.FromExisting(new TestView(), new TestViewModel());
        var coordinator = new RoutePresentationCoordinator();

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("home", cancellationToken: token),
            new CallbackPlanFactory((context, _) =>
            {
                Assert.Equal("home", context.Navigation.Route.RouteId);
                Assert.Equal("primary", context.OutletName);
                return ValueTask.FromResult(RouteOutletCommitPlan.Replace("primary", handle));
            }));

        Assert.Equal(RoutePresentationResultStatus.Succeeded, result.Status);
        Assert.True(result.Succeeded);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.Same(handle.View, outlet.CurrentContent);
    }

    [Fact]
    public async Task OutletFailureDisposesCandidateAndRestoresRouter()
    {
        var router = CreateRouter();
        await router.NavigateByPathAsync("home");
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var disposeCount = 0;
        var handle = BoundViewHandle.FromExisting(
            new TestView(),
            new TestViewModel(),
            () => disposeCount++);
        var coordinator = new RoutePresentationCoordinator();

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("settings", cancellationToken: token),
            new CallbackPlanFactory((_, _) => ValueTask.FromResult(
                RouteOutletCommitPlan.Replace("secondary", handle))));

        Assert.Equal(RoutePresentationResultStatus.PresentationFailedRouteRestored, result.Status);
        Assert.Equal(NavigationRestoreResultStatus.Restored, result.Restore?.Status);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.True(handle.IsDisposed);
        Assert.Equal(1, disposeCount);
        Assert.Null(outlet.CurrentContent);
    }

    [Fact]
    public async Task RoutingFailureDoesNotCreatePresentationPlan()
    {
        var router = CreateRouter();
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var factoryCalls = 0;
        var coordinator = new RoutePresentationCoordinator();

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("missing", cancellationToken: token),
            new CallbackPlanFactory((_, _) =>
            {
                factoryCalls++;
                throw new InvalidOperationException("must not run");
            }));

        Assert.Equal(RoutePresentationResultStatus.RoutingNotCommitted, result.Status);
        Assert.Equal(0, factoryCalls);
        Assert.Null(outlet.CurrentContent);
    }

    [Fact]
    public async Task CancellationBeforeAdmissionDoesNotNavigateOrCreatePlan()
    {
        var router = CreateRouter();
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var navigationCalls = 0;
        var factoryCalls = 0;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await new RoutePresentationCoordinator().NavigateAsync(
            router,
            outlet,
            token =>
            {
                navigationCalls++;
                return router.NavigateByPathAsync("home", cancellationToken: token);
            },
            new CallbackPlanFactory((_, _) =>
            {
                factoryCalls++;
                throw new InvalidOperationException("must not run");
            }),
            cancellation.Token);

        Assert.Equal(RoutePresentationResultStatus.Cancelled, result.Status);
        Assert.Equal(0, navigationCalls);
        Assert.Equal(0, factoryCalls);
        Assert.Null(router.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task CancellationAfterRouterCommitStillRestoresRoute()
    {
        var router = CreateRouter();
        await router.NavigateByPathAsync("home");
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        using var cancellation = new CancellationTokenSource();

        var result = await new RoutePresentationCoordinator().NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("settings", cancellationToken: token),
            new CallbackPlanFactory((_, _) =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }),
            cancellation.Token);

        Assert.Equal(RoutePresentationResultStatus.PresentationFailedRouteRestored, result.Status);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.Equal(NavigationRestoreResultStatus.Restored, result.Restore?.Status);
    }

    [Fact]
    public async Task StoppedOutletRejectsCandidateAndRouterRestores()
    {
        var router = CreateRouter();
        await router.NavigateByPathAsync("home");
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        await outlet.DisposeAsync();
        var disposeCount = 0;
        var handle = BoundViewHandle.FromExisting(
            new TestView(),
            new TestViewModel(),
            () => disposeCount++);

        var result = await new RoutePresentationCoordinator().NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("settings", cancellationToken: token),
            new CallbackPlanFactory((_, _) => ValueTask.FromResult(
                RouteOutletCommitPlan.Replace("primary", handle))));

        Assert.Equal(RoutePresentationResultStatus.PresentationFailedRouteRestored, result.Status);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.Equal(1, disposeCount);
    }

    [Fact]
    public async Task PlanFactoryFailureRestoresRouter()
    {
        var router = CreateRouter();
        await router.NavigateByPathAsync("home");
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var coordinator = new RoutePresentationCoordinator();

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("settings", cancellationToken: token),
            new CallbackPlanFactory((_, _) => throw new InvalidOperationException("view creation failed")));

        Assert.Equal(RoutePresentationResultStatus.PresentationFailedRouteRestored, result.Status);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task LaterNavigationRejectsLateRestoreWithoutUndoingNewRoute()
    {
        var router = CreateRouter();
        await router.NavigateByPathAsync("home");
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var coordinator = new RoutePresentationCoordinator();

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("settings", cancellationToken: token),
            new CallbackPlanFactory(async (_, token) =>
            {
                await router.NavigateByPathAsync("profile", cancellationToken: token);
                throw new InvalidOperationException("late view failure");
            }));

        Assert.Equal(RoutePresentationResultStatus.RestoreRejectedByNewerNavigation, result.Status);
        Assert.Equal("CITY-NAVIGATION-RESTORE-SUPERSEDED", result.Restore?.Code);
        Assert.Equal("profile", router.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task SameOutletSerializesRouterThroughPresentationIntent()
    {
        var router = CreateRouter();
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var coordinator = new RoutePresentationCoordinator();
        var firstFactoryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondNavigationStarted = false;

        var first = coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("home", cancellationToken: token),
            new CallbackPlanFactory(async (_, token) =>
            {
                firstFactoryEntered.TrySetResult();
                await releaseFirstFactory.Task.WaitAsync(token);
                return RouteOutletCommitPlan.Replace(
                    "primary",
                    BoundViewHandle.FromExisting(new TestView(), new TestViewModel()));
            })).AsTask();
        await firstFactoryEntered.Task;

        var second = coordinator.NavigateAsync(
            router,
            outlet,
            async token =>
            {
                secondNavigationStarted = true;
                return await router.NavigateByPathAsync("settings", cancellationToken: token);
            },
            new CallbackPlanFactory((_, _) => ValueTask.FromResult(
                RouteOutletCommitPlan.Replace(
                    "primary",
                    BoundViewHandle.FromExisting(new TestView(), new TestViewModel()))))).AsTask();

        Assert.False(secondNavigationStarted);
        releaseFirstFactory.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.True(secondNavigationStarted);
        Assert.Equal("settings", router.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ThrowingDiagnosticsCannotChangeSuccessfulCoordination()
    {
        var router = CreateRouter();
        var outlet = new RouteOutlet("primary", new ImmediateDispatcher());
        var coordinator = new RoutePresentationCoordinator(new ThrowingDiagnostics());

        var result = await coordinator.NavigateAsync(
            router,
            outlet,
            token => router.NavigateByPathAsync("home", cancellationToken: token),
            new CallbackPlanFactory((_, _) => ValueTask.FromResult(
                RouteOutletCommitPlan.Replace(
                    "primary",
                    BoundViewHandle.FromExisting(new TestView(), new TestViewModel())))));

        Assert.Equal(RoutePresentationResultStatus.Succeeded, result.Status);
        Assert.Equal("home", router.CurrentSnapshot.Route.RouteId);
        Assert.NotNull(outlet.CurrentContent);
    }

    private static NavigationScope CreateRouter() =>
        new(RouteGraphSnapshot.Create(
            [
                Route("home"),
                Route("settings"),
                Route("profile"),
            ]));

    private static RouteDescriptor Route(string id) =>
        new(
            id,
            RouteDefinitionKind.Route,
            id,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            parentRouteId: null);

    private sealed class CallbackPlanFactory(
        Func<RoutePresentationPlanContext, CancellationToken, ValueTask<RouteOutletCommitPlan>> callback)
        : IRoutePresentationPlanFactory
    {
        public ValueTask<RouteOutletCommitPlan> CreateAsync(
            RoutePresentationPlanContext context,
            CancellationToken cancellationToken = default) =>
            callback(context, cancellationToken);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default) =>
            callback(cancellationToken);
    }

    private sealed class TestView;

    private sealed class TestViewModel;

    private sealed class ThrowingDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record) =>
            throw new InvalidOperationException("diagnostics failed");

        public void Complete()
        {
        }
    }
}
