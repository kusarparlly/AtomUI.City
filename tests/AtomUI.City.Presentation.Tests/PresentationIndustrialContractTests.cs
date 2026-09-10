using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationIndustrialContractTests
{
    [Fact]
    public async Task AggregateRegistrationProvidesThePresentationRuntimeSurface()
    {
        var services = new ServiceCollection();

        services.AddPresentation();

        await using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IPresentationRuntime>());
        Assert.NotNull(provider.GetRequiredService<IUiDispatcher>());
        Assert.NotNull(provider.GetRequiredService<IViewModelFactory>());
        Assert.NotNull(provider.GetRequiredService<IViewRegistry>());
        Assert.NotNull(provider.GetRequiredService<IViewLocator>());
        Assert.NotNull(provider.GetRequiredService<ViewFactory>());
        Assert.NotNull(provider.GetRequiredService<ViewBinder>());
        Assert.NotNull(provider.GetRequiredService<IInteractionHandlerRegistry>());
        Assert.NotNull(provider.GetRequiredService<CommandBinding>());
        Assert.NotNull(provider.GetRequiredService<ValidationVisualStateBinding>());
        Assert.NotNull(provider.GetRequiredService<IPresentationResourceRegistry>());
        Assert.NotNull(provider.GetRequiredService<IActivePluginViewRegistry>());
        Assert.NotNull(provider.GetRequiredService<IPresentationPluginUnloadCoordinator>());
    }

    [Fact]
    public void AggregateRegistrationRejectsNonPositiveQueueCapacities()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddPresentation(
            new PresentationQueueOptions { OutletPendingCapacity = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddPresentation(
            new PresentationQueueOptions { ModalInteractionPendingCapacity = 0 }));
    }

    [Fact]
    public void ViewModelLeaseIsAsyncDisposeOnly()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ViewModelLease)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ViewModelLease)));
    }

    [Fact]
    public async Task ConcurrentOutletDisposeWaitersObserveTheSameAsyncEntryCleanup()
    {
        var viewModel = new BlockingAsyncDisposable();
        var outlet = new RouteOutlet("primary", new InlineDispatcher());
        var handle = BoundViewHandle.FromExisting(new object(), viewModel);
        Assert.True((await outlet.CommitAsync(RouteOutletCommitPlan.Replace(
            "primary",
            handle,
            ViewModelLease.EntryOwned(viewModel)))).Succeeded);

        var first = outlet.DisposeAsync().AsTask();
        await viewModel.Started.Task;
        var second = outlet.DisposeAsync().AsTask();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        viewModel.Release();
        await Task.WhenAll(first, second);

        Assert.Equal(1, viewModel.DisposeCount);
        Assert.True(handle.IsDisposed);
        Assert.Equal(RouteOutletState.Stopped, outlet.State);
    }

    [Fact]
    public async Task RouteOutletRejectsPlanReuseAndRecordsOwnershipViolation()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var outlet = new RouteOutlet("primary", new InlineDispatcher(), diagnostics);
        var handle = BoundViewHandle.FromExisting(new object(), new object());
        var plan = RouteOutletCommitPlan.Replace("primary", handle);

        Assert.True((await outlet.CommitAsync(plan)).Succeeded);
        var exception = await Assert.ThrowsAsync<PresentationException>(
            () => outlet.CommitAsync(plan).AsTask());

        Assert.Equal(PresentationError.CandidateOwnershipViolation, exception.Error);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.CandidateOwnershipViolation &&
                record.Context["outletName"] == "primary" &&
                record.Context["candidateState"] == "EntryOwned");

        await outlet.DisposeAsync();
    }

    [Fact]
    public async Task RouteOutletBoundsPendingWorkRejectsNewestAndPreservesFifo()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new InlineDispatcher();
        var order = new List<int>();
        var firstActivation = new BlockingActivatable(1, order);
        var secondActivation = new RecordingActivatable(2, order);
        var rejectedDisposeCount = 0;
        var outlet = new RouteOutlet(
            "primary",
            dispatcher,
            target: null,
            diagnostics,
            queueOptions: new PresentationQueueOptions { OutletPendingCapacity = 1 });

        var first = outlet.CommitAsync(RouteOutletCommitPlan.Replace(
            "primary",
            BoundViewHandle.FromExisting(new object(), firstActivation))).AsTask();
        await firstActivation.Started.Task;
        var second = outlet.CommitAsync(RouteOutletCommitPlan.Replace(
            "primary",
            BoundViewHandle.FromExisting(new object(), secondActivation))).AsTask();
        var rejectedHandle = BoundViewHandle.FromExisting(
            new object(),
            new object(),
            () => Interlocked.Increment(ref rejectedDisposeCount));

        var rejected = await outlet.CommitAsync(
            RouteOutletCommitPlan.Replace("primary", rejectedHandle));

        Assert.False(rejected.Succeeded);
        Assert.Equal(PresentationError.OutletQueueFull, rejected.Error);
        Assert.Equal(1, Volatile.Read(ref rejectedDisposeCount));
        Assert.Equal(1, outlet.QueueSnapshot.Capacity);
        Assert.Equal(1, outlet.QueueSnapshot.PendingCount);
        Assert.Equal(1, outlet.QueueSnapshot.InFlightCount);
        Assert.Equal(1, outlet.QueueSnapshot.RejectedCount);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.OutletQueueRejected &&
                record.Context["capacity"] == "1" &&
                record.Context["rejectedCount"] == "1");

        firstActivation.Release();
        Assert.True((await first).Succeeded);
        Assert.True((await second).Succeeded);
        Assert.Equal([1, 2], order);

        await outlet.DisposeAsync();
        Assert.Equal(0, outlet.QueueSnapshot.PendingCount);
        Assert.Equal(0, outlet.QueueSnapshot.InFlightCount);
    }

    [Fact]
    public async Task ModalInteractionQueueRejectsNewestAndPreservesPerWindowFifo()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new InteractionHandlerRegistry(
            new InlineDispatcher(),
            diagnostics,
            new PresentationQueueOptions { ModalInteractionPendingCapacity = 1 });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();
        using var registration = registry.Register<int, int>(
            async (context, cancellationToken) =>
            {
                lock (order)
                {
                    order.Add(context.Request);
                }

                if (context.Request == 1)
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(cancellationToken);
                }

                return context.Request;
            });
        var context = new InteractionDispatchContext(WindowId: "main", IsModal: true);

        var first = registry.HandleAsync<int, int>(1, context).AsTask();
        await started.Task;
        var second = registry.HandleAsync<int, int>(2, context).AsTask();
        var rejected = await registry.HandleAsync<int, int>(3, context);

        Assert.Equal(InteractionResultStatus.Failed, rejected.Status);
        var error = Assert.IsType<PresentationException>(rejected.Exception);
        Assert.Equal(PresentationError.InteractionQueueFull, error.Error);
        var snapshot = registry.GetModalQueueSnapshot("main");
        Assert.Equal(1, snapshot.PendingCount);
        Assert.Equal(1, snapshot.InFlightCount);
        Assert.Equal(1, snapshot.RejectedCount);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.InteractionQueueRejected &&
                record.Context["windowId"] == "main" &&
                record.Context["capacity"] == "1");

        release.TrySetResult();
        Assert.Equal(1, (await first).Value);
        Assert.Equal(2, (await second).Value);
        Assert.Equal([1, 2], order);
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return callback(cancellationToken);
        }
    }

    private sealed class BlockingActivatable(int value, List<int> order) : IActivatable
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask ActivateAsync(
            IActivationScope scope,
            CancellationToken cancellationToken)
        {
            order.Add(value);
            Started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }

        public ValueTask ActivateAsync(IActivationScope scope) =>
            ActivateAsync(scope, CancellationToken.None);

        public ValueTask DeactivateAsync() => ValueTask.CompletedTask;

        public ValueTask DeactivateAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Release() => _release.TrySetResult();
    }

    private sealed class RecordingActivatable(int value, List<int> order) : IActivatable
    {
        public ValueTask ActivateAsync(IActivationScope scope) =>
            ActivateAsync(scope, CancellationToken.None);

        public ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken)
        {
            order.Add(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync() => ValueTask.CompletedTask;

        public ValueTask DeactivateAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class BlockingAsyncDisposable : IAsyncDisposable
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
}
