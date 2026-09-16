using System.Text.Json;

namespace AtomUI.City.Build.Tests;

public sealed class PublicApiGovernanceTests
{
    private static readonly string[] ProductNames =
    [
        "Build",
        "Cli",
        "Core",
        "Data",
        "EventBus",
        "Generators",
        "Localization",
        "Mvvm",
        "PluginSystem",
        "Presentation",
        "Routing",
        "Security",
        "State",
        "Templates",
        "Testing",
    ];

    [Fact]
    public void EveryProductProjectHasANonEmptyPublicApiBaseline()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        foreach (var productName in ProductNames)
        {
            var projectRoot = Path.Combine(repositoryRoot, "src", $"AtomUI.City.{productName}");
            var shipped = Path.Combine(projectRoot, "PublicAPI.Shipped.txt");
            var unshipped = Path.Combine(projectRoot, "PublicAPI.Unshipped.txt");

            Assert.True(File.Exists(shipped), $"Missing API baseline: {shipped}");
            Assert.True(File.Exists(unshipped), $"Missing API baseline: {unshipped}");
            Assert.True(
                CountSignatures(shipped) + CountSignatures(unshipped) > 0,
                $"Public API baseline is empty for AtomUI.City.{productName}.");
        }
    }

    [Fact]
    public void PreviewVersionAndHistoricalPackageBaselineRulesAreWiredIntoTheGate()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var versionProps = File.ReadAllText(Path.Combine(repositoryRoot, "build", "Version.props"));
        var packageProps = File.ReadAllText(Path.Combine(repositoryRoot, "build", "PackageMetaInfo.props"));
        var gate = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "check-public-api.sh"));

        Assert.Contains("<AtomUICityVersion>1.0.0-preview.1</AtomUICityVersion>", versionProps, StringComparison.Ordinal);
        Assert.Contains("<AtomUICityApiBaselineVersion>", versionProps, StringComparison.Ordinal);
        Assert.Contains("<PackageValidationBaselineVersion", packageProps, StringComparison.Ordinal);
        Assert.Contains("Stable version %s requires AtomUICityApiBaselineVersion", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void DogfoodApiPolicyRequiresMultipleIndependentEvidenceCategories()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var policyPath = Path.Combine(
            repositoryRoot,
            "fixtures",
            "AtomUI.City.Fixtures.DesktopDogfood",
            "Automation",
            "api-coverage.json");
        using var policy = JsonDocument.Parse(File.ReadAllText(policyPath));

        Assert.Equal(2, policy.RootElement.GetProperty("schemaVersion").GetInt32());
        foreach (var module in policy.RootElement.GetProperty("modules").EnumerateArray())
        {
            var categories = module.GetProperty("requiredEvidenceCategories")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray();
            Assert.True(categories.Length >= 2, $"{module.GetProperty("module").GetString()} has insufficient evidence families.");
            Assert.DoesNotContain(categories, static category => string.IsNullOrWhiteSpace(category));
            Assert.Equal(categories.Length, categories.Distinct(StringComparer.Ordinal).Count());
            Assert.False(module.TryGetProperty("evidenceCategory", out _));
        }
    }

    private static int CountSignatures(string path) =>
        File.ReadLines(path).Count(static line =>
            !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
}
