using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusHostIntegrationTests
{
    [Fact]
    public void BuildRejectsSelectedGeneratedHandlerWhenItsContractOwnerIsMissing()
    {
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.UseModule<MissingGeneratedContractModule>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(typeof(MissingGeneratedContractHandler).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(MissingGeneratedContractEvent).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("selected Module contributions", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, MissingGeneratedContractHandler.ActivationCount);
    }

    [Fact]
    public async Task EventBusModuleRegistersStartsAndStopsOneHostManagedRuntime()
    {
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1")));

        await using var host = builder.Build();
        var eventBus = host.Services.GetRequiredService<IEventBus>();
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var channelMonitor = host.Services.GetRequiredService<IEventChannelMonitor>();

        Assert.Same(eventBus, publisher);
        Assert.Same(eventBus, subscriber);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.PublishAsync(new HostEvent(1)));
        Assert.Empty(channelMonitor.GetChannelSnapshots());

        await host.StartAsync();

        Assert.NotNull(host.ApplicationScope);
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = subscriber.Subscribe<HostEvent>(
            host.ApplicationScope,
            (context, cancellationToken) =>
            {
                received.TrySetResult(context.Event.Value);
                return ValueTask.CompletedTask;
            });

        var result = await publisher.PublishAsync(new HostEvent(42));

        Assert.Equal(42, await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Single(result.Deliveries);
        Assert.True(result.Deliveries[0].Succeeded);

        await host.StopAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await publisher.PublishAsync(new HostEvent(2)));
    }

    [Fact]
    public async Task EventBusModuleAndManualRegistrationRemainOneManagedSingleton()
    {
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
        {
            services.AddEventBus();
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1"));
        });

        await using var host = builder.Build();

        Assert.Same(
            host.Services.GetRequiredService<IEventBus>(),
            host.Services.GetRequiredService<IEventPublisher>());

        await host.StartAsync();
        var posted = await host.Services
            .GetRequiredService<IEventPublisher>()
            .PostAsync(new HostEvent(1));

        Assert.True(posted.Accepted);
        Assert.DoesNotContain(
            host.Services.GetServices<IHostedService>(),
            service => service.GetType().Assembly == typeof(EventBusModule).Assembly);
    }

    [Fact]
    public async Task DependentModuleCanUseEventBusFromItsPreInitializationHook()
    {
        PreInitializationPublisherModule.Reset();
        var builder = CreateBuilder();
        builder
            .UseModule<PreInitializationPublisherModule>()
            .UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1")));

        await using var host = builder.Build();

        await host.StartAsync();

        Assert.Equal(1, PreInitializationPublisherModule.AcceptedCount);
        Assert.Contains(
            host.Services.GetRequiredService<IModuleRegistry>().Modules,
            descriptor => descriptor.ModuleType == typeof(EventBusModule));
    }

    [Fact]
    public async Task StandaloneAddEventBusRemainsImmediatelyUsable()
    {
        var services = new ServiceCollection();
        services.AddEventContract<HostEvent>(
            new EventContractId("atomui.city.tests.host-event.v1"));

        await using var provider = services.BuildServiceProvider();
        var result = await provider
            .GetRequiredService<IEventPublisher>()
            .PostAsync(new HostEvent(1));

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task StopBeforeStartKeepsHostManagedRuntimeClosed()
    {
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1")));

        await using var host = builder.Build();
        var publisher = host.Services.GetRequiredService<IEventPublisher>();

        await host.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.PublishAsync(new HostEvent(1)));
    }

    [Fact]
    public async Task GeneratedHandlerIsActivatedByApplicationScopeAndReleasedByHostStop()
    {
        GeneratedHostEventHandler.Reset();
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
        {
            services.AddEventContract<HostEvent>(new EventContractId("atomui.city.tests.host-event.v1"));
            services.AddSingleton<GeneratedHostEventHandler>();
            services.AddSingleton(GeneratedEventHandlerDescriptor.Create<HostEvent, GeneratedHostEventHandler>(
                typeof(EventBusModule),
                EventChannel<HostEvent>.DefaultName,
                EventDispatchPolicy.Serialized,
                EventDispatchMode.InlineIfAllowed,
                EventErrorPolicy.ContinueAndReport,
                30_000,
                3));
        });

        await using var host = builder.Build();
        await host.StartAsync();

        var result = await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(new HostEvent(73));

        Assert.Single(result.Deliveries);
        Assert.Equal(73, GeneratedHostEventHandler.LastValue);
        Assert.Equal(1, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);

        await host.StopAsync();

        Assert.Equal(0, host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot().ActiveSubscriptionCount);
    }

    [Fact]
    public async Task StartupRollbackTerminatesEventBusThatAlreadyEnteredRuntime()
    {
        var builder = CreateBuilder();
        builder
            .UseModule<FailingAfterEventBusModule>()
            .UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1")));

        await using var host = builder.Build();
        var publisher = host.Services.GetRequiredService<IEventPublisher>();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Equal("dependent module startup failed", failure.Message);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await publisher.PublishAsync(new HostEvent(1)));
    }

    [Fact]
    public async Task ConcurrentHostStopAndDisposeShareEventBusTermination()
    {
        var builder = CreateBuilder();
        builder.UseModule<EventBusModule>();
        builder.ConfigureServices(services =>
            services.AddEventContract<HostEvent>(
                new EventContractId("atomui.city.tests.host-event.v1")));

        var host = builder.Build();
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        await host.StartAsync();
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = host.Services
            .GetRequiredService<IEventSubscriber>()
            .Subscribe<HostEvent>(
                host.ApplicationScope!,
                async (_, cancellationToken) =>
                {
                    handlerEntered.TrySetResult();
                    await releaseHandler.Task.ConfigureAwait(false);
                });
        var posted = await publisher.PostAsync(new HostEvent(1));
        Assert.True(posted.Accepted);
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var start = new ManualResetEventSlim();
        using var enteredLifecycle = new CountdownEvent(64);

        var callers = Enumerable.Range(0, 64)
            .Select(index => Task.Run(async () =>
            {
                start.Wait();
                var lifecycle = index % 2 == 0
                    ? host.StopAsync()
                    : host.DisposeAsync().AsTask();
                enteredLifecycle.Signal();
                await lifecycle;
            }))
            .ToArray();

        start.Set();
        Assert.True(enteredLifecycle.Wait(TimeSpan.FromSeconds(2)));
        releaseHandler.TrySetResult();
        await Task.WhenAll(callers);

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await publisher.PublishAsync(new HostEvent(1)));
    }

    [Fact]
    public void LifecycleControllerIsNotAPublicContract()
    {
        var controllerType = typeof(EventBusModule).Assembly.GetType(
            "AtomUI.City.EventBus.IEventBusLifecycleController",
            throwOnError: true)!;

        Assert.False(controllerType.IsPublic);
        Assert.False(controllerType.IsNestedPublic);
    }

    private static IApplicationHostBuilder CreateBuilder()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.EventBus.Tests";
            options.ApplicationName = "AtomUI.City.EventBus.Tests";
        });
        return builder;
    }

    private sealed record HostEvent(int Value);

    private sealed record MissingGeneratedContractEvent(int Value);

    private sealed class MissingGeneratedContractHandler : IEventHandler<MissingGeneratedContractEvent>
    {
        private static int _activationCount;

        public MissingGeneratedContractHandler()
        {
            Interlocked.Increment(ref _activationCount);
        }

        public static int ActivationCount => Volatile.Read(ref _activationCount);

        public ValueTask HandleAsync(
            EventContext<MissingGeneratedContractEvent> context,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [DependsOn(typeof(EventBusModule))]
    public sealed class MissingGeneratedContractModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton(
                GeneratedEventHandlerDescriptor.Create<MissingGeneratedContractEvent, MissingGeneratedContractHandler>(
                    typeof(MissingGeneratedContractModule),
                    EventChannel<MissingGeneratedContractEvent>.DefaultName,
                    EventDispatchPolicy.Serialized,
                    EventDispatchMode.InlineIfAllowed,
                    EventErrorPolicy.ContinueAndReport,
                    30_000,
                    3));
        }
    }

    private sealed class GeneratedHostEventHandler : IEventHandler<HostEvent>
    {
        private static int _lastValue;

        public static int LastValue => Volatile.Read(ref _lastValue);

        public static void Reset() => Volatile.Write(ref _lastValue, 0);

        public ValueTask HandleAsync(
            EventContext<HostEvent> context,
            CancellationToken cancellationToken)
        {
            Volatile.Write(ref _lastValue, context.Event.Value);
            return ValueTask.CompletedTask;
        }
    }

    [DependsOn(typeof(EventBusModule))]
    private sealed class PreInitializationPublisherModule : ModuleBase
    {
        private static int _acceptedCount;

        public static int AcceptedCount => Volatile.Read(ref _acceptedCount);

        public static void Reset() => Volatile.Write(ref _acceptedCount, 0);

        public override async ValueTask OnPreApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            var result = await context.Services
                .GetRequiredService<IEventPublisher>()
                .PostAsync(new HostEvent(1), cancellationToken: cancellationToken);

            if (result.Accepted)
            {
                Interlocked.Increment(ref _acceptedCount);
            }
        }
    }

    [DependsOn(typeof(EventBusModule))]
    private sealed class FailingAfterEventBusModule : ModuleBase
    {
        public override ValueTask OnPreApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("dependent module startup failed");
        }
    }
}
