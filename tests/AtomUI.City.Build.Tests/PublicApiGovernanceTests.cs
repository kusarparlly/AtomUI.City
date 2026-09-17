using System.Text.Json;
using System.Xml.Linq;

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
    public void EveryProductProjectHasTheApplicableApiBaseline()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        foreach (var productName in ProductNames)
        {
            var projectRoot = Path.Combine(repositoryRoot, "src", $"AtomUI.City.{productName}");
            var shipped = Path.Combine(projectRoot, "PublicAPI.Shipped.txt");
            var unshipped = Path.Combine(projectRoot, "PublicAPI.Unshipped.txt");

            Assert.True(File.Exists(shipped), $"Missing API baseline: {shipped}");
            Assert.True(File.Exists(unshipped), $"Missing API baseline: {unshipped}");
            var signatureCount = CountSignatures(shipped) + CountSignatures(unshipped);
            if (productName is "Build")
            {
                Assert.Equal(0, signatureCount);
            }
            else
            {
                Assert.True(signatureCount > 0, $"Public API baseline is empty for AtomUI.City.{productName}.");
            }
        }
    }

    [Fact]
    public void EveryPublicAssemblyProjectRequiresCompleteXmlDocumentation()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var commonPropsPath = Path.Combine(repositoryRoot, "build", "Common.props");
        var commonProps = XDocument.Load(commonPropsPath);
        var commonPropsText = File.ReadAllText(commonPropsPath);
        var gate = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "check-public-api.sh"));

        var unconditionalNoWarn = commonProps
            .Descendants("NoWarn")
            .Where(static property => property.Attribute("Condition") is null)
            .Select(static property => property.Value);
        Assert.DoesNotContain(
            unconditionalNoWarn,
            value => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Contains("CS1591", StringComparer.Ordinal));

        Assert.Contains("<AtomUICityPublicApiDocumentationRequired Condition=", commonPropsText, StringComparison.Ordinal);
        Assert.Contains("$(WarningsAsErrors);CS1591", commonPropsText, StringComparison.Ordinal);
        foreach (var productName in ProductNames.Where(static name => name is not "Build"))
        {
            Assert.Contains($"'$(MSBuildProjectName)' == 'AtomUI.City.{productName}'", commonPropsText, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("'$(MSBuildProjectName)' == 'AtomUI.City.Build'", commonPropsText, StringComparison.Ordinal);

        Assert.Contains("for product_name in \"${product_names[@]}\"", gate, StringComparison.Ordinal);
        Assert.Contains("validate_build_artifacts", gate, StringComparison.Ordinal);
        Assert.Contains("asset-only package contains a forbidden runtime lib asset", gate, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Build.contract.json", gate, StringComparison.Ordinal);
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
