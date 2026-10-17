using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusPluginContractTests
{
    private static readonly EventContractId SharedContractId = new("atomui.city.tests.plugin-shared.v1");

    [Fact]
    public async Task LeaseGrantsOnlyExactContractChannelAndDirection()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();
        await using var lease = await controller.CreateAsync(new EventBusContributionRequest(
            "plugin.alpha",
            [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Publish | EventPluginAccess.Subscribe, 2, 4)]));
        var received = 0;
        await using var subscription = lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            (context, _) => { received = context.Event.Value; return ValueTask.CompletedTask; });

        var result = await lease.Publisher.PublishAsync(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            new SharedPluginEvent(42));

        Assert.Equal(EventBusContributionState.Active, lease.State);
        Assert.Equal(42, received);
        Assert.Single(result.Deliveries);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await lease.Publisher.PublishAsync(EventPluginPlane.Shared, new SharedPluginEvent(1)));
        Assert.Throws<UnauthorizedAccessException>(() =>
            lease.Subscriber.Subscribe<OtherPluginEvent>(
                EventPluginPlane.Shared,
                (_, _) => ValueTask.CompletedTask));
        var rejections = host.Services.GetRequiredService<IHostDiagnostics>().Records
            .Where(record => record.Code == EventDiagnosticIds.PluginContributionRejected).ToArray();
        Assert.Equal(2, rejections.Length);
        Assert.All(rejections, rejection => Assert.Equal("plugin.alpha", rejection.Context["pluginId"]));
        Assert.Contains(rejections, rejection =>
            rejection.Context["channel"] == EventChannel<SharedPluginEvent>.DefaultName);
    }

    [Fact]
    public async Task DuplicatePluginIdAndIncompatibleSchemaFailBeforeCommit()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();
        await using var first = await controller.CreateAsync(new EventBusContributionRequest("plugin.same"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.CreateAsync(new EventBusContributionRequest("plugin.same")));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.CreateAsync(new EventBusContributionRequest(
                "plugin.version",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Publish, 4, 5)])));
    }

    [Fact]
    public async Task ManualSharedContractCannotBeGrantedToAPlugin()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureHost(options => options.ApplicationId = "AtomUI.City.EventBus.ManualContractTest");
        builder.ConfigureServices(services =>
        {
            services.AddEventBus();
            services.AddEventContract<OtherPluginEvent>(new EventContractId("manual.shared"));
        });
        await using var host = builder.Build();
        await host.StartAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await host.Services.GetRequiredService<IEventBusContributionController>().CreateAsync(
                new EventBusContributionRequest(
                    "plugin.manual",
                    [new EventPluginAccessRule(new EventContractId("manual.shared"), "default", EventPluginAccess.Publish)])));

        Assert.Contains("validated closed object graph", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            host.Services.GetRequiredService<IHostDiagnostics>().Records,
            record => record.Code == EventDiagnosticIds.PluginContributionRejected &&
                      record.Context["pluginId"] == "plugin.manual");
    }

    [Fact]
    public async Task LeaseStopRevokesAdmissionDrainsSubscriptionsAndAllowsIdReuse()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();
        var request = new EventBusContributionRequest(
            "plugin.restart",
            [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Subscribe)]);
        var lease = await controller.CreateAsync(request);
        _ = lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            (_, _) => ValueTask.CompletedTask);

        await lease.StopAsync();

        Assert.Equal(EventBusContributionState.Disposed, lease.State);
        Assert.Equal(0, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);
        Assert.Throws<ObjectDisposedException>(() =>
            lease.Subscriber.Subscribe<SharedPluginEvent>(
                EventPluginPlane.Shared,
                (_, _) => ValueTask.CompletedTask));
        await using var replacement = await controller.CreateAsync(request);
        Assert.Equal(EventBusContributionState.Active, replacement.State);
    }

    [Fact]
    public async Task HostStopTerminatesActiveContribution()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest("plugin.host-stop"));

        await host.StopAsync();

        Assert.Equal(EventBusContributionState.Disposed, lease.State);
    }

    [Fact]
    public async Task QuiescingRejectsNewWorkAndDrainsTheAcceptedPublication()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest(
                "plugin.drain",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Publish | EventPluginAccess.Subscribe)]));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            async (_, _) => { entered.TrySetResult(); await release.Task.ConfigureAwait(false); });
        var publication = lease.Publisher.PublishAsync(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            new SharedPluginEvent(1)).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = lease.StopAsync().AsTask();

        Assert.True(lease.State is EventBusContributionState.Quiescing or EventBusContributionState.Draining);
        Assert.False(stop.IsCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await lease.Publisher.PublishAsync(
                EventPluginPlane.Shared,
                new EventChannel<SharedPluginEvent>("plugin"),
                new SharedPluginEvent(2)));
        release.TrySetResult();
        await Task.WhenAll(publication, stop);
        Assert.Equal(EventBusContributionState.Disposed, lease.State);
    }

    [Fact]
    public async Task DrainTimeoutFaultsLeaseReportsStableContextAndRetainsPluginIdUntilLateCleanup()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();
        var request = new EventBusContributionRequest(
            "plugin.timeout",
            [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Publish | EventPluginAccess.Subscribe)],
            quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.FromMilliseconds(75) });
        var lease = await controller.CreateAsync(request);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            });
        var publication = lease.Publisher.PublishAsync(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            new SharedPluginEvent(1)).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsAsync<EventPluginDrainTimeoutException>(
            () => lease.StopAsync().AsTask());

        Assert.Equal("plugin.timeout", exception.PluginId);
        Assert.Equal(TimeSpan.FromMilliseconds(75), exception.DrainTimeout);
        Assert.True(exception.ActiveSubscriptions > 0);
        Assert.Equal(0, exception.PendingRegistrations);
        Assert.Equal(EventBusContributionState.Faulted, lease.State);
        var repeated = await Assert.ThrowsAsync<EventPluginDrainTimeoutException>(
            () => lease.DisposeAsync().AsTask());
        Assert.Same(exception, repeated);
        var timeoutDiagnostic = Assert.Single(
            host.Services.GetRequiredService<IHostDiagnostics>().Records,
            record => record.Code == EventDiagnosticIds.EventPluginDrainTimedOut &&
                      record.Context["pluginId"] == "plugin.timeout");
        Assert.Equal("75", timeoutDiagnostic.Context["drainTimeoutMilliseconds"]);
        Assert.Equal(exception.ActiveOperations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            timeoutDiagnostic.Context["activeOperations"]);
        Assert.Equal(exception.ActiveSubscriptions.ToString(System.Globalization.CultureInfo.InvariantCulture),
            timeoutDiagnostic.Context["activeSubscriptions"]);
        Assert.Equal("0", timeoutDiagnostic.Context["pendingRegistrations"]);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.CreateAsync(request));

        release.TrySetResult();
        await publication.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(
            () => host.Services.GetRequiredService<IHostDiagnostics>().Records.Any(record =>
                record.Code == EventDiagnosticIds.PluginContributionDisposed &&
                record.Context["pluginId"] == "plugin.timeout" &&
                record.Message.Contains("late cleanup", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(2));

        await using var replacement = await controller.CreateAsync(request);
        Assert.Equal(EventBusContributionState.Active, replacement.State);
        Assert.Equal(EventBusContributionState.Faulted, lease.State);
    }

    [Fact]
    public async Task DrainTimeoutAlsoBoundsAnActivePluginPublishOperation()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var applicationSubscription = host.Services.GetRequiredService<IEventSubscriber>().Subscribe<SharedPluginEvent>(
            host.ApplicationScope!,
            new EventChannel<SharedPluginEvent>("plugin"),
            async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            });
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>().CreateAsync(
            new EventBusContributionRequest(
                "plugin.operation-timeout",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Publish)],
                quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.FromMilliseconds(75) }));
        var publication = lease.Publisher.PublishAsync(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            new SharedPluginEvent(1)).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsAsync<EventPluginDrainTimeoutException>(
            () => lease.StopAsync().AsTask());

        Assert.Equal(1, exception.ActiveOperations);
        Assert.Equal(0, exception.ActiveSubscriptions);
        Assert.Equal(0, exception.PendingRegistrations);
        Assert.Equal(EventBusContributionState.Faulted, lease.State);
        release.TrySetResult();
        await publication.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(
            () => host.Services.GetRequiredService<IHostDiagnostics>().Records.Any(record =>
                record.Code == EventDiagnosticIds.PluginContributionDisposed &&
                record.Context["pluginId"] == "plugin.operation-timeout" &&
                record.Message.Contains("late cleanup", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CallerCancellationStopsOnlyItsWaitWhileTheLeaseTerminationContinues()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>().CreateAsync(
            new EventBusContributionRequest(
                "plugin.wait-cancel",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Subscribe)],
                quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.FromSeconds(2) }));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            });
        var publication = host.Services.GetRequiredService<IEventPublisher>().PublishAsync(
            new EventChannel<SharedPluginEvent>("plugin"),
            new SharedPluginEvent(1)).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => lease.StopAsync(cancellation.Token).AsTask());

        Assert.True(lease.State is EventBusContributionState.Quiescing or EventBusContributionState.Draining);
        release.TrySetResult();
        await publication.WaitAsync(TimeSpan.FromSeconds(2));
        await lease.DisposeAsync();
        Assert.Equal(EventBusContributionState.Disposed, lease.State);
        Assert.DoesNotContain(
            host.Services.GetRequiredService<IHostDiagnostics>().Records,
            record => record.Code == EventDiagnosticIds.EventPluginDrainTimedOut &&
                      record.Context["pluginId"] == "plugin.wait-cancel");
    }

    [Fact]
    public async Task QuiescingDiagnosticCanReenterStopWithoutPublishingASecondTermination()
    {
        var diagnostics = new ReentrantHostDiagnostics();
        await using var host = CreateHost(diagnostics);
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest("plugin.diagnostic-reentry"));
        Task? reentrantStop = null;
        diagnostics.OnWrite = record =>
        {
            if (record.Code == EventDiagnosticIds.PluginContributionQuiescing)
            {
                reentrantStop = lease.StopAsync().AsTask();
            }
        };

        await lease.StopAsync();
        Assert.NotNull(reentrantStop);
        await reentrantStop;

        Assert.Equal(EventBusContributionState.Disposed, lease.State);
        Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.PluginContributionQuiescing &&
                      record.Context["pluginId"] == "plugin.diagnostic-reentry");
    }

    [Fact]
    public async Task DiagnosticsSinkRunsOutsideTheLeaseLockAcrossThreads()
    {
        var diagnostics = new ReentrantHostDiagnostics();
        await using var host = CreateHost(diagnostics);
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest("plugin.diagnostic-lock-probe"));
        using var reentrantCallReturned = new ManualResetEventSlim();
        Task? reentrantStop = null;
        var observedLockHeld = false;
        diagnostics.OnWrite = record =>
        {
            if (record.Code != EventDiagnosticIds.PluginContributionQuiescing)
            {
                return;
            }

            reentrantStop = Task.Run(async () =>
            {
                var stop = lease.StopAsync();
                reentrantCallReturned.Set();
                await stop;
            });
            observedLockHeld = !reentrantCallReturned.Wait(TimeSpan.FromSeconds(1));
        };

        await lease.StopAsync();
        Assert.NotNull(reentrantStop);
        await reentrantStop;

        Assert.False(observedLockHeld);
        Assert.Equal(EventBusContributionState.Disposed, lease.State);
    }

    [Fact]
    public async Task ActivatedDiagnosticCannotReenterAndClaimTheSamePluginId()
    {
        var diagnostics = new ReentrantHostDiagnostics();
        await using var host = CreateHost(diagnostics);
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();
        var request = new EventBusContributionRequest("plugin.activation-reentry");
        Exception? nestedFailure = null;
        ValueTask<IEventBusContributionLease>? unexpectedNestedLease = null;
        diagnostics.OnWrite = record =>
        {
            if (record.Code != EventDiagnosticIds.PluginContributionActivated ||
                record.Context["pluginId"] != "plugin.activation-reentry")
            {
                return;
            }

            try
            {
                unexpectedNestedLease = controller.CreateAsync(request);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        await using var lease = await controller.CreateAsync(request);

        Assert.IsType<InvalidOperationException>(nestedFailure);
        Assert.Null(unexpectedNestedLease);
        Assert.Equal(EventBusContributionState.Active, lease.State);
        Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.PluginContributionActivated &&
                      record.Context["pluginId"] == "plugin.activation-reentry");
    }

    [Fact]
    public async Task SubscribeReentrancyRollsBackInsteadOfCommittingAfterQuiescing()
    {
        var diagnostics = new ReentrantHostDiagnostics();
        await using var host = CreateHost(diagnostics);
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>().CreateAsync(
            new EventBusContributionRequest(
                "plugin.subscribe-reentry",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Subscribe)]));
        Task? reentrantStop = null;
        diagnostics.OnWrite = record =>
        {
            if (record.Code == EventDiagnosticIds.EventSubscriptionAdded)
            {
                reentrantStop = lease.StopAsync().AsTask();
            }
        };

        Assert.Throws<ObjectDisposedException>(() => lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            (_, _) => ValueTask.CompletedTask));
        Assert.NotNull(reentrantStop);
        await reentrantStop;

        Assert.Equal(EventBusContributionState.Disposed, lease.State);
        Assert.Equal(0, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);
        Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.PluginContributionQuiescing &&
                      record.Context["pluginId"] == "plugin.subscribe-reentry");
    }

    [Fact]
    public async Task ConcurrentSubscribeAndStopNeverLeavesAHalfCommittedSubscription()
    {
        await using var host = CreateHost();
        await host.StartAsync();
        var controller = host.Services.GetRequiredService<IEventBusContributionController>();

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var lease = await controller.CreateAsync(new EventBusContributionRequest(
                $"plugin.subscribe-stop-race.{iteration}",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Subscribe)]));
            using var start = new ManualResetEventSlim();
            var subscribe = Task.Run<IEventSubscription?>(() =>
            {
                start.Wait();
                try
                {
                    return lease.Subscriber.Subscribe<SharedPluginEvent>(
                        EventPluginPlane.Shared,
                        new EventChannel<SharedPluginEvent>("plugin"),
                        (_, _) => ValueTask.CompletedTask);
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            });
            var stop = Task.Run(async () =>
            {
                start.Wait();
                await lease.StopAsync();
            });

            start.Set();
            await Task.WhenAll(subscribe, stop).WaitAsync(TimeSpan.FromSeconds(2));
            var subscription = await subscribe;

            Assert.Equal(EventBusContributionState.Disposed, lease.State);
            if (subscription is not null)
            {
                Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
            }
        }

        Assert.Equal(0, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);
    }

    [Fact]
    public async Task PendingRegistrationIsBoundedByDrainDeadlineAndRollsBackWhenExternalSubscribeReturns()
    {
        var diagnostics = new ReentrantHostDiagnostics();
        await using var host = CreateHost(diagnostics);
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>().CreateAsync(
            new EventBusContributionRequest(
                "plugin.pending-timeout",
                [new EventPluginAccessRule(SharedContractId, "plugin", EventPluginAccess.Subscribe)],
                quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.FromMilliseconds(75) }));
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callbackRelease = new ManualResetEventSlim();
        diagnostics.OnWrite = record =>
        {
            if (record.Code == EventDiagnosticIds.EventSubscriptionAdded)
            {
                callbackEntered.TrySetResult();
                callbackRelease.Wait();
            }
        };
        var subscribe = Task.Run(() => lease.Subscriber.Subscribe<SharedPluginEvent>(
            EventPluginPlane.Shared,
            new EventChannel<SharedPluginEvent>("plugin"),
            (_, _) => ValueTask.CompletedTask));
        await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        EventPluginDrainTimeoutException exception;
        try
        {
            exception = await Assert.ThrowsAsync<EventPluginDrainTimeoutException>(
                () => lease.StopAsync().AsTask());
            Assert.Equal(1, exception.PendingRegistrations);
            Assert.Equal(0, exception.ActiveSubscriptions);
            Assert.Equal(EventBusContributionState.Faulted, lease.State);
        }
        finally
        {
            callbackRelease.Set();
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(() => subscribe);
        await WaitUntilAsync(
            () => diagnostics.Records.Any(record =>
                record.Code == EventDiagnosticIds.PluginContributionDisposed &&
                record.Context["pluginId"] == "plugin.pending-timeout" &&
                record.Message.Contains("late cleanup", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(2));
        Assert.Equal(0, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);
    }

    [Fact]
    public void RequestAndQuotaModelsRejectIllegalBoundaries()
    {
        Assert.Throws<ArgumentException>(() => new EventBusContributionRequest(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPluginAccessRule(
            SharedContractId, "plugin", EventPluginAccess.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventBusContributionRequest(
            "plugin.bad-quota", quotas: new EventPluginQuotas { MaximumSubscriptions = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventBusContributionRequest(
            "plugin.bad-timeout", quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventBusContributionRequest(
            "plugin.excessive-timeout",
            quotas: new EventPluginQuotas { DrainTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) }));
        Assert.Throws<ArgumentException>(() => new EventPluginDrainTimeoutException(
            " ", TimeSpan.FromSeconds(1), 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPluginDrainTimeoutException(
            "plugin.timeout", TimeSpan.Zero, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPluginDrainTimeoutException(
            "plugin.timeout", TimeSpan.FromSeconds(1), -1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPluginDrainTimeoutException(
            "plugin.timeout", TimeSpan.FromSeconds(1), 0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPluginDrainTimeoutException(
            "plugin.timeout", TimeSpan.FromSeconds(1), 0, 0, -1));
        Assert.Throws<ArgumentException>(() => new EventBusContributionRequest(
            "plugin.shared-as-private",
            privateContracts: [EventContractDescriptor.Shared<SharedPluginEvent>(SharedContractId, typeof(SharedPluginEvent).Assembly)]));
    }

    [Fact]
    public async Task PrivatePlaneAcceptsOnlyItsCollectibleContractAndReleasesItWithLease()
    {
        using var loadContext = new PluginTestLoadContext();
        var assembly = loadContext.LoadFromAssemblyPath(typeof(EventBusPluginContractTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(PrivatePluginEvent).FullName!, throwOnError: true)!;
        var descriptorFactory = typeof(EventContractDescriptor).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(EventContractDescriptor.PluginPrivate) && method.IsGenericMethodDefinition);
        var descriptor = (EventContractDescriptor)descriptorFactory.MakeGenericMethod(eventType)
            .Invoke(null, [new EventContractId("plugin.alpha.private.v1")])!;
        await using var host = CreateHost();
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest("plugin.private", privateContracts: [descriptor]));
        var publish = typeof(IPluginEventPublisher).GetMethods()
            .Single(method => method.Name == nameof(IPluginEventPublisher.PublishAsync) &&
                              method.IsGenericMethodDefinition && method.GetParameters().Length == 4);
        var privateEvent = Activator.CreateInstance(eventType, 17)!;
        var valueTask = publish.MakeGenericMethod(eventType).Invoke(
            lease.Publisher,
            [EventPluginPlane.Private, privateEvent, null, CancellationToken.None])!;
        var publishTask = (Task)valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null)!;

        await publishTask;
        await lease.DisposeAsync();

        Assert.Equal(EventBusContributionState.Disposed, lease.State);
        Assert.Throws<TargetInvocationException>(() => publish.MakeGenericMethod(eventType).Invoke(
            lease.Publisher,
            [EventPluginPlane.Private, privateEvent, null, CancellationToken.None]));
    }

    [Fact]
    public async Task DisposedLeaseDoesNotRetainPluginAssemblyLoadContext()
    {
        var result = await CreateAndDisposeCollectibleContributionAsync();

        for (var attempt = 0; result.LoadContext.IsAlive && attempt < 20; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(10);
        }

        Assert.Equal(EventBusContributionState.Disposed, result.Lease.State);
        Assert.False(result.LoadContext.IsAlive);
    }

    private static IApplicationHost CreateHost(IHostDiagnostics? diagnostics = null)
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.EventBus.PluginContractTests";
            options.ApplicationName = "AtomUI.City.EventBus.PluginContractTests";
        });
        builder.ConfigureServices(services =>
        {
            if (diagnostics is not null)
            {
                services.AddSingleton(diagnostics);
                services.AddSingleton<IHostDiagnostics>(diagnostics);
            }

            services.AddEventBus();
            services.AddSingleton(EventContractDescriptor.GeneratedShared<SharedPluginEvent>(
                SharedContractId, typeof(SharedPluginEvent).Assembly, 3, "PLUGIN-SHARED-V3"));
            services.AddSingleton(EventContractDescriptor.Shared<OtherPluginEvent>(
                new EventContractId("atomui.city.tests.plugin-other.v1"), typeof(OtherPluginEvent).Assembly));
        });
        return builder.Build();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected plugin EventBus state was not observed before the test deadline.");
            }

            await Task.Delay(10);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference LoadContext, IEventBusContributionLease Lease)>
        CreateAndDisposeCollectibleContributionAsync()
    {
        var loadContext = new PluginTestLoadContext();
        var weakReference = new WeakReference(loadContext, trackResurrection: false);
        var assembly = loadContext.LoadFromAssemblyPath(typeof(EventBusPluginContractTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(PrivatePluginEvent).FullName!, throwOnError: true)!;
        var factory = typeof(EventContractDescriptor).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(EventContractDescriptor.PluginPrivate) && method.IsGenericMethodDefinition);
        var descriptor = (EventContractDescriptor)factory.MakeGenericMethod(eventType)
            .Invoke(null, [new EventContractId("plugin.collectible.private.v1")])!;
        await using var host = CreateHost();
        await host.StartAsync();
        var lease = await host.Services.GetRequiredService<IEventBusContributionController>()
            .CreateAsync(new EventBusContributionRequest("plugin.collectible", privateContracts: [descriptor]));
        await lease.DisposeAsync();
        await host.StopAsync();
        loadContext.Unload();
        return (weakReference, lease);
    }

    private sealed record SharedPluginEvent(int Value);
    private sealed record OtherPluginEvent(int Value);
    private sealed record PrivatePluginEvent(int Value);

    private sealed class PluginTestLoadContext : AssemblyLoadContext, IDisposable
    {
        public PluginTestLoadContext() : base(isCollectible: true) { }
        protected override Assembly? Load(AssemblyName assemblyName) => null;
        public void Dispose() => Unload();
    }

    private sealed class ReentrantHostDiagnostics : IHostDiagnostics
    {
        private readonly object _syncRoot = new();
        private readonly List<HostDiagnosticRecord> _records = [];

        public Action<HostDiagnosticRecord>? OnWrite { get; set; }

        public IReadOnlyList<HostDiagnosticRecord> Records
        {
            get
            {
                lock (_syncRoot)
                {
                    return Array.AsReadOnly(_records.ToArray());
                }
            }
        }

        public void Write(HostDiagnosticRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            lock (_syncRoot)
            {
                _records.Add(record);
            }

            OnWrite?.Invoke(record);
        }

        public void Complete()
        {
        }
    }
}
