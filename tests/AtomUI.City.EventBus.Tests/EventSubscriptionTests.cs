using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventSubscriptionTests
{
    [Fact]
    public void PublicSubscribeOverloadsRequireLifecycleOwner()
    {
        var subscribeMethods = typeof(IEventSubscriber)
            .GetMethods()
            .Where(method => method.Name == nameof(IEventSubscriber.Subscribe))
            .ToArray();

        Assert.Equal(4, subscribeMethods.Length);
        Assert.All(
            subscribeMethods,
            method => Assert.Equal(typeof(LifecycleScope), method.GetParameters()[0].ParameterType));
    }

    [Fact]
    public void SubscriptionStateValuesAreStable()
    {
        Assert.Equal(0, (int)EventSubscriptionState.Created);
        Assert.Equal(1, (int)EventSubscriptionState.Active);
        Assert.Equal(2, (int)EventSubscriptionState.Quiescing);
        Assert.Equal(3, (int)EventSubscriptionState.Draining);
        Assert.Equal(4, (int)EventSubscriptionState.Disposed);
        Assert.Equal(5, (int)EventSubscriptionState.Faulted);
    }

    [Fact]
    public void DisposeCanBeCalledMoreThanOnce()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Dispose();
        eventBus.Dispose();
    }

    [Fact]
    public void SubscribeRejectsDisposedBus()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask));
    }

    [Fact]
    public void OwnedSubscribeRejectsDisposedBus()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "app");

        eventBus.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => eventBus.Subscribe<TestEvent>(
                owner,
                (_, _) => ValueTask.CompletedTask));
    }

    [Fact]
    public async Task DisposeClearsActiveSubscriptions()
    {
        var eventBus = new InMemoryEventBus();
        var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

        eventBus.Dispose();
        await subscription.DisposeAsync();

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        subscription.Dispose();
    }

    [Fact]
    public async Task DisposedSubscriptionNoLongerReceivesEvents()
    {
        var eventBus = new InMemoryEventBus();
        var receivedCount = 0;
        var subscription = eventBus.Subscribe<TestEvent>(_ =>
        {
            receivedCount++;
            return ValueTask.CompletedTask;
        });

        subscription.Dispose();
        await subscription.DisposeAsync();
        await eventBus.PublishAsync(new TestEvent("ignored"));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.Equal(0, receivedCount);
    }

    [Fact]
    public async Task StopAsyncRemovesSubscriptionFromNewPublicationSnapshots()
    {
        var eventBus = new InMemoryEventBus();
        var receivedCount = 0;
        var subscription = eventBus.Subscribe<TestEvent>(_ =>
        {
            receivedCount++;
            return ValueTask.CompletedTask;
        });

        await subscription.StopAsync();
        var result = await eventBus.PublishAsync(new TestEvent("ignored"));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.Equal(0, receivedCount);
        Assert.Equal(0, result.DeliveredCount);
    }

    [Fact]
    public async Task StopAsyncOnDisposedSubscriptionIgnoresCanceledToken()
    {
        var eventBus = new InMemoryEventBus();
        var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);
        using var cancellation = new CancellationTokenSource();

        await subscription.StopAsync();
        await cancellation.CancelAsync();
        await subscription.StopAsync(cancellation.Token);

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task StopAsyncWaitsForInFlightHandlerToComplete()
    {
        var eventBus = new InMemoryEventBus();
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                handlerStarted.SetResult();
                await releaseHandler.Task;
            });

        var publication = eventBus.PublishAsync(new TestEvent("running")).AsTask();
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stop = subscription.StopAsync().AsTask();
        await Task.Delay(100);

        Assert.False(stop.IsCompleted);

        releaseHandler.SetResult();

        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        await publication.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task DisposeReturnsBeforeInFlightHandlerDrains()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = eventBus.Subscribe<TestEvent>(
            owner,
            async (_, _) =>
            {
                handlerStarted.SetResult();
                await releaseHandler.Task;
            });

        var publication = eventBus.PublishAsync(new TestEvent("running")).AsTask();
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        subscription.Dispose();

        Assert.Contains(
            subscription.State,
            new[] { EventSubscriptionState.Quiescing, EventSubscriptionState.Draining });
        Assert.False(publication.IsCompleted);

        releaseHandler.SetResult();
        await subscription.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task SubscribeAcceptsTypedEventHandlerInstance()
    {
        var eventBus = new InMemoryEventBus();
        var handler = new RecordingEventHandler();

        eventBus.Subscribe<TestEvent>(handler);

        var result = await eventBus.PublishAsync(new TestEvent("handled"));

        Assert.True(result.Succeeded);
        Assert.Equal("handled", handler.Value);
    }

    [Fact]
    public async Task SynchronousHandlerExtensionRequiresAndUsesOwner()
    {
        IEventSubscriber subscriber = new InMemoryEventBus();
        var eventBus = Assert.IsType<InMemoryEventBus>(subscriber);
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var received = string.Empty;

        var subscription = subscriber.Subscribe<TestEvent>(
            owner,
            (context, _) => received = context.Event.Value);
        await eventBus.PublishAsync(new TestEvent("handled"));

        Assert.Equal("handled", received);

        await owner.StopAsync();
        await subscription.StopAsync();
        await eventBus.PublishAsync(new TestEvent("ignored"));

        Assert.Equal("handled", received);
    }

    [Fact]
    public async Task SubscribeAcceptsOwnedTypedEventHandlerInstance()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "app");
        var handler = new RecordingEventHandler();

        var subscription = eventBus.Subscribe(owner, handler);

        await owner.StopAsync();
        await subscription.StopAsync();
        var result = await eventBus.PublishAsync(new TestEvent("ignored"));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.DeliveredCount);
        Assert.Null(handler.Value);
    }

    [Fact]
    public async Task OwnerScopeCancellationDisposesSubscription()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "app");
        var receivedCount = 0;
        var subscription = eventBus.Subscribe<TestEvent>(
            owner,
            (_, _) =>
            {
                receivedCount++;
                return ValueTask.CompletedTask;
            });

        await owner.StopAsync();
        await subscription.StopAsync();
        await eventBus.PublishAsync(new TestEvent("ignored"));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.Equal(0, receivedCount);
    }

    [Fact]
    public async Task StoppingOneOwnerRemovesOnlyThatOwnersSubscription()
    {
        var eventBus = new InMemoryEventBus();
        var windowOwner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var workspaceOwner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "workspace");
        var windowCount = 0;
        var workspaceCount = 0;
        var windowSubscription = eventBus.Subscribe<TestEvent>(
            windowOwner,
            (_, _) =>
            {
                windowCount++;
                return ValueTask.CompletedTask;
            });
        eventBus.Subscribe<TestEvent>(
            workspaceOwner,
            (_, _) =>
            {
                workspaceCount++;
                return ValueTask.CompletedTask;
            });

        await windowOwner.StopAsync();
        await windowSubscription.StopAsync();
        var result = await eventBus.PublishAsync(new TestEvent("workspace-only"));

        Assert.Equal(0, windowCount);
        Assert.Equal(1, workspaceCount);
        Assert.Equal(1, result.DeliveredCount);
    }

    [Fact]
    public async Task OwnerCancellationReachesInFlightHandlerAndDrainCompletes()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = eventBus.Subscribe<TestEvent>(
            owner,
            async (context, cancellationToken) =>
            {
                handlerStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });

        var publication = eventBus.PublishAsync(new TestEvent("cancel")).AsTask();
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await owner.StopAsync();
        await subscription.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var result = await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.Equal(1, result.CanceledCount);
    }

    [Fact]
    public async Task CancelingStopWaitDoesNotCancelTerminationTransaction()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = eventBus.Subscribe<TestEvent>(
            owner,
            async (_, _) =>
            {
                handlerStarted.SetResult();
                await releaseHandler.Task;
            });

        var publication = eventBus.PublishAsync(new TestEvent("running")).AsTask();
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var waitCancellation = new CancellationTokenSource();
        await waitCancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await subscription.StopAsync(waitCancellation.Token));

        Assert.Contains(
            subscription.State,
            new[] { EventSubscriptionState.Quiescing, EventSubscriptionState.Draining });

        releaseHandler.SetResult();
        await subscription.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task ConcurrentStopAndDisposeCallsShareSuccessfulTermination()
    {
        var eventBus = new InMemoryEventBus();
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var subscription = eventBus.Subscribe<TestEvent>(owner, (_, _) => ValueTask.CompletedTask);

        var stopTasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(async () => await subscription.StopAsync()))
            .ToArray();
        var disposeTasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(async () => await subscription.DisposeAsync()))
            .ToArray();

        subscription.Dispose();
        await Task.WhenAll(stopTasks.Concat(disposeTasks)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        var result = await eventBus.PublishAsync(new TestEvent("ignored"));
        Assert.Equal(0, result.DeliveredCount);
    }

    [Fact]
    public async Task TerminationFailureFaultsSubscriptionAndIsDiagnosed()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Window, "window");
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = eventBus.Subscribe<TestEvent>(
            owner,
            async (context, cancellationToken) =>
            {
                using var registration = cancellationToken.Register(
                    static () => throw new InvalidOperationException("cancellation cleanup failed"));
                handlerStarted.SetResult();

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            });

        var publication = eventBus.PublishAsync(new TestEvent("fault")).AsTask();
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await subscription.StopAsync());
        await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("cancellation cleanup failed", exception.Message);
        Assert.Equal(EventSubscriptionState.Faulted, subscription.State);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventSubscriptionTerminationFailed);
        Assert.Equal(subscription.Id.ToString(), record.Context["subscriptionId"]);
    }

    [Fact]
    public async Task OwnerStopRacingSubscriptionCommitLeavesNoActiveSubscription()
    {
        var eventBus = new InMemoryEventBus();

        for (var iteration = 0; iteration < 250; iteration++)
        {
            var owner = LifecycleScope.CreateRoot(
                LifecycleScopeKind.Window,
                $"window-{iteration}");
            using var start = new ManualResetEventSlim();
            IEventSubscription? subscription = null;

            var subscribe = Task.Run(() =>
            {
                start.Wait();

                try
                {
                    subscription = eventBus.Subscribe<TestEvent>(
                        owner,
                        (_, _) => ValueTask.CompletedTask);
                }
                catch (InvalidOperationException)
                {
                    // The owner won the race before the subscription commit.
                }
            });
            var stop = Task.Run(async () =>
            {
                start.Wait();
                await owner.StopAsync();
            });

            start.Set();
            await Task.WhenAll(subscribe, stop).WaitAsync(TimeSpan.FromSeconds(5));

            if (subscription is not null)
            {
                await subscription.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotEqual(EventSubscriptionState.Active, subscription.State);
            }
        }

        var result = await eventBus.PublishAsync(new TestEvent("orphan-check"));
        Assert.Equal(0, result.DeliveredCount);
    }

    [Fact]
    public async Task OwnedSubscribeRejectsStoppedOwner()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "app");
        await owner.StopAsync();

        Assert.Throws<InvalidOperationException>(
            () => eventBus.Subscribe<TestEvent>(
                owner,
                (_, _) => ValueTask.CompletedTask));

        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventSubscriptionAdded);
    }

    private sealed record TestEvent(string Value);

    private sealed class RecordingEventHandler : IEventHandler<TestEvent>
    {
        public string? Value { get; private set; }

        public ValueTask HandleAsync(
            EventContext<TestEvent> context,
            CancellationToken cancellationToken)
        {
            Value = context.Event.Value;

            return ValueTask.CompletedTask;
        }
    }
}
