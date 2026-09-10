using AtomUI.City.Core.Threading;
using BenchmarkDotNet.Attributes;

namespace AtomUI.City.Presentation.Benchmarks;

[MemoryDiagnoser]
public class ViewRegistryBenchmarks
{
    private readonly ViewLookupRequest _request = new(typeof(BenchmarkViewModel), "details");
    private ViewRegistry _registry = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registry = new ViewRegistry();
        _registry.Register(new ViewDescriptor(
            typeof(BenchmarkViewModel),
            typeof(BenchmarkView),
            "details",
            static _ => new BenchmarkView()));
        _registry.Register(
            new ViewDescriptor(
                typeof(BenchmarkViewModel),
                typeof(OverriddenBenchmarkView),
                "details",
                static _ => new OverriddenBenchmarkView(),
                pluginId: "benchmark-plugin",
                contributionId: "benchmark-contribution"),
            new ViewRegistrationOptions { ReplaceExisting = true });
    }

    [Benchmark]
    public ViewDescriptor LocateExactOverride() => _registry.Locate(_request);
}

[MemoryDiagnoser]
public class RouteOutletBenchmarks
{
    private RouteOutlet _outlet = null!;
    private BenchmarkOutletTarget _target = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _target = new BenchmarkOutletTarget();
        _outlet = new RouteOutlet("primary", InlineUiDispatcher.Instance, _target);
        await CommitNextAsync();
    }

    [Benchmark]
    public ValueTask<RouteOutletCommitResult> ReplaceCommittedEntry() => CommitNextAsync();

    [GlobalCleanup]
    public async Task CleanupAsync() => await _outlet.DisposeAsync();

    private ValueTask<RouteOutletCommitResult> CommitNextAsync()
    {
        var viewModel = new BenchmarkViewModel();
        return _outlet.CommitAsync(RouteOutletCommitPlan.Replace(
            "primary",
            BoundViewHandle.FromExisting(new BenchmarkView(), viewModel)));
    }
}

[MemoryDiagnoser]
public class InteractionDispatchBenchmarks
{
    private InteractionHandlerRegistry _registry = null!;
    private IDisposable _registration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registry = new InteractionHandlerRegistry(InlineUiDispatcher.Instance);
        _registration = _registry.Register<BenchmarkRequest, int>(
            static (context, _) => ValueTask.FromResult(context.Request.Value + 1));
    }

    [Benchmark]
    public ValueTask<AtomUI.City.Mvvm.InteractionResult<int>> DispatchGlobalHandler() =>
        _registry.HandleAsync<BenchmarkRequest, int>(new BenchmarkRequest(41));

    [GlobalCleanup]
    public void Cleanup() => _registration.Dispose();
}

[MemoryDiagnoser]
public class VisualLifecycleBenchmarks
{
    private readonly object _view = new();
    private VisualLifecycleHub _hub = null!;
    private VisualIdentity _identity = null!;
    private IDisposable[] _subscriptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _hub = new VisualLifecycleHub();
        _identity = new VisualIdentity(_view, "main", "primary", 42, "entry-42");
        _subscriptions =
        [
            _hub.Subscribe(static _ => { }, new VisualLifecycleSubscriptionOptions
            {
                WindowId = "main",
                OutletName = "primary",
                OperationId = 42,
                EntryId = "entry-42",
            }),
            _hub.Subscribe(static _ => { }, new VisualLifecycleSubscriptionOptions
            {
                WindowId = "other",
            }),
        ];
    }

    [Benchmark]
    public void NotifyMatchingIdentity() =>
        _hub.Notify(_identity, VisualLifecycleEventKind.Attached);

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }
}

public sealed record BenchmarkRequest(int Value);

public sealed class BenchmarkViewModel;

public class BenchmarkView;

public sealed class OverriddenBenchmarkView : BenchmarkView;

internal sealed class BenchmarkOutletTarget : IRouteOutletTarget
{
    public object? Content { get; private set; }

    public void SetContent(object? content) => Content = content;
}

internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public static InlineUiDispatcher Instance { get; } = new();

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
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return callback(cancellationToken);
    }
}
