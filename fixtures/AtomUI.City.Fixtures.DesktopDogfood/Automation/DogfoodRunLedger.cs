using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodRunLedger
{
    private const int TimelineCapacity = 200;

    private readonly ConcurrentDictionary<string, long> _categoryCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _coverage = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<DogfoodScenarioEvidence> _scenarioResults = new();
    private readonly Queue<DogfoodActionRecord> _timeline = new(TimelineCapacity);
    private readonly object _timelineSyncRoot = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private long _actions;
    private long _expectedFailures;
    private string? _failure;

    public DogfoodRunLedger(DogfoodRunOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public DogfoodRunOptions Options { get; }

    public long ActionCount => Interlocked.Read(ref _actions);

    public long ExpectedFailureCount => Interlocked.Read(ref _expectedFailures);

    public IReadOnlyList<DogfoodScenarioEvidence> ScenarioResults => _scenarioResults.ToArray();

    public void Record(string category, string id, string outcome = "completed")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        var sequence = Interlocked.Increment(ref _actions);
        _categoryCounts.AddOrUpdate(category, 1, static (_, current) => current + 1);
        _coverage.TryAdd($"{category}:{id}", 0);
        if (string.Equals(outcome, "expected-failure", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _expectedFailures);
        }

        lock (_timelineSyncRoot)
        {
            if (_timeline.Count == TimelineCapacity)
            {
                _timeline.Dequeue();
            }

            _timeline.Enqueue(new DogfoodActionRecord(sequence, category, id, outcome));
        }
    }

    public void RecordFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Interlocked.CompareExchange(
            ref _failure,
            $"{exception.GetType().FullName}: {exception.Message}",
            comparand: null);
    }

    public void RecordScenario(DogfoodScenarioEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _scenarioResults.Enqueue(evidence);
        Record("scenario", evidence.ScenarioId, evidence.Status);
    }

    public long Count(string category) =>
        _categoryCounts.TryGetValue(category, out var count) ? count : 0;

    public int Covered(string category) =>
        _coverage.Keys.Count(key => key.StartsWith(category + ":", StringComparison.Ordinal));

    public async Task WriteReportAsync(int exitCode, bool resourcesReleased)
    {
        _elapsed.Stop();
        Directory.CreateDirectory(Options.ArtifactRoot);
        var reportDirectory = Path.GetDirectoryName(Options.ReportPath);
        if (!string.IsNullOrEmpty(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }

        DogfoodActionRecord[] timeline;
        lock (_timelineSyncRoot)
        {
            timeline = _timeline.ToArray();
        }

        var report = new DogfoodRunReport(
            SchemaVersion: 2,
            Profile: Options.Profile.ToString(),
            Options.Seed,
            ExitCode: exitCode,
            ElapsedMilliseconds: _elapsed.ElapsedMilliseconds,
            ActionCount,
            ExpectedFailureCount,
            ResourcesReleased: resourcesReleased,
            Failure: _failure,
            CategoryCounts: new SortedDictionary<string, long>(_categoryCounts, StringComparer.Ordinal),
            Coverage: _coverage.Keys.Order(StringComparer.Ordinal).ToArray(),
            ScenarioResults: _scenarioResults
                .OrderBy(static item => item.BatchSequence)
                .ThenBy(static item => item.ScenarioSequence)
                .ToArray(),
            Timeline: timeline);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        if (json.Contains("stress/admin/r10", StringComparison.Ordinal) ||
            json.Contains("stress/alice/r8", StringComparison.Ordinal) ||
            json.Contains("refresh-token", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Dogfood report contains credential material.");
        }

        await File.WriteAllTextAsync(Options.ReportPath, json).ConfigureAwait(false);
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_REPORT profile={Options.Profile.ToString().ToLowerInvariant()} actions={ActionCount} expectedFailures={ExpectedFailureCount} released={resourcesReleased.ToString().ToLowerInvariant()} path={Options.ReportPath}");
    }
}

internal sealed record DogfoodRunReport(
    int SchemaVersion,
    string Profile,
    int Seed,
    int ExitCode,
    long ElapsedMilliseconds,
    long ActionCount,
    long ExpectedFailureCount,
    bool ResourcesReleased,
    string? Failure,
    IReadOnlyDictionary<string, long> CategoryCounts,
    IReadOnlyList<string> Coverage,
    IReadOnlyList<DogfoodScenarioEvidence> ScenarioResults,
    IReadOnlyList<DogfoodActionRecord> Timeline);

internal sealed record DogfoodActionRecord(
    long Sequence,
    string Category,
    string Id,
    string Outcome);
