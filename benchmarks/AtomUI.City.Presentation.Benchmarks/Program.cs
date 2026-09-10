using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace AtomUI.City.Presentation.Benchmarks;

internal static class Program
{
    private const double MaximumTimeRegression = 0.15;
    private const double MaximumAllocationRegression = 0.10;

    public static int Main(string[] args)
    {
        try
        {
            var options = GateOptions.Parse(args);
            var rounds = new List<IReadOnlyDictionary<string, BenchmarkMeasurement>>(options.Rounds);

            for (var round = 1; round <= options.Rounds; round++)
            {
                var roundArguments = options.BenchmarkArguments
                    .Concat(["--artifacts", Path.Combine(options.ArtifactsPath, $"round-{round}")])
                    .ToArray();
                var summaries = BenchmarkSwitcher
                    .FromAssembly(typeof(Program).Assembly)
                    .Run(roundArguments)
                    .ToArray();
                rounds.Add(ReadMeasurements(summaries));
            }

            var measurements = AggregateMedian(rounds);
            if (options.Mode == BaselineMode.Verify)
            {
                Console.WriteLine($"PRESENTATION_BENCHMARK_GATE_OK cases={measurements.Count} mode=verify");
                return 0;
            }

            var environment = BenchmarkEnvironment.Capture();
            if (options.Mode == BaselineMode.Capture)
            {
                var baseline = new PresentationBenchmarkBaseline(
                    SchemaVersion: 1,
                    CapturedAtUtc: DateTimeOffset.UtcNow,
                    GitCommit: ReadEnvironment("PRESENTATION_GIT_COMMIT"),
                    CandidateFingerprint: ReadEnvironment("PRESENTATION_CANDIDATE_FINGERPRINT"),
                    Environment: environment,
                    Measurements: measurements.Values.OrderBy(static item => item.Name, StringComparer.Ordinal).ToArray());
                Directory.CreateDirectory(Path.GetDirectoryName(options.BaselinePath)!);
                File.WriteAllText(
                    options.BaselinePath,
                    JsonSerializer.Serialize(baseline, JsonOptions));
                Console.WriteLine(
                    $"PRESENTATION_BENCHMARK_BASELINE_CAPTURED cases={measurements.Count} file={options.BaselinePath}");
                return 0;
            }

            var approved = JsonSerializer.Deserialize<PresentationBenchmarkBaseline>(
                File.ReadAllText(options.BaselinePath),
                JsonOptions) ?? throw new InvalidOperationException("The approved baseline file is invalid.");
            ValidateEnvironment(approved.Environment, environment);
            Compare(approved.Measurements, measurements);
            Console.WriteLine($"PRESENTATION_BENCHMARK_GATE_OK cases={measurements.Count} mode=compare");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"PRESENTATION_BENCHMARK_GATE_FAILED: {exception.Message}");
            return 1;
        }
    }

    private static IReadOnlyDictionary<string, BenchmarkMeasurement> ReadMeasurements(
        IReadOnlyList<Summary> summaries)
    {
        var reports = summaries.SelectMany(static summary => summary.Reports).ToArray();
        if (reports.Length == 0)
        {
            throw new InvalidOperationException("No benchmark cases were executed.");
        }

        var invalid = reports.Where(static report => !report.Success || report.ResultStatistics is null).ToArray();
        if (invalid.Length != 0)
        {
            throw new InvalidOperationException($"{invalid.Length} benchmark cases produced no valid result.");
        }

        return reports.ToDictionary(
            static report => report.BenchmarkCase.DisplayInfo,
            static report => new BenchmarkMeasurement(
                report.BenchmarkCase.DisplayInfo,
                report.ResultStatistics!.Mean,
                report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0L),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, BenchmarkMeasurement> AggregateMedian(
        IReadOnlyList<IReadOnlyDictionary<string, BenchmarkMeasurement>> rounds)
    {
        var names = rounds[0].Keys.Order(StringComparer.Ordinal).ToArray();
        if (rounds.Any(round => !round.Keys.Order(StringComparer.Ordinal).SequenceEqual(names, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Benchmark rounds did not execute the same case set.");
        }

        return names.ToDictionary(
            static name => name,
            name => new BenchmarkMeasurement(
                name,
                Median(rounds.Select(round => round[name].MeanNanoseconds)),
                Median(rounds.Select(round => round[name].AllocatedBytesPerOperation))),
            StringComparer.Ordinal);
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static void ValidateEnvironment(BenchmarkEnvironment approved, BenchmarkEnvironment current)
    {
        if (approved != current)
        {
            throw new InvalidOperationException(
                $"Baseline environment mismatch. Approved={approved}; Current={current}.");
        }
    }

    private static void Compare(
        IReadOnlyList<BenchmarkMeasurement> approved,
        IReadOnlyDictionary<string, BenchmarkMeasurement> current)
    {
        var failures = new List<string>();
        foreach (var baseline in approved)
        {
            if (!current.TryGetValue(baseline.Name, out var measurement))
            {
                failures.Add($"missing case '{baseline.Name}'");
                continue;
            }

            if (measurement.MeanNanoseconds > baseline.MeanNanoseconds * (1 + MaximumTimeRegression))
            {
                failures.Add(
                    $"'{baseline.Name}' time {measurement.MeanNanoseconds:F2}ns exceeds approved {baseline.MeanNanoseconds:F2}ns by more than 15%");
            }

            var allocationLimit = baseline.AllocatedBytesPerOperation == 0
                ? 0
                : baseline.AllocatedBytesPerOperation * (1 + MaximumAllocationRegression);
            if (measurement.AllocatedBytesPerOperation > allocationLimit)
            {
                failures.Add(
                    $"'{baseline.Name}' allocation {measurement.AllocatedBytesPerOperation:F2}B exceeds approved {baseline.AllocatedBytesPerOperation:F2}B by more than 10%");
            }
        }

        var unexpected = current.Keys.Except(approved.Select(static item => item.Name), StringComparer.Ordinal);
        failures.AddRange(unexpected.Select(static name => $"unapproved case '{name}'"));
        if (failures.Count != 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        }
    }

    private static string ReadEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) ?? "<unknown>";

    private static string ReadDotNetSdkVersion()
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        return process is null
            ? "<unknown>"
            : process.StandardOutput.ReadToEnd().Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed record GateOptions(
        BaselineMode Mode,
        string BaselinePath,
        string ArtifactsPath,
        int Rounds,
        IReadOnlyList<string> BenchmarkArguments)
    {
        public static GateOptions Parse(IReadOnlyList<string> args)
        {
            var mode = BaselineMode.Verify;
            var baselinePath = string.Empty;
            var artifactsPath = Path.GetFullPath("output/presentation-benchmark-gate");
            var rounds = 1;
            var benchmarkArguments = new List<string>();

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--baseline-mode":
                        mode = Enum.Parse<BaselineMode>(args[++index], ignoreCase: true);
                        break;
                    case "--baseline-file":
                        baselinePath = Path.GetFullPath(args[++index]);
                        break;
                    case "--gate-artifacts":
                        artifactsPath = Path.GetFullPath(args[++index]);
                        break;
                    case "--rounds":
                        rounds = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    default:
                        benchmarkArguments.Add(args[index]);
                        break;
                }
            }

            if (rounds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds));
            }

            if (mode != BaselineMode.Verify && string.IsNullOrWhiteSpace(baselinePath))
            {
                throw new ArgumentException("Capture and compare modes require --baseline-file.");
            }

            if (mode == BaselineMode.Compare && !File.Exists(baselinePath))
            {
                throw new FileNotFoundException("The approved baseline file does not exist.", baselinePath);
            }

            return new GateOptions(mode, baselinePath, artifactsPath, rounds, benchmarkArguments);
        }
    }

    private sealed record PresentationBenchmarkBaseline(
        int SchemaVersion,
        DateTimeOffset CapturedAtUtc,
        string GitCommit,
        string CandidateFingerprint,
        BenchmarkEnvironment Environment,
        IReadOnlyList<BenchmarkMeasurement> Measurements);

    private sealed record BenchmarkEnvironment(
        string OperatingSystem,
        string ProcessArchitecture,
        string ProcessorIdentifier,
        string DotNetSdk,
        string DotNetRuntime,
        string BenchmarkDotNet)
    {
        public static BenchmarkEnvironment Capture() => new(
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "<unknown>",
            ReadDotNetSdkVersion(),
            RuntimeInformation.FrameworkDescription,
            typeof(BenchmarkSwitcher).Assembly.GetName().Version?.ToString() ?? "<unknown>");
    }

    private sealed record BenchmarkMeasurement(
        string Name,
        double MeanNanoseconds,
        double AllocatedBytesPerOperation);

    private enum BaselineMode
    {
        Verify,
        Capture,
        Compare,
    }
}
