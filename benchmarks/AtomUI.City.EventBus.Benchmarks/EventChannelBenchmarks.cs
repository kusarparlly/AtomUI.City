using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Diagnostics;
using BenchmarkDotNet.Attributes;

namespace AtomUI.City.EventBus.Benchmarks;

[MemoryDiagnoser]
public class EventPublicationBenchmarks
{
    private InMemoryEventBus _eventBus = null!;
    private LifecycleScope _owner = null!;
    private int _sequence;

    [Params(0, 1, 20)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "eventbus-benchmark");
        _eventBus = new InMemoryEventBus();
        for (var index = 0; index < SubscriberCount; index++)
        {
            _eventBus.Subscribe<BenchmarkEvent>(
                _owner,
                static (_, _) => ValueTask.CompletedTask);
        }
    }

    [Benchmark]
    public ValueTask<EventPublishResult> PublishAsync()
    {
        return _eventBus.PublishAsync(new BenchmarkEvent(Interlocked.Increment(ref _sequence)));
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _eventBus.DisposeAsync();
        await _owner.DisposeAsync();
    }
}

[MemoryDiagnoser]
public class EventDiagnosticsBenchmarks
{
    private InMemoryEventBus _eventBus = null!;
    private int _sequence;

    [Params(false, true)]
    public bool DiagnosticsEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new InMemoryEventBus(
            diagnostics: DiagnosticsEnabled ? new InMemoryHostDiagnostics() : null);
    }

    [Benchmark]
    public ValueTask<EventPublishResult> PublishAsync() =>
        _eventBus.PublishAsync(new BenchmarkEvent(Interlocked.Increment(ref _sequence)));

    [GlobalCleanup]
    public async Task CleanupAsync() => await _eventBus.DisposeAsync();
}

[MemoryDiagnoser]
public class EventSubscriptionLifecycleBenchmarks
{
    private InMemoryEventBus _eventBus = null!;
    private LifecycleScope _owner = null!;

    [GlobalSetup]
    public void Setup()
    {
        _owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "eventbus-subscription-benchmark");
        _eventBus = new InMemoryEventBus();
    }

    [Benchmark]
    public void CreateAndReleaseSubscription()
    {
        using var subscription = _eventBus.Subscribe<BenchmarkEvent>(
            _owner,
            static (_, _) => ValueTask.CompletedTask);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _eventBus.DisposeAsync();
        await _owner.DisposeAsync();
    }
}

[MemoryDiagnoser]
public class EventBackpressureBenchmarks
{
    private InMemoryEventBus _eventBus = null!;
    private LifecycleScope _owner = null!;
    private int _sequence;

    [Params(EventChannelBackpressurePolicy.Wait, EventChannelBackpressurePolicy.Reject)]
    public EventChannelBackpressurePolicy Policy { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "eventbus-backpressure-benchmark");
        _eventBus = new InMemoryEventBus(channelOptions: new EventChannelOptions
        {
            Capacity = 4,
            BackpressurePolicy = Policy,
            QueueWaitTimeout = TimeSpan.FromSeconds(5)
        });
        _eventBus.Subscribe<BenchmarkEvent>(
            _owner,
            static async (_, _) => await Task.Delay(1).ConfigureAwait(false));
    }

    [Benchmark]
    public async Task<int> BurstPostAsync()
    {
        var accepted = 0;
        for (var index = 0; index < 32; index++)
        {
            var result = await _eventBus.PostAsync(
                new BenchmarkEvent(Interlocked.Increment(ref _sequence))).ConfigureAwait(false);
            if (result.Accepted)
            {
                accepted++;
            }
        }

        return accepted;
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _eventBus.DisposeAsync();
        await _owner.DisposeAsync();
    }
}

[MemoryDiagnoser]
public class EventChannelCardinalityBenchmarks
{
    private InMemoryEventBus _eventBus = null!;
    private EventChannel<BenchmarkEvent>[] _channels = null!;
    private int _cursor;

    [Params(1, 64, 256)]
    public int ChannelCount { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _eventBus = new InMemoryEventBus(
            runtimeOptions: new EventBusRuntimeOptions
            {
                MaximumChannelRuntimes = EventBusRuntimeOptions.DefaultMaximumChannelRuntimes,
            });
        _channels = Enumerable.Range(0, ChannelCount)
            .Select(index => new EventChannel<BenchmarkEvent>($"channel-{index}"))
            .ToArray();

        foreach (var channel in _channels)
        {
            await _eventBus.PublishAsync(channel, new BenchmarkEvent(0));
        }
    }

    [Benchmark]
    public ValueTask<EventPublishResult> PublishToExistingChannelAsync()
    {
        var index = (Interlocked.Increment(ref _cursor) & int.MaxValue) % _channels.Length;
        return _eventBus.PublishAsync(_channels[index], new BenchmarkEvent(index));
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _eventBus.DisposeAsync();
    }
}

[MemoryDiagnoser]
public class EventChannelLimitBenchmarks
{
    private const int RuntimeLimit = 256;
    private InMemoryEventBus _eventBus = null!;
    private EventChannel<BenchmarkEvent> _overflowChannel;
    private int _sequence;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _eventBus = new InMemoryEventBus(
            runtimeOptions: new EventBusRuntimeOptions { MaximumChannelRuntimes = RuntimeLimit });

        for (var index = 0; index < RuntimeLimit; index++)
        {
            await _eventBus.PublishAsync(
                new EventChannel<BenchmarkEvent>($"channel-{index}"),
                new BenchmarkEvent(index));
        }

        _overflowChannel = new EventChannel<BenchmarkEvent>("overflow");
    }

    [Benchmark]
    public ValueTask<EventPostResult> RejectOverflowChannelAsync()
    {
        return _eventBus.PostAsync(
            _overflowChannel,
            new BenchmarkEvent(Interlocked.Increment(ref _sequence)));
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _eventBus.DisposeAsync();
    }
}

public sealed record BenchmarkEvent(int Sequence);
