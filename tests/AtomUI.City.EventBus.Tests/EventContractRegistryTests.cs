using System.Reflection;
using System.Runtime.Loader;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventContractRegistryTests
{
    [Fact]
    public void ContractPlaneValuesAreStable()
    {
        Assert.Equal(0, (int)EventContractPlane.Shared);
        Assert.Equal(1, (int)EventContractPlane.PluginPrivate);
    }

    [Fact]
    public void ContractIdRejectsSurroundingWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new EventContractId(" atomui.city.tests.event.v1 "));
    }

    [Fact]
    public void ContractIdRejectsControlCharacters()
    {
        Assert.Throws<ArgumentException>(() => new EventContractId("atomui.city.tests\n.event.v1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ContractIdRejectsMissingValue(string value)
    {
        Assert.Throws<ArgumentException>(() => new EventContractId(value));
    }

    [Fact]
    public void ContractIdRejectsNullValue()
    {
        Assert.Throws<ArgumentNullException>(() => new EventContractId(null!));
    }

    [Fact]
    public void SharedContractDescriptorRequiresSharedAssemblyMatch()
    {
        var contractId = new EventContractId("atomui.city.tests.shared.v1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => EventContractDescriptor.Shared<TestEvent>(contractId, typeof(string).Assembly));

        Assert.Contains(typeof(TestEvent).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedContractDescriptorRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(
            () => EventContractDescriptor.Shared<TestEvent>(default, typeof(TestEvent).Assembly));
    }

    [Fact]
    public void PluginPrivateContractDescriptorRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(
            () => EventContractDescriptor.PluginPrivate<TestEvent>(default));
    }

    [Fact]
    public void ImplicitDefaultSharedFactoryIsNotPublicApi()
    {
        Assert.Null(typeof(EventContractDescriptor).GetMethod(
            "DefaultShared",
            BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void PluginPrivateContractDescriptorRejectsDefaultLoadContextType()
    {
        var contractId = new EventContractId("atomui.city.tests.false-private.v1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => EventContractDescriptor.PluginPrivate<TestEvent>(contractId));

        Assert.Contains(typeof(TestEvent).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginPrivateContractDescriptorRequiresCollectibleNonDefaultLoadContext()
    {
        using var loadContext = new TestLoadContext();
        var assembly = loadContext.LoadFromAssemblyPath(
            typeof(EventContractRegistryTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(TestEvent).FullName!, throwOnError: true)!;
        var contractId = new EventContractId("atomui.city.tests.real-private.v1");

        var descriptor = CreatePluginPrivateDescriptor(eventType, contractId);

        Assert.Equal(contractId, descriptor.ContractId);
        Assert.Equal(eventType, descriptor.EventType);
        Assert.Equal(assembly, descriptor.Assembly);
        Assert.Equal(EventContractPlane.PluginPrivate, descriptor.Plane);
    }

    [Fact]
    public void ContractRegistryRejectsDuplicateContractId()
    {
        var contractId = new EventContractId("atomui.city.tests.duplicate.v1");
        var registry = new InMemoryEventContractRegistry();

        registry.Register(EventContractDescriptor.Shared<TestEvent>(contractId, typeof(TestEvent).Assembly));

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(EventContractDescriptor.Shared<OtherEvent>(contractId, typeof(OtherEvent).Assembly)));

        Assert.Contains(contractId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractRegistryRejectsDuplicateDescriptorRegistration()
    {
        var contractId = new EventContractId("atomui.city.tests.duplicate-same.v1");
        var descriptor = EventContractDescriptor.Shared<TestEvent>(
            contractId,
            typeof(TestEvent).Assembly);
        var registry = new InMemoryEventContractRegistry();

        registry.Register(descriptor);

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(descriptor));

        Assert.Contains(contractId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedContractRegistryRejectsPluginPrivateDescriptor()
    {
        var contractId = new EventContractId("atomui.city.tests.private.v1");
        var registry = new InMemoryEventContractRegistry();
        using var loadContext = new TestLoadContext();
        var assembly = loadContext.LoadFromAssemblyPath(
            typeof(EventContractRegistryTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(TestEvent).FullName!, throwOnError: true)!;
        var descriptor = CreatePluginPrivateDescriptor(eventType, contractId);

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(descriptor));

        Assert.Contains(contractId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractRegistryReturnsDefaultDescriptorForUnregisteredInternalEvent()
    {
        var registry = new InMemoryEventContractRegistry();

        var descriptor = registry.GetOrCreate<TestEvent>();

        Assert.Equal(new EventContractId(typeof(TestEvent).FullName!), descriptor.ContractId);
        Assert.Equal(EventContractPlane.Shared, descriptor.Plane);
        Assert.Equal(typeof(TestEvent), descriptor.EventType);
    }

    [Fact]
    public void ContractRegistryKeepsDefaultDescriptorMappingStable()
    {
        var registry = new InMemoryEventContractRegistry();
        var descriptor = registry.GetOrCreate<TestEvent>();
        var changedContractId = new EventContractId("atomui.city.tests.changed.v1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(EventContractDescriptor.Shared<TestEvent>(changedContractId, typeof(TestEvent).Assembly)));

        Assert.Contains(typeof(TestEvent).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Equal(descriptor.ContractId, registry.GetOrCreate<TestEvent>().ContractId);
    }

    [Fact]
    public void FreezePublishesSortedImmutableSnapshotAndIsIdempotent()
    {
        var registry = new InMemoryEventContractRegistry();
        var second = EventContractDescriptor.Shared<TestEvent>(
            new EventContractId("atomui.city.tests.z-second.v1"),
            typeof(TestEvent).Assembly);
        var first = EventContractDescriptor.Shared<OtherEvent>(
            new EventContractId("atomui.city.tests.a-first.v1"),
            typeof(OtherEvent).Assembly);
        registry.Register(second);
        registry.Register(first);

        registry.Freeze();
        var snapshot = registry.Descriptors;
        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Equal([first, second], snapshot);
        Assert.Same(snapshot, registry.Descriptors);
        Assert.Throws<NotSupportedException>(
            () => ((IList<EventContractDescriptor>)snapshot).Add(first));
    }

    [Fact]
    public void FrozenRegistryRejectsRegistrationAndImplicitCreation()
    {
        var registry = new InMemoryEventContractRegistry();
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(
                EventContractDescriptor.Shared<TestEvent>(
                    new EventContractId("atomui.city.tests.frozen.v1"),
                    typeof(TestEvent).Assembly)));
        Assert.Throws<InvalidOperationException>(() => registry.GetOrCreate<TestEvent>());
        Assert.Empty(registry.Descriptors);
    }

    [Fact]
    public void RegistrySupportsExactIdAndTypeLookup()
    {
        var contractId = new EventContractId("atomui.city.tests.lookup.v1");
        var descriptor = EventContractDescriptor.Shared<TestEvent>(
            contractId,
            typeof(TestEvent).Assembly);
        var registry = new InMemoryEventContractRegistry();
        registry.Register(descriptor);
        registry.Freeze();

        Assert.True(registry.TryGet(contractId, out var byId));
        Assert.True(registry.TryGet(typeof(TestEvent), out var byType));
        Assert.Same(descriptor, byId);
        Assert.Same(descriptor, byType);
        Assert.False(registry.TryGet(
            new EventContractId("atomui.city.tests.missing.v1"),
            out var missing));
        Assert.Null(missing);
        Assert.Throws<ArgumentException>(
            () => registry.TryGet(default(EventContractId), out _));
        Assert.Throws<ArgumentNullException>(() => registry.TryGet((Type)null!, out _));
    }

    [Fact]
    public void RegistryDoesNotUseAssignableTypeMatching()
    {
        var registry = new InMemoryEventContractRegistry();
        registry.Register(
            EventContractDescriptor.Shared<BaseEvent>(
                new EventContractId("atomui.city.tests.base.v1"),
                typeof(BaseEvent).Assembly));

        Assert.True(registry.TryGet(typeof(BaseEvent), out _));
        Assert.False(registry.TryGet(typeof(DerivedEvent), out var derived));
        Assert.Null(derived);
    }

    [Fact]
    public void MutableRegistrySnapshotsDoNotChangeAfterPublication()
    {
        var registry = new InMemoryEventContractRegistry();
        var first = EventContractDescriptor.Shared<TestEvent>(
            new EventContractId("atomui.city.tests.snapshot-first.v1"),
            typeof(TestEvent).Assembly);
        var second = EventContractDescriptor.Shared<OtherEvent>(
            new EventContractId("atomui.city.tests.snapshot-second.v1"),
            typeof(OtherEvent).Assembly);
        registry.Register(first);

        var snapshot = registry.Descriptors;
        registry.Register(second);

        Assert.Equal([first], snapshot);
        Assert.Equal([first, second], registry.Descriptors);
    }

    [Fact]
    public async Task ConcurrentImplicitCreationPublishesOneDescriptorInstance()
    {
        var registry = new InMemoryEventContractRegistry();
        using var start = new ManualResetEventSlim();
        var operations = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return registry.GetOrCreate<TestEvent>();
            }))
            .ToArray();

        start.Set();
        var descriptors = await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(descriptors, descriptor => Assert.Same(descriptors[0], descriptor));
        Assert.Same(descriptors[0], Assert.Single(registry.Descriptors));
    }

    [Fact]
    public void SharedDescriptorRejectsContractTypeFromCollectibleLoadContext()
    {
        using var loadContext = new TestLoadContext();
        var assembly = loadContext.LoadFromAssemblyPath(
            typeof(EventContractRegistryTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(TestEvent).FullName!, throwOnError: true)!;
        var sharedFactory = typeof(EventContractDescriptor)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(EventContractDescriptor.Shared) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 2);
        var closedFactory = sharedFactory.MakeGenericMethod(eventType);

        var exception = Assert.Throws<TargetInvocationException>(
            () => closedFactory.Invoke(
                null,
                [new EventContractId("atomui.city.tests.collectible.v1"), assembly]));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void ImplicitContractCreationRejectsTypeFromCollectibleLoadContext()
    {
        using var loadContext = new TestLoadContext();
        var assembly = loadContext.LoadFromAssemblyPath(
            typeof(EventContractRegistryTests).Assembly.Location);
        var eventType = assembly.GetType(typeof(TestEvent).FullName!, throwOnError: true)!;
        var registry = new InMemoryEventContractRegistry();
        var getOrCreate = typeof(InMemoryEventContractRegistry)
            .GetMethod(nameof(InMemoryEventContractRegistry.GetOrCreate))!
            .MakeGenericMethod(eventType);

        var exception = Assert.Throws<TargetInvocationException>(
            () => getOrCreate.Invoke(registry, null));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Empty(registry.Descriptors);
    }

    [Fact]
    public async Task FreezeRacingRegistrationNeverPublishesPartialMapping()
    {
        for (var iteration = 0; iteration < 250; iteration++)
        {
            var contractId = new EventContractId($"atomui.city.tests.race-{iteration}.v1");
            var descriptor = EventContractDescriptor.Shared<TestEvent>(
                contractId,
                typeof(TestEvent).Assembly);
            var registry = new InMemoryEventContractRegistry();
            using var start = new ManualResetEventSlim();
            var registered = false;

            var registration = Task.Run(() =>
            {
                start.Wait();

                try
                {
                    registry.Register(descriptor);
                    registered = true;
                }
                catch (InvalidOperationException)
                {
                    // Freeze won the transaction.
                }
            });
            var freeze = Task.Run(() =>
            {
                start.Wait();
                registry.Freeze();
            });

            start.Set();
            await Task.WhenAll(registration, freeze).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(registry.IsFrozen);
            Assert.Equal(registered, registry.TryGet(contractId, out var byId));
            Assert.Equal(registered, registry.TryGet(typeof(TestEvent), out var byType));
            Assert.Equal(registered ? 1 : 0, registry.Descriptors.Count);
            Assert.Equal(byId, byType);
        }
    }

    [Fact]
    public async Task FrozenRegistryRejectsUnknownContractBeforeSubscribeOrPublish()
    {
        var registry = new InMemoryEventContractRegistry();
        registry.Freeze();
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(registry, diagnostics);
        var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");

        var subscribeException = Assert.Throws<InvalidOperationException>(
            () => eventBus.Subscribe<TestEvent>(
                owner,
                (_, _) => ValueTask.CompletedTask));
        var publishException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PublishAsync(new TestEvent("unknown")));
        var postException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PostAsync(new TestEvent("unknown")));

        Assert.Contains(typeof(TestEvent).FullName!, subscribeException.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(TestEvent).FullName!, publishException.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(TestEvent).FullName!, postException.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventSubscriptionAdded);
        var contractRejections = diagnostics.Records
            .Where(record => record.Code == EventDiagnosticIds.EventContractRejected)
            .ToArray();
        Assert.Equal(3, contractRejections.Length);
        Assert.Equal(
            ["post", "publish", "subscribe"],
            contractRejections
                .Select(record => record.Context!["operation"])
                .Order(StringComparer.Ordinal));
        Assert.All(contractRejections, record =>
        {
            Assert.Equal(typeof(TestEvent).FullName, record.Context!["eventType"]);
            Assert.Equal(EventChannel<TestEvent>.Default.Name, record.Context["channel"]);
        });
        Assert.Empty(registry.Descriptors);
    }

    private sealed record TestEvent(string Value);

    private sealed record OtherEvent(string Value);

    private class BaseEvent;

    private sealed class DerivedEvent : BaseEvent;

    private static EventContractDescriptor CreatePluginPrivateDescriptor(
        Type eventType,
        EventContractId contractId)
    {
        var factory = typeof(EventContractDescriptor)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(EventContractDescriptor.PluginPrivate) &&
                method.IsGenericMethodDefinition);

        return (EventContractDescriptor)factory
            .MakeGenericMethod(eventType)
            .Invoke(null, [contractId])!;
    }

    private sealed class TestLoadContext : AssemblyLoadContext, IDisposable
    {
        public TestLoadContext()
            : base(isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return null;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
