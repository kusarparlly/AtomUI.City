using System.Diagnostics;
using System.Text.Json;
using AtomUI.City.EventBus;
using AtomUI.City.Presentation;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodResourceMonitor
{
    private static readonly TimeSpan ForcedGcInterval = TimeSpan.FromMinutes(5);
    private readonly object _syncRoot = new();
    private readonly List<DogfoodResourceSnapshot> _snapshots = [];
    private readonly DogfoodRunOptions _options;
    private readonly IEventBusMonitor _eventBus;
    private readonly IEventChannelMonitor _eventChannels;
    private readonly IPresentationRuntime _presentation;
    private CancellationTokenSource? _samplingCancellation;
    private Task? _samplingTask;
    private DateTimeOffset _startedAt;
    private int _unobservedTaskExceptions;

    public DogfoodResourceMonitor(
        DogfoodRunOptions options,
        IEventBusMonitor eventBus,
        IEventChannelMonitor eventChannels,
        IPresentationRuntime presentation)
    {
        _options = options;
        _eventBus = eventBus;
        _eventChannels = eventChannels;
        _presentation = presentation;
    }

    public async Task BeginMeasuredPhaseAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (_samplingTask is not null)
            {
                return;
            }

            _startedAt = DateTimeOffset.UtcNow;
            _samplingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Capture("baseline", forceFullGc: true);
        lock (_syncRoot)
        {
            _samplingTask = SampleLoopAsync(_samplingCancellation!.Token);
        }

        await Task.Yield();
    }

    public async Task CompleteAsync(bool resourcesReleased)
    {
        CancellationTokenSource? cancellation;
        Task? samplingTask;
        lock (_syncRoot)
        {
            cancellation = _samplingCancellation;
            samplingTask = _samplingTask;
        }

        cancellation?.Cancel();
        if (samplingTask is not null)
        {
            try
            {
                await samplingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
            {
            }
        }

        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Capture("shutdown", forceFullGc: true);
        DogfoodResourceSnapshot[] snapshots;
        lock (_syncRoot)
        {
            snapshots = [.. _snapshots];
        }

        var evaluation = Evaluate(snapshots, resourcesReleased);
        var path = Path.Combine(_options.ArtifactRoot, "resource-snapshots.json");
        Directory.CreateDirectory(_options.ArtifactRoot);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                new DogfoodResourceReport(1, snapshots, evaluation),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                })).ConfigureAwait(false);

        Console.WriteLine(
            $"DESKTOP_DOGFOOD_RESOURCES passed={evaluation.Passed.ToString().ToLowerInvariant()} " +
            $"samples={snapshots.Length} managedSlopeMiBPerHour={evaluation.ManagedHeapSlopeMiBPerHour:F2} " +
            $"path={path}");
        if (!evaluation.Passed && _options.Profile is DogfoodRunProfile.BurnIn or DogfoodRunProfile.Soak)
        {
            throw new InvalidOperationException(
                "Dogfood resource gates failed: " + string.Join("; ", evaluation.Failures));
        }
    }

    private async Task SampleLoopAsync(CancellationToken cancellationToken)
    {
        var lastForcedGc = DateTimeOffset.UtcNow;
        while (true)
        {
            await Task.Delay(_options.SampleInterval, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var forceFullGc = now - lastForcedGc >= ForcedGcInterval;
            Capture(forceFullGc ? "quiescent" : "periodic", forceFullGc);
            if (forceFullGc)
            {
                lastForcedGc = now;
            }
        }
    }

    private void Capture(string phase, bool forceFullGc)
    {
        if (forceFullGc)
        {
            for (var index = 0; index < 3; index++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
        }

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var bus = _eventBus.GetSnapshot();
        var channels = _eventChannels.GetChannelSnapshots();
        var windows = _presentation.Windows.ToArray();
        var outlets = windows.SelectMany(static window => window.Outlets).ToArray();
        var snapshot = new DogfoodResourceSnapshot(
            DateTimeOffset.UtcNow,
            phase,
            forceFullGc,
            DateTimeOffset.UtcNow - _startedAt,
            process.WorkingSet64,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(forceFullCollection: false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            process.Threads.Count,
            OperatingSystem.IsWindows() ? process.HandleCount : 0,
            bus.ActiveSubscriptionCount,
            channels.Sum(static channel => channel.PendingCount),
            channels.Sum(static channel => channel.InFlightCount),
            windows.Length,
            outlets.Length,
            outlets.Sum(static outlet => outlet.QueueSnapshot.PendingCount),
            outlets.Sum(static outlet => outlet.QueueSnapshot.InFlightCount),
            outlets.Sum(static outlet => outlet.QueueSnapshot.RejectedCount),
            Volatile.Read(ref _unobservedTaskExceptions));
        lock (_syncRoot)
        {
            _snapshots.Add(snapshot);
        }
    }

    private DogfoodResourceEvaluation Evaluate(
        IReadOnlyList<DogfoodResourceSnapshot> snapshots,
        bool resourcesReleased)
    {
        if (snapshots.Count < 2)
        {
            return new DogfoodResourceEvaluation(false, 0, ["fewer than two resource snapshots"]);
        }

        var baseline = snapshots[0];
        var final = snapshots[^1];
        var retained = snapshots.Where(static snapshot => snapshot.ForcedFullGc).ToArray();
        var slope = CalculateSlopeMiBPerHour(retained);
        var failures = new List<string>();
        var managedLimit = Math.Max(32L * 1024 * 1024, baseline.ManagedHeapBytes / 5);
        var workingSetLimit = Math.Max(128L * 1024 * 1024, baseline.WorkingSetBytes * 3 / 10);
        if (_options.Profile is DogfoodRunProfile.BurnIn or DogfoodRunProfile.Soak)
        {
            Require(final.ManagedHeapBytes - baseline.ManagedHeapBytes <= managedLimit,
                $"managed heap delta exceeded {managedLimit} bytes", failures);
            Require(final.WorkingSetBytes - baseline.WorkingSetBytes <= workingSetLimit,
                $"working set delta exceeded {workingSetLimit} bytes", failures);
            Require(slope <= 8d, $"retained managed heap slope was {slope:F2} MiB/hour", failures);
            Require(final.ThreadCount - baseline.ThreadCount <= 8, "thread count grew by more than 8", failures);
            if (OperatingSystem.IsWindows())
            {
                Require(final.HandleCount - baseline.HandleCount <= 16, "handle count grew by more than 16", failures);
            }
        }

        Require(final.EventPendingCount == 0 && final.EventInFlightCount == 0,
            "EventBus was not quiescent at shutdown", failures);
        Require(final.PresentationPendingCount == 0 && final.PresentationInFlightCount == 0,
            "Presentation queues were not quiescent at shutdown", failures);
        Require(final.WindowCount == 0 && final.OutletCount == 0,
            "Presentation windows or outlets survived shutdown", failures);
        Require(final.UnobservedTaskExceptionCount == 0,
            "unobserved task exceptions were raised", failures);
        Require(resourcesReleased, "host resources were not marked released", failures);
        return new DogfoodResourceEvaluation(failures.Count == 0, slope, failures);
    }

    private static double CalculateSlopeMiBPerHour(IReadOnlyList<DogfoodResourceSnapshot> snapshots)
    {
        if (snapshots.Count < 2)
        {
            return 0;
        }

        var xMean = snapshots.Average(static snapshot => snapshot.Elapsed.TotalHours);
        var yMean = snapshots.Average(static snapshot => snapshot.ManagedHeapBytes / 1024d / 1024d);
        var numerator = 0d;
        var denominator = 0d;
        foreach (var snapshot in snapshots)
        {
            var x = snapshot.Elapsed.TotalHours - xMean;
            numerator += x * ((snapshot.ManagedHeapBytes / 1024d / 1024d) - yMean);
            denominator += x * x;
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static void Require(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Interlocked.Increment(ref _unobservedTaskExceptions);
        args.SetObserved();
    }
}

internal sealed record DogfoodResourceReport(
    int SchemaVersion,
    IReadOnlyList<DogfoodResourceSnapshot> Snapshots,
    DogfoodResourceEvaluation Evaluation);

internal sealed record DogfoodResourceEvaluation(
    bool Passed,
    double ManagedHeapSlopeMiBPerHour,
    IReadOnlyList<string> Failures);

internal sealed record DogfoodResourceSnapshot(
    DateTimeOffset Timestamp,
    string Phase,
    bool ForcedFullGc,
    TimeSpan Elapsed,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int ThreadCount,
    int HandleCount,
    int EventSubscriptionCount,
    int EventPendingCount,
    int EventInFlightCount,
    int WindowCount,
    int OutletCount,
    int PresentationPendingCount,
    int PresentationInFlightCount,
    long PresentationRejectedCount,
    int UnobservedTaskExceptionCount);
