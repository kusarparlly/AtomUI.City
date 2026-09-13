namespace AtomUI.City.Fixtures.DesktopDogfood;

internal enum DogfoodRunProfile
{
    Manual,
    Gui,
    Quick,
    Standard,
    Soak,
    Extreme,
    Headless,
    Api,
    BurnIn,
    Network,
    Contribution,
}

internal sealed record DogfoodRunOptions(
    DogfoodRunProfile Profile,
    bool KeepOpenOnFailure,
    int Seed,
    string ArtifactRoot,
    string ReportPath,
    TimeSpan MinimumDuration,
    TimeSpan SampleInterval,
    int ContributionCycles)
{
    public bool IsAutomated => Profile is not (DogfoodRunProfile.Manual or DogfoodRunProfile.Gui);

    public bool IsExternallyDriven => Profile == DogfoodRunProfile.Gui;

    public bool UsesHeadlessUi => Profile is
        DogfoodRunProfile.Headless or DogfoodRunProfile.BurnIn or DogfoodRunProfile.Soak;

    public int TargetActionCount => Profile switch
    {
        DogfoodRunProfile.Manual => 24,
        DogfoodRunProfile.Gui => 24,
        DogfoodRunProfile.Quick => 500,
        DogfoodRunProfile.Standard => 10_000,
        DogfoodRunProfile.Soak => 250_000,
        DogfoodRunProfile.Extreme => 100_000,
        DogfoodRunProfile.Headless => 20_000,
        DogfoodRunProfile.Api => 2_000,
        DogfoodRunProfile.BurnIn => 25_000,
        DogfoodRunProfile.Network => 1_000,
        DogfoodRunProfile.Contribution => 5_000,
        _ => throw new ArgumentOutOfRangeException(nameof(Profile)),
    };

    public static DogfoodRunOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var profileText = GetValue(args, "--profile");
        var profile = args.Contains("--smoke", StringComparer.OrdinalIgnoreCase)
            ? DogfoodRunProfile.Quick
            : profileText is null
                ? DogfoodRunProfile.Manual
                : Enum.TryParse<DogfoodRunProfile>(
                    profileText.Replace("-", string.Empty, StringComparison.Ordinal),
                    ignoreCase: true,
                    out var parsed)
                    ? parsed
                    : throw new ArgumentException($"Unknown Dogfood profile '{profileText}'.", nameof(args));

        var seedText = GetValue(args, "--seed");
        var seed = seedText is null
            ? 20260911
            : int.TryParse(seedText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedSeed)
                ? parsedSeed
                : throw new ArgumentException($"Invalid Dogfood seed '{seedText}'.", nameof(args));

        var artifactRoot = GetValue(args, "--artifact-root");
        if (string.IsNullOrWhiteSpace(artifactRoot))
        {
            artifactRoot = Path.Combine(
                Path.GetTempPath(),
                "AtomUI.City",
                "DesktopDogfood",
                $"{profile.ToString().ToLowerInvariant()}-{Environment.ProcessId}");
        }

        artifactRoot = Path.GetFullPath(artifactRoot);
        var reportPath = GetValue(args, "--report");
        reportPath = string.IsNullOrWhiteSpace(reportPath)
            ? Path.Combine(artifactRoot, "run-report.json")
            : Path.GetFullPath(reportPath);

        var minimumDuration = ParseDuration(
            GetValue(args, "--minimum-duration"),
            profile switch
            {
                DogfoodRunProfile.BurnIn => TimeSpan.FromMinutes(15),
                DogfoodRunProfile.Soak => TimeSpan.FromHours(2),
                _ => TimeSpan.Zero,
            },
            "--minimum-duration");
        var sampleInterval = ParseDuration(
            GetValue(args, "--sample-interval"),
            TimeSpan.FromSeconds(30),
            "--sample-interval");
        if (sampleInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Dogfood sample interval must be positive.");
        }

        var contributionCyclesText = GetValue(args, "--contribution-cycles");
        var contributionCycles = contributionCyclesText is null
            ? 100
            : int.TryParse(contributionCyclesText, out var parsedCycles) && parsedCycles > 0
                ? parsedCycles
                : throw new ArgumentException(
                    $"Invalid Dogfood contribution cycle count '{contributionCyclesText}'.",
                    nameof(args));

        return new DogfoodRunOptions(
            profile,
            args.Contains("--keep-open-on-failure", StringComparer.OrdinalIgnoreCase),
            seed,
            artifactRoot,
            reportPath,
            minimumDuration,
            sampleInterval,
            contributionCycles);
    }

    private static TimeSpan ParseDuration(string? text, TimeSpan fallback, string argumentName)
    {
        if (text is null)
        {
            return fallback;
        }

        return TimeSpan.TryParse(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            out var duration) && duration >= TimeSpan.Zero
                ? duration
                : throw new ArgumentException(
                    $"Invalid Dogfood duration '{text}' for '{argumentName}'.");
    }

    private static string? GetValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return argument[(name.Length + 1)..];
            }

            if (string.Equals(argument, name, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count)
                {
                    throw new ArgumentException($"Dogfood argument '{name}' requires a value.", nameof(args));
                }

                return args[index + 1];
            }
        }

        return null;
    }
}
