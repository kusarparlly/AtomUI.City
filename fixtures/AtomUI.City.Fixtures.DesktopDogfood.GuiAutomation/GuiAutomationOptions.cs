using System.Globalization;

namespace AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation;

internal sealed record GuiAutomationOptions(
    string ApplicationAssembly,
    string ArtifactRoot,
    string GuiReportPath,
    int Seed,
    int CountdownSeconds)
{
    public static GuiAutomationOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var applicationAssembly = GetRequiredValue(args, "--app");
        var artifactRoot = Path.GetFullPath(GetRequiredValue(args, "--artifact-root"));
        var report = GetValue(args, "--gui-report");
        var seedText = GetValue(args, "--seed");
        var countdownText = GetValue(args, "--countdown");
        var seed = seedText is null
            ? 20260911
            : int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed)
                ? parsedSeed
                : throw new ArgumentException($"Invalid GUI automation seed '{seedText}'.", nameof(args));
        var countdown = countdownText is null
            ? 5
            : int.TryParse(countdownText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCountdown) &&
              parsedCountdown is >= 0 and <= 30
                ? parsedCountdown
                : throw new ArgumentException(
                    $"GUI automation countdown must be between 0 and 30 seconds; actual='{countdownText}'.",
                    nameof(args));

        applicationAssembly = Path.GetFullPath(applicationAssembly);
        if (!File.Exists(applicationAssembly))
        {
            throw new FileNotFoundException("DesktopDogfood application assembly was not found.", applicationAssembly);
        }

        return new GuiAutomationOptions(
            applicationAssembly,
            artifactRoot,
            string.IsNullOrWhiteSpace(report)
                ? Path.Combine(artifactRoot, "gui-report.json")
                : Path.GetFullPath(report),
            seed,
            countdown);
    }

    private static string GetRequiredValue(IReadOnlyList<string> args, string name) =>
        GetValue(args, name) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"GUI automation argument '{name}' is required.", nameof(args));

    private static string? GetValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[index][(name.Length + 1)..];
            }

            if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"GUI automation argument '{name}' requires a value.", nameof(args));
            }

            return args[index + 1];
        }

        return null;
    }
}
