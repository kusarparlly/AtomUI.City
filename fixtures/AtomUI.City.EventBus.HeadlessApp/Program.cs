using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus.HeadlessApp;

[ApplicationModule]
[DependsOn(typeof(EventBusModule))]
[DependsOn(typeof(DogfoodModuleTwo))]
[DependsOn(typeof(DogfoodModuleThree))]
[DependsOn(typeof(DogfoodModuleFour))]
[DependsOn(typeof(DogfoodModuleFive))]
public sealed class HeadlessApplicationModule : ModuleBase;

[Module("AtomUI.City.EventBus.Dogfood.Two")]
public sealed class DogfoodModuleTwo : ModuleBase;

[Module("AtomUI.City.EventBus.Dogfood.Three")]
public sealed class DogfoodModuleThree : ModuleBase;

[Module("AtomUI.City.EventBus.Dogfood.Four")]
public sealed class DogfoodModuleFour : ModuleBase;

[Module("AtomUI.City.EventBus.Dogfood.Five")]
public sealed class DogfoodModuleFive : ModuleBase;

[EventContract("atomui.city.eventbus.aot-message.v1", typeof(HeadlessApplicationModule), SchemaVersion = 1)]
[EventChannel("aot", Capacity = 16, BackpressurePolicy = EventChannelBackpressurePolicy.Wait)]
[EventChannel("serialized", Capacity = 32)]
[EventChannel("partitioned", Capacity = 32, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 4)]
[EventChannel("concurrent", Capacity = 32, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 4)]
public sealed record AotMessage(int Value);

[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "aot")]
public sealed class AotMessageHandler : IEventHandler<AotMessage>
{
    private static int _lastValue;

    public static int LastValue => Volatile.Read(ref _lastValue);

    public ValueTask HandleAsync(EventContext<AotMessage> context, CancellationToken cancellationToken)
    {
        Volatile.Write(ref _lastValue, context.Event.Value);
        return ValueTask.CompletedTask;
    }
}

public abstract class DogfoodHandler : IEventHandler<AotMessage>
{
    private static int _handledCount;
    public static int HandledCount => Volatile.Read(ref _handledCount);
    public ValueTask HandleAsync(EventContext<AotMessage> context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _handledCount);
        return ValueTask.CompletedTask;
    }
}

[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "serialized")] public sealed class DogfoodHandler01 : DogfoodHandler;
[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "partitioned")] public sealed class DogfoodHandler02 : DogfoodHandler;
[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "concurrent")] public sealed class DogfoodHandler03 : DogfoodHandler;
[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "serialized")] public sealed class DogfoodHandler04 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleTwo), ChannelName = "partitioned")] public sealed class DogfoodHandler05 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleTwo), ChannelName = "concurrent")] public sealed class DogfoodHandler06 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleTwo), ChannelName = "serialized")] public sealed class DogfoodHandler07 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleTwo), ChannelName = "partitioned")] public sealed class DogfoodHandler08 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleThree), ChannelName = "concurrent")] public sealed class DogfoodHandler09 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleThree), ChannelName = "serialized")] public sealed class DogfoodHandler10 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleThree), ChannelName = "partitioned")] public sealed class DogfoodHandler11 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleThree), ChannelName = "concurrent")] public sealed class DogfoodHandler12 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFour), ChannelName = "serialized")] public sealed class DogfoodHandler13 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFour), ChannelName = "partitioned")] public sealed class DogfoodHandler14 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFour), ChannelName = "concurrent")] public sealed class DogfoodHandler15 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFour), ChannelName = "serialized")] public sealed class DogfoodHandler16 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFive), ChannelName = "partitioned")] public sealed class DogfoodHandler17 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFive), ChannelName = "concurrent")] public sealed class DogfoodHandler18 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFive), ChannelName = "serialized")] public sealed class DogfoodHandler19 : DogfoodHandler;
[EventHandler(typeof(DogfoodModuleFive), ChannelName = "concurrent")] public sealed class DogfoodHandler20 : DogfoodHandler;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args is ["--test-entry-failure"])
        {
            throw new InvalidOperationException("EventBus fixture entry failure.");
        }

        await VerifyStopBeforeStartAsync(args);

        var builder = CreateBuilder(args);
        await using var host = builder.Build();
        await host.StartAsync();
        var result = await host.Services.GetRequiredService<IEventPublisher>()
            .PublishAsync(new EventChannel<AotMessage>("aot"), new AotMessage(808));
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var serialized = await publisher.PublishAsync(
            new EventChannel<AotMessage>("serialized"), new AotMessage(1));
        var partitioned = await publisher.PublishAsync(
            new EventChannel<AotMessage>("partitioned"), new AotMessage(2),
            new EventPublishOptions { PartitionKey = "dogfood" });
        var concurrent = await publisher.PublishAsync(
            new EventChannel<AotMessage>("concurrent"), new AotMessage(3));
        var moduleCount = host.Services.GetRequiredService<IModuleRegistry>().Modules.Count;
        var dogfoodDeliveries = serialized.Deliveries.Count + partitioned.Deliveries.Count + concurrent.Deliveries.Count;
        Ensure(result.Deliveries.Count == 1 && result.Deliveries[0].Succeeded,
            "The generated AOT handler did not complete successfully.");
        Ensure(AotMessageHandler.LastValue == 808, "The generated AOT handler received the wrong payload.");
        Ensure(moduleCount >= 6, "The product matrix did not select at least six modules.");
        Ensure(dogfoodDeliveries == 20 && DogfoodHandler.HandledCount == 20,
            "The generated 20-handler product matrix was incomplete.");

        await VerifyAdmissionAndOwnershipAsync(host);
        await VerifyFailurePoliciesAsync(host);
        VerifyDiagnosticsAndMetrics(host);

        await Task.WhenAll(host.StopAsync(), host.StopAsync());
        var stoppedPublicationRejected = false;
        try
        {
            await publisher.PublishAsync(new EventChannel<AotMessage>("aot"), new AotMessage(909));
        }
        catch (InvalidOperationException)
        {
            stoppedPublicationRejected = true;
        }

        Ensure(stoppedPublicationRejected, "The stopped EventBus accepted a new publication.");
        Console.WriteLine($"EVENTBUS_DOGFOOD modules={moduleCount} handlers=20 deliveries={dogfoodDeliveries}");
        Console.WriteLine("EVENTBUS_DYNAMIC_MATRIX admission=ok ownership=ok failures=ok metrics=ok concurrent-stop=ok");
        Console.WriteLine("EVENTBUS_AOT_OK");
        return 0;
    }

    private static IApplicationHostBuilder CreateBuilder(string[] args)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.UseModule<EventBusModule>();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.EventBus.HeadlessApp";
            options.ApplicationName = "AtomUI.City.EventBus.HeadlessApp";
        });
        return builder;
    }

    private static async Task VerifyStopBeforeStartAsync(string[] args)
    {
        await using var unstartedHost = CreateBuilder(args).Build();
        await unstartedHost.StopAsync();
        Ensure(unstartedHost.HostScope.State == LifecycleScopeState.Stopped,
            "Stop-before-Start did not stop the Host scope.");
    }

    private static async Task VerifyAdmissionAndOwnershipAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var applicationScope = host.ApplicationScope ??
            throw new InvalidOperationException("The running Host did not expose its ApplicationScope.");
        await using var owner = applicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-order-owner");
        var observed = new List<int>();
        var serializedChannel = new EventChannel<AotMessage>("serialized");
        subscriber.Subscribe(owner, serializedChannel, (context, _) =>
        {
            lock (observed)
            {
                observed.Add(context.Event.Value);
            }
            return ValueTask.CompletedTask;
        });

        var post = await publisher.PostAsync(serializedChannel, new AotMessage(100));
        var publish = await publisher.PublishAsync(serializedChannel, new AotMessage(101));
        Ensure(post.Accepted && publish.Succeeded, "The mixed Post/Publish admission was not accepted.");
        lock (observed)
        {
            Ensure(observed.SequenceEqual([100, 101]), "Post and Publish did not preserve shared admission order.");
        }

        await owner.StopAsync();
        await publisher.PublishAsync(serializedChannel, new AotMessage(102));
        lock (observed)
        {
            Ensure(observed.Count == 2, "A stopped owner continued to receive events.");
        }
    }

    private static async Task VerifyFailurePoliciesAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var applicationScope = host.ApplicationScope ??
            throw new InvalidOperationException("The running Host did not expose its ApplicationScope.");
        var channel = new EventChannel<AotMessage>("aot");

        await using (var continueOwner = applicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-continue"))
        {
            subscriber.Subscribe<AotMessage>(continueOwner, channel,
                (_, _) => throw new InvalidOperationException("continue"),
                EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.ContinueAndReport));
            var result = await publisher.PublishAsync(channel, new AotMessage(200));
            Ensure(result.FailedCount == 1, "ContinueAndReport did not return the handler failure.");
        }

        await using (var stopOwner = applicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-stop"))
        {
            var laterHandlerCalled = false;
            subscriber.Subscribe<AotMessage>(stopOwner, channel,
                (_, _) => throw new InvalidOperationException("stop"),
                EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
            subscriber.Subscribe<AotMessage>(stopOwner, channel, (_, _) =>
            {
                laterHandlerCalled = true;
                return ValueTask.CompletedTask;
            });
            var result = await publisher.PublishAsync(channel, new AotMessage(201));
            Ensure(result.FailedCount == 1 && result.SkippedCount >= 1 && !laterHandlerCalled,
                "StopPublication did not prevent a later handler from starting.");
        }

        await using (var failOwner = applicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-fail"))
        {
            subscriber.Subscribe<AotMessage>(failOwner, channel,
                (_, _) => throw new InvalidOperationException("fail-publisher"),
                EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));
            var failureObserved = false;
            try
            {
                await publisher.PublishAsync(channel, new AotMessage(202));
            }
            catch (InvalidOperationException exception) when (exception.Message == "fail-publisher")
            {
                failureObserved = true;
            }
            Ensure(failureObserved, "FailPublisher did not propagate the handler exception.");
        }

        await using (var disableOwner = applicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-disable"))
        {
            var invocationCount = 0;
            var subscription = subscriber.Subscribe<AotMessage>(disableOwner, channel, (_, _) =>
            {
                Interlocked.Increment(ref invocationCount);
                throw new InvalidOperationException("disable");
            }, EventSubscriptionOptions.Serialized
                .WithErrorPolicy(EventErrorPolicy.DisableSubscription)
                .WithDisableSubscriptionAfterFailures(2));
            await publisher.PublishAsync(channel, new AotMessage(203));
            await publisher.PublishAsync(channel, new AotMessage(204));
            await subscription.StopAsync();
            await publisher.PublishAsync(channel, new AotMessage(205));
            Ensure(invocationCount == 2 && subscription.State == EventSubscriptionState.Disposed,
                "DisableSubscription did not quiesce after its configured failure threshold.");
        }
    }

    private static void VerifyDiagnosticsAndMetrics(IApplicationHost host)
    {
        var busSnapshot = host.Services.GetRequiredService<IEventBusMonitor>().GetSnapshot();
        var channelSnapshots = host.Services.GetRequiredService<IEventChannelMonitor>().GetChannelSnapshots();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        Ensure(busSnapshot.PublicationCount > 0 && busSnapshot.DeliverySucceededCount > 0 &&
               busSnapshot.DeliveryFailedCount >= 4,
            "The EventBus metrics did not observe the dynamic product matrix.");
        Ensure(channelSnapshots.Count >= 4 && channelSnapshots.All(snapshot =>
                snapshot.PendingCount == 0 && snapshot.InFlightCount == 0),
            "A channel retained pending or in-flight work after the product matrix.");
        Ensure(diagnostics.Records.Any(record => record.Code == EventDiagnosticIds.EventPublished) &&
               diagnostics.Records.Any(record => record.Code == EventDiagnosticIds.EventDeliveryFailed),
            "The Host diagnostics did not observe successful and failed EventBus deliveries.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
