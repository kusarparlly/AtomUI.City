using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusRegistrationTests
{
    [Fact]
    public async Task ServiceCollectionRegistersEventBusContracts()
    {
        var services = new ServiceCollection();

        services.AddEventBus();

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
        var subscriber = serviceProvider.GetRequiredService<IEventSubscriber>();
        var monitor = serviceProvider.GetRequiredService<IEventChannelMonitor>();
        var eventBusMonitor = serviceProvider.GetRequiredService<IEventBusMonitor>();
        var registry = serviceProvider.GetRequiredService<IEventContractRegistry>();

        Assert.Same(eventBus, publisher);
        Assert.Same(eventBus, subscriber);
        Assert.Same(eventBus, monitor);
        Assert.Same(eventBus, eventBusMonitor);
        Assert.IsType<InMemoryEventBus>(eventBus);
        Assert.IsType<InMemoryEventContractRegistry>(registry);
        Assert.Same(
            EventBusRuntimeOptions.Default,
            serviceProvider.GetRequiredService<EventBusRuntimeOptions>());
        Assert.True(registry.IsFrozen);
        Assert.Empty(registry.Descriptors);
    }

    [Fact]
    public async Task ServiceCollectionRegistersDefaultDiagnosticsCollector()
    {
        var services = new ServiceCollection();

        services.AddEventBus();

        await using var serviceProvider = services.BuildServiceProvider();
        var diagnostics = serviceProvider.GetRequiredService<IHostDiagnostics>();

        var inMemoryDiagnostics = Assert.IsType<InMemoryHostDiagnostics>(diagnostics);
        Assert.Equal(EventBusDiagnosticsOptions.DefaultMemoryBufferCapacity, inMemoryDiagnostics.Capacity);
    }

    [Fact]
    public async Task ServiceCollectionUsesRegisteredDiagnosticsCollector()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();

        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.diagnostics.v1"));

        await using var serviceProvider = services.BuildServiceProvider();
        var resolvedDiagnostics = serviceProvider.GetRequiredService<IHostDiagnostics>();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new RegisteredEvent("diagnostics"));

        Assert.Same(diagnostics, resolvedDiagnostics);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventPublished);
    }

    [Fact]
    public async Task ServiceCollectionRegistersEventContractDescriptor()
    {
        var services = new ServiceCollection();
        var contractId = new EventContractId("atomui.city.tests.registration.v1");

        services.AddEventContract<RegisteredEvent>(contractId);

        await using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IEventContractRegistry>();

        var descriptor = registry.GetOrCreate<RegisteredEvent>();

        Assert.True(registry.IsFrozen);
        Assert.Equal(contractId, descriptor.ContractId);
        Assert.Equal(typeof(RegisteredEvent), descriptor.EventType);
    }

    [Fact]
    public async Task EventBusResolutionFreezesCallerProvidedContractRegistry()
    {
        var registry = new InMemoryEventContractRegistry();
        registry.Register(
            EventContractDescriptor.Shared<RegisteredEvent>(
                new EventContractId("atomui.city.tests.custom-registry.v1"),
                typeof(RegisteredEvent).Assembly));
        var services = new ServiceCollection();
        services.AddSingleton<IEventContractRegistry>(registry);
        services.AddEventBus();

        await using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<IEventBus>();

        Assert.True(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => registry.Register(
            EventContractDescriptor.Shared<AnotherRegisteredEvent>(
                new EventContractId("atomui.city.tests.too-late.v1"),
                typeof(AnotherRegisteredEvent).Assembly)));
    }

    [Fact]
    public async Task CallerProvidedRegistryReceivesCollectedContractDescriptorsBeforeFreeze()
    {
        var registry = new InMemoryEventContractRegistry();
        var contractId = new EventContractId("atomui.city.tests.custom-collected.v1");
        var services = new ServiceCollection();
        services.AddSingleton<IEventContractRegistry>(registry);
        services.AddEventContract<RegisteredEvent>(contractId);

        await using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<IEventBus>();

        Assert.True(registry.IsFrozen);
        Assert.True(registry.TryGet(contractId, out var descriptor));
        Assert.Equal(typeof(RegisteredEvent), descriptor!.EventType);
    }

    [Fact]
    public void FrozenCallerProvidedRegistryCannotSilentlyDiscardCollectedDescriptors()
    {
        var registry = new InMemoryEventContractRegistry();
        registry.Freeze();
        var services = new ServiceCollection();
        services.AddSingleton<IEventContractRegistry>(registry);
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.missing-collected.v1"));
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => serviceProvider.GetRequiredService<IEventBus>());

        Assert.Contains("does not contain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceProviderDisposesEventBusSingleton()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        var serviceProvider = services.BuildServiceProvider();
        var eventBus = (InMemoryEventBus)serviceProvider.GetRequiredService<IEventBus>();

        await serviceProvider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PublishAsync(new RegisteredEvent("disposed")));
    }

    [Fact]
    public void EventBusInterfaceExposesDisposeContract()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IEventBus)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IEventBus)));
    }

    [Fact]
    public async Task ServiceCollectionUsesExplicitDefaultChannelOptions()
    {
        var services = new ServiceCollection();
        var options = new EventChannelOptions
        {
            Capacity = 7,
            BackpressurePolicy = EventChannelBackpressurePolicy.Reject,
        };

        services.AddEventBus(options);
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.configured-channel.v1"));

        await using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(options, serviceProvider.GetRequiredService<EventChannelOptions>());
        Assert.IsType<InMemoryEventBus>(serviceProvider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public async Task ServiceCollectionUsesExplicitRuntimeOptions()
    {
        var services = new ServiceCollection();
        var options = new EventBusRuntimeOptions { MaximumChannelRuntimes = 17 };

        services.ConfigureEventBusRuntime(options);

        await using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(options, serviceProvider.GetRequiredService<EventBusRuntimeOptions>());
        Assert.IsType<InMemoryEventBus>(serviceProvider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public async Task ServiceCollectionUsesCallerProvidedDispatchInfrastructure()
    {
        var scheduler = new RecordingBackgroundScheduler();
        var dispatchOptions = new EventBusDispatchOptions
        {
            MaximumConcurrentDeliveriesPerPublication = 5
        };
        var services = new ServiceCollection();
        services.AddSingleton<IEventBackgroundScheduler>(scheduler);
        services.AddSingleton(dispatchOptions);
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.dispatch-infrastructure.v1"));

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<InMemoryEventBus>();
        eventBus.Subscribe<RegisteredEvent>(
            _ => ValueTask.CompletedTask,
            EventSubscriptionOptions.Background());

        var result = await eventBus.PublishAsync(new RegisteredEvent("background"));

        Assert.True(result.Succeeded);
        Assert.Same(scheduler, serviceProvider.GetRequiredService<IEventBackgroundScheduler>());
        Assert.Same(dispatchOptions, serviceProvider.GetRequiredService<EventBusDispatchOptions>());
        Assert.Equal(1, scheduler.RunCount);
    }

    [Fact]
    public async Task ServiceCollectionRegistersAndUsesSafePayloadProjector()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddSingleton(new EventBusDiagnosticsOptions { EnablePayloadProjection = true });
        services.AddEventPayloadDiagnosticProjector<RegisteredEvent, RegisteredEventProjector>();
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.payload-projector.v1"));

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new RegisteredEvent("safe"));

        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventPublished);
        Assert.Equal("safe", record.Context["payload.value"]);
    }

    [Fact]
    public async Task ServiceCollectionUsesConfiguredDiagnosticsOptions()
    {
        var options = new EventBusDiagnosticsOptions { TraceSamplingRate = 0d };
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddEventBus();
        services.ConfigureEventBusDiagnostics(options);
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.diagnostics-options.v1"));

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new RegisteredEvent("not-traced"));

        Assert.Same(options, serviceProvider.GetRequiredService<EventBusDiagnosticsOptions>());
        Assert.DoesNotContain(diagnostics.Records, item => item.Code == EventDiagnosticIds.EventPublished);
    }

    [Fact]
    public async Task ServiceCollectionConfiguresOneNamedContractChannel()
    {
        var services = new ServiceCollection();
        var channel = new EventChannel<RegisteredEvent>("parallel");
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.named-channel.v1"));
        services.ConfigureEventChannel(
            channel,
            new EventChannelOptions
            {
                Capacity = 9,
                ExecutionMode = EventChannelExecutionMode.Concurrent,
                MaximumConcurrency = 3,
            });

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        var monitor = serviceProvider.GetRequiredService<IEventChannelMonitor>();

        await eventBus.PublishAsync(channel, new RegisteredEvent("configured"));

        var snapshot = Assert.Single(monitor.GetChannelSnapshots());
        Assert.Equal("parallel", snapshot.ChannelName);
        Assert.Equal(9, snapshot.Capacity);
        Assert.Equal(EventChannelExecutionMode.Concurrent, snapshot.ExecutionMode);
    }

    [Fact]
    public void DuplicateContractChannelConfigurationIsRejected()
    {
        var services = new ServiceCollection();
        var channel = new EventChannel<RegisteredEvent>("duplicate");
        services.AddEventContract<RegisteredEvent>(
            new EventContractId("atomui.city.tests.duplicate-channel.v1"));
        services.ConfigureEventChannel(channel, new EventChannelOptions());
        services.ConfigureEventChannel(channel, new EventChannelOptions());
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void EventBusModuleRejectsSelectedGeneratedHandlerWithoutItsContractDuringConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(GeneratedEventHandlerDescriptor.Create<RegisteredEvent, GeneratedHandler>(
            typeof(EventBusModule),
            EventChannel<RegisteredEvent>.DefaultName,
            EventDispatchPolicy.Serialized,
            EventDispatchMode.InlineIfAllowed,
            EventErrorPolicy.ContinueAndReport,
            30_000,
            3));
        var context = new ServiceConfigurationContext(new TestApplicationContext(), services);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new EventBusModule().PostConfigureServices(context));

        Assert.Contains(typeof(GeneratedHandler).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(RegisteredEvent).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("selected Module contributions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventBusModuleAcceptsClosedSelectedGeneratedHandlerCatalog()
    {
        var services = new ServiceCollection();
        services.AddSingleton(EventContractDescriptor.GeneratedShared<RegisteredEvent>(
            new EventContractId("atomui.city.tests.generated-closed.v1"),
            typeof(RegisteredEvent).Assembly,
            1,
            "TEST"));
        services.AddSingleton(GeneratedEventHandlerDescriptor.Create<RegisteredEvent, GeneratedHandler>(
            typeof(EventBusModule),
            EventChannel<RegisteredEvent>.DefaultName,
            EventDispatchPolicy.Serialized,
            EventDispatchMode.InlineIfAllowed,
            EventErrorPolicy.ContinueAndReport,
            30_000,
            3));
        var context = new ServiceConfigurationContext(new TestApplicationContext(), services);

        new EventBusModule().PostConfigureServices(context);
    }

    private sealed record RegisteredEvent(string Value);

    private sealed record AnotherRegisteredEvent(string Value);

    private sealed class GeneratedHandler : IEventHandler<RegisteredEvent>
    {
        public ValueTask HandleAsync(
            EventContext<RegisteredEvent> context,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed record TestApplicationContext : IApplicationContext
    {
        public string ApplicationId => "AtomUI.City.EventBus.Tests";
        public Guid ApplicationInstanceId { get; } = Guid.NewGuid();
        public string ApplicationName => "AtomUI.City.EventBus.Tests";
        public string ApplicationVersion => "1.0.0-test";
        public string EnvironmentName => "Testing";
        public string ContentRootPath => Directory.GetCurrentDirectory();
        public string AppDataPath => Path.Combine(ContentRootPath, "app-data");
        public IReadOnlyList<string> StartupArguments { get; } = Array.Empty<string>();
    }

    private sealed class RecordingBackgroundScheduler : IEventBackgroundScheduler
    {
        public int RunCount { get; private set; }

        public async ValueTask RunAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            await callback(cancellationToken);
        }
    }

    private sealed class RegisteredEventProjector : IEventPayloadDiagnosticProjector<RegisteredEvent>
    {
        public EventPayloadDiagnosticSnapshot Project(RegisteredEvent eventData)
        {
            return new EventPayloadDiagnosticSnapshot(
                new Dictionary<string, string?> { ["value"] = eventData.Value });
        }
    }
}
