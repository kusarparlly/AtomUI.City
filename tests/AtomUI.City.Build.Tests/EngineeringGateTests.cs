using System.Text.RegularExpressions;

namespace AtomUI.City.Build.Tests;

public sealed class EngineeringGateTests
{
    private const string EngineeringScriptsDirectoryName = "engineering";

    [Fact]
    public void EditorConfigDefinesRepositoryFormattingPolicy()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var editorConfigPath = Path.Combine(repositoryRoot, ".editorconfig");

        Assert.True(File.Exists(editorConfigPath), "Expected a repository-level .editorconfig file.");

        var editorConfig = File.ReadAllText(editorConfigPath);

        Assert.Contains("root = true", editorConfig, StringComparison.Ordinal);
        Assert.Contains("indent_style = space", editorConfig, StringComparison.Ordinal);
        Assert.Contains("indent_size = 4", editorConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet_diagnostic.IDE0073", editorConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("file_header_template", editorConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousIntegrationWorkflowRunsRequiredEngineeringGates()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml");

        Assert.True(File.Exists(workflowPath), "Expected GitHub Actions CI workflow at .github/workflows/ci.yml.");

        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("dotnet restore AtomUICity.slnx", workflow, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-release.sh --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("presentation-windows-release", workflow, StringComparison.Ordinal);
        Assert.Contains("check-presentation-release.ps1", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalReleaseGateRunsRequiredEngineeringGates()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-release.sh");

        Assert.True(File.Exists(scriptPath), "Expected local release gate at engineering/check-release.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("release gate failed", script, StringComparison.Ordinal);
        Assert.Contains("dotnet restore AtomUICity.slnx", script, StringComparison.Ordinal);
        Assert.Contains("dotnet format AtomUICity.slnx --verify-no-changes --no-restore", script, StringComparison.Ordinal);
        Assert.Contains("dotnet build AtomUICity.slnx --configuration \"$configuration\" --no-restore", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-docs.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-license.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-project-inventory.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-dependency-boundaries.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-public-api.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-eventbus-package-consumer.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-eventbus-benchmarks.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/test-ci.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/pack.sh --configuration \"$configuration\" --no-build", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/validate-packages.sh --configuration \"$configuration\"", script, StringComparison.Ordinal);
        Assert.Contains("export CONFIGURATION=\"$configuration\"", script, StringComparison.Ordinal);
        Assert.Contains("dotnet restore AtomUICity.slnx -p:Configuration=\"$configuration\"", script, StringComparison.Ordinal);
        Assert.Contains("dotnet build AtomUICity.slnx --configuration \"$configuration\" --no-restore", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-template-smoke.sh", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryUsesDescriptiveEngineeringScriptDirectoryName()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var abbreviatedDirectoryPath = Path.Combine(repositoryRoot, "e" + "ng");
        var engineeringDirectoryPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName);

        Assert.False(Directory.Exists(abbreviatedDirectoryPath), "Use engineering/ instead of the abbreviated engineering/ directory.");
        Assert.True(Directory.Exists(engineeringDirectoryPath), "Expected engineering scripts at engineering/.");
    }

    [Fact]
    public void ContinuousIntegrationTestScriptAppliesTestCategoryPolicy()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "test-ci.sh");

        Assert.True(File.Exists(scriptPath), "Expected CI test script at engineering/test-ci.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("dotnet test AtomUICity.slnx --configuration \"$configuration\" --no-build", script, StringComparison.Ordinal);
        Assert.Contains("Category!=PlatformIntegration", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CentralizedLicenseCheckScriptExists()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-license.sh");

        Assert.True(File.Exists(scriptPath), "Expected centralized license check script at engineering/check-license.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("LICENSE", script, StringComparison.Ordinal);
        Assert.Contains("LGPL-3.0-only", script, StringComparison.Ordinal);
        Assert.Contains("PackageLicenseExpression", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationCheckScriptExists()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-docs.sh");

        Assert.True(File.Exists(scriptPath), "Expected documentation check script at engineering/check-docs.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("odd code fences", script, StringComparison.Ordinal);
        Assert.Contains("missing markdown links", script, StringComparison.Ordinal);
        Assert.Contains("--pcre2", script, StringComparison.Ordinal);
        Assert.Contains("[[:alnum:]_]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackScriptAvoidsNounsetUnsafeOptionalArrayExpansion()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "pack.sh");

        Assert.True(File.Exists(scriptPath), "Expected package script at engineering/pack.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.DoesNotContain("\"${no_build[@]}\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackScriptTreatsWarningsAsErrors()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "pack.sh");

        Assert.True(File.Exists(scriptPath), "Expected package script at engineering/pack.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("-p:TreatWarningsAsErrors=true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackScriptRestoresSelectedConfigurationBeforeNoBuildPack()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "pack.sh");

        Assert.True(File.Exists(scriptPath), "Expected package script at engineering/pack.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("dotnet restore \"$project\" -p:Configuration=\"$configuration\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackScriptRemovesStaleAtomUICityPackagesBeforePacking()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "pack.sh");

        Assert.True(File.Exists(scriptPath), "Expected package script at engineering/pack.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("AtomUI.City.*.nupkg", script, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.*.snupkg", script, StringComparison.Ordinal);
        Assert.Contains("rm -f", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicApiScriptBuildsAndPacksFrozenModulesAgainstTheirApiBaselines()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-public-api.sh");

        Assert.True(File.Exists(scriptPath), "Expected public API check script at engineering/check-public-api.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("PublicAPI.Shipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("PublicAPI.Unshipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", script, StringComparison.Ordinal);
        Assert.Contains("dotnet build", script, StringComparison.Ordinal);
        Assert.Contains("dotnet pack", script, StringComparison.Ordinal);
        Assert.Contains("TreatWarningsAsErrors", script, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.Core", script, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.EventBus", script, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.Presentation", script, StringComparison.Ordinal);
        Assert.Contains("$assembly_name.sourcelink.json", script, StringComparison.Ordinal);
        Assert.Contains("$assembly_name.*.nupkg", script, StringComparison.Ordinal);
        Assert.Contains("validate_build_artifacts", script, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.EventBus/PublicAPI.Shipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.EventBus/PublicAPI.Unshipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.Presentation/PublicAPI.Shipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.Presentation/PublicAPI.Unshipped.txt", script, StringComparison.Ordinal);
        Assert.Contains("%s public API gate passed", script, StringComparison.Ordinal);
        Assert.Contains("https://raw.githubusercontent.com/", script, StringComparison.Ordinal);
        Assert.Contains("git rev-parse HEAD", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EventBusBenchmarkGateRejectsEmptyRunsAndRequiresReports()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-eventbus-benchmarks.sh");
        var programPath = Path.Combine(
            repositoryRoot,
            "benchmarks",
            "AtomUI.City.EventBus.Benchmarks",
            "Program.cs");

        Assert.True(File.Exists(scriptPath), "Expected EventBus benchmark gate script.");
        Assert.True(File.Exists(programPath), "Expected EventBus benchmark executable.");

        var script = File.ReadAllText(scriptPath);
        var program = File.ReadAllText(programPath);
        Assert.Contains("*-report.csv", script, StringComparison.Ordinal);
        Assert.Contains("report_count", script, StringComparison.Ordinal);
        Assert.Contains("reports.Length == 0", program, StringComparison.Ordinal);
        Assert.Contains("report.ResultStatistics is null", program, StringComparison.Ordinal);
        Assert.Contains("EVENTBUS_BENCHMARK_GATE_OK", program, StringComparison.Ordinal);
    }

    [Fact]
    public void PresentationBenchmarkGateCapturesAndComparesApprovedWindowsBaselines()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-presentation-benchmarks.ps1");
        var programPath = Path.Combine(
            repositoryRoot,
            "benchmarks",
            "AtomUI.City.Presentation.Benchmarks",
            "Program.cs");

        Assert.True(File.Exists(scriptPath));
        Assert.True(File.Exists(programPath));
        var script = File.ReadAllText(scriptPath);
        var program = File.ReadAllText(programPath);

        Assert.Contains("ValidateSet(\"Verify\", \"Capture\", \"Compare\")", script, StringComparison.Ordinal);
        Assert.Contains("PRESENTATION_CANDIDATE_FINGERPRINT", script, StringComparison.Ordinal);
        Assert.Contains("baselines/windows-x64.json", script, StringComparison.Ordinal);
        Assert.Contains("MaximumTimeRegression = 0.15", program, StringComparison.Ordinal);
        Assert.Contains("MaximumAllocationRegression = 0.10", program, StringComparison.Ordinal);
        Assert.Contains("AggregateMedian", program, StringComparison.Ordinal);
        Assert.Contains("ValidateEnvironment", program, StringComparison.Ordinal);
        Assert.Contains("PRESENTATION_BENCHMARK_GATE_OK", program, StringComparison.Ordinal);
    }

    [Fact]
    public void PresentationPackageConsumerAndWindowsReleaseCandidateGatesAreComplete()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var consumerScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            EngineeringScriptsDirectoryName,
            "check-presentation-package-consumer.ps1"));
        var releaseScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            EngineeringScriptsDirectoryName,
            "check-presentation-release.ps1"));
        var templateRoot = Path.Combine(
            repositoryRoot,
            EngineeringScriptsDirectoryName,
            "package-consumers",
            "presentation");
        var project = File.ReadAllText(Path.Combine(templateRoot, "Presentation.PackageConsumer.csproj.template"));
        var program = File.ReadAllText(Path.Combine(templateRoot, "Program.cs.template"));

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.Presentation", project, StringComparison.Ordinal);
        Assert.Contains("PRESENTATION_PACKAGE_CONSUMER_OK", program, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.Localization", consumerScript, StringComparison.Ordinal);
        Assert.Contains("Presentation package contains a forbidden", consumerScript, StringComparison.Ordinal);
        Assert.Contains("--no-http-cache", consumerScript, StringComparison.Ordinal);
        Assert.Contains("Presentation unit, Headless, stress and desktop tests", releaseScript, StringComparison.Ordinal);
        Assert.Contains("check-presentation-public-api.ps1", releaseScript, StringComparison.Ordinal);
        Assert.Contains("check-presentation-package-consumer.ps1", releaseScript, StringComparison.Ordinal);
        Assert.Contains("check-presentation-benchmarks.ps1", releaseScript, StringComparison.Ordinal);
        Assert.Contains("PRESENTATION_RC_IDENTITY", releaseScript, StringComparison.Ordinal);
    }

    [Fact]
    public void EventBusPackageConsumerGateUsesLocalCityFeedAndAnIsolatedPackageCache()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-eventbus-package-consumer.sh");
        var templateRoot = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "package-consumers", "eventbus");

        Assert.True(File.Exists(scriptPath), "Expected EventBus package consumer gate script.");
        Assert.True(File.Exists(Path.Combine(templateRoot, "EventBus.PackageConsumer.csproj.template")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "NuGet.Config.template")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "Program.cs.template")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "Directory.Build.props.template")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "Directory.Build.targets.template")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "Directory.Packages.props.template")));

        var script = File.ReadAllText(scriptPath);
        var nugetConfig = File.ReadAllText(Path.Combine(templateRoot, "NuGet.Config.template"));
        var project = File.ReadAllText(Path.Combine(templateRoot, "EventBus.PackageConsumer.csproj.template"));
        var program = File.ReadAllText(Path.Combine(templateRoot, "Program.cs.template"));

        Assert.Contains("repository_root=\"$(pwd -P)\"", script, StringComparison.Ordinal);
        Assert.Contains("$repository_root/output/eventbus-package-consumer", script, StringComparison.Ordinal);
        Assert.Contains("local_feed=\"$validation_root/local-feed\"", script, StringComparison.Ordinal);
        Assert.Contains("NUGET_PACKAGES", script, StringComparison.Ordinal);
        Assert.Contains("DOTNET_CLI_HOME", script, StringComparison.Ordinal);
        Assert.Contains("Refusing to clean unexpected consumer paths", script, StringComparison.Ordinal);
        Assert.Contains("find \"$packages\" -mindepth 1 -maxdepth 1 -exec rm -rf {} +", script, StringComparison.Ordinal);
        Assert.Contains("--no-http-cache", script, StringComparison.Ordinal);
        Assert.Contains("project.assets.json", script, StringComparison.Ordinal);
        Assert.Contains("--runtime win-x64", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("EventBus.PackageConsumer.exe", script, StringComparison.Ordinal);
        Assert.Contains("EVENTBUS_PACKAGE_CONSUMER_OK", script, StringComparison.Ordinal);
        Assert.Contains("<clear />", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("<package pattern=\"AtomUI.City.*\" />", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("../local-feed", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("Directory.Packages.props", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"AtomUI.City.Build\"", project, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"AtomUI.City.Core\"", project, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"AtomUI.City.EventBus\"", project, StringComparison.Ordinal);
        Assert.Contains("UseModule<ConsumerModule>", program, StringComparison.Ordinal);
        Assert.Contains("PublishAsync", program, StringComparison.Ordinal);
        Assert.Contains("PostAsync", program, StringComparison.Ordinal);
        Assert.Contains("StopAsync", program, StringComparison.Ordinal);
    }

    [Fact]
    public void EventBusReleaseCandidateGateIsReleaseOnlyAndScopeBounded()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-eventbus-release.sh");

        Assert.True(File.Exists(scriptPath), "Expected EventBus release candidate gate script.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("configuration=\"Release\"", script, StringComparison.Ordinal);
        Assert.Contains("--verify-no-changes", script, StringComparison.Ordinal);
        Assert.Contains("--include \"${candidate_code_paths[@]}\"", script, StringComparison.Ordinal);
        Assert.Contains("EventBus stress iteration %s/20", script, StringComparison.Ordinal);
        Assert.Contains("timeout 300s dotnet test", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-public-api.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-eventbus-package-consumer.sh", script, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-eventbus-benchmarks.sh", script, StringComparison.Ordinal);
        Assert.Contains("EVENTBUS_RC_IDENTITY", script, StringComparison.Ordinal);
        Assert.Contains("git rev-parse HEAD", script, StringComparison.Ordinal);
        Assert.Contains("sha256sum", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageMetadataUsesCanonicalUpstreamProjectAndRepositoryUrls()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var packageMetadataPath = Path.Combine(repositoryRoot, "build", "PackageMetaInfo.props");
        var packageMetadata = File.ReadAllText(packageMetadataPath);

        Assert.Contains(
            "<ProjectUrl>https://github.com/AtomUI/AtomUI.City</ProjectUrl>",
            packageMetadata,
            StringComparison.Ordinal);
        Assert.Contains(
            "<RepositoryUrl>https://github.com/AtomUI/AtomUI.City</RepositoryUrl>",
            packageMetadata,
            StringComparison.Ordinal);
        Assert.Contains("<PublishRepositoryUrl>true</PublishRepositoryUrl>", packageMetadata, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreFeatureStatusesStaySynchronized()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var features = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "modules", "core", "features.md"));
        var testing = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "modules", "core", "testing.md"));
        var expectedIds = Enumerable.Range(1, 8)
            .Select(number => $"AUC-CORE-{number:D3}")
            .ToArray();

        var indexStatuses = ReadStatuses(
            features,
            "^\\| (?<id>AUC-CORE-\\d{3}) \\| [^|]+ \\| (?<status>[^ |]+) \\|");
        var detailStatuses = ReadStatuses(
            features,
            "^Feature ID: `(?<id>AUC-CORE-\\d{3})`\\r?\\nStatus: (?<status>\\S+)$");
        var testingStatuses = ReadStatuses(
            testing,
            "^\\| (?<id>AUC-CORE-\\d{3}) \\|.*\\| (?<status>[^ |]+) \\|$");

        Assert.Equal(expectedIds, indexStatuses.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expectedIds, detailStatuses.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expectedIds, testingStatuses.Keys.Order(StringComparer.Ordinal));

        foreach (var featureId in expectedIds)
        {
            Assert.Equal(indexStatuses[featureId], detailStatuses[featureId]);
            Assert.Equal(indexStatuses[featureId], testingStatuses[featureId]);
        }
    }

    [Fact]
    public void EventBusFeatureStatusesStaySynchronized()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var features = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "modules", "eventbus", "features.md"));
        var testing = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "modules", "eventbus", "testing.md"));
        var expectedIds = Enumerable.Range(1, 9)
            .Select(number => $"AUC-EVENTBUS-{number:D3}")
            .ToArray();

        var indexStatuses = ReadStatuses(
            features,
            "^\\| (?<id>AUC-EVENTBUS-\\d{3}) \\| [^|]+ \\| [^|]+ \\| (?<status>[^|]+?) \\|");
        var detailStatuses = ReadStatuses(
            features,
            "^Feature ID: `(?<id>AUC-EVENTBUS-\\d{3})`\\r?\\nStatus: (?<status>.+)$");
        var testingStatuses = ReadStatuses(
            testing,
            "^\\| (?<id>AUC-EVENTBUS-\\d{3}) \\|.*\\| (?<status>[^|]+?) \\|$");

        Assert.Equal(expectedIds, indexStatuses.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expectedIds, detailStatuses.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expectedIds, testingStatuses.Keys.Order(StringComparer.Ordinal));

        foreach (var featureId in expectedIds)
        {
            Assert.Equal(indexStatuses[featureId], detailStatuses[featureId]);
            Assert.Equal(indexStatuses[featureId], testingStatuses[featureId]);
        }
    }

    [Fact]
    public void CSharpSourceFilesDoNotUseRepositoryLicenseHeaders()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var csharpFiles = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(csharpFiles);

        var filesWithRepositoryLicenseHeaders = csharpFiles
            .Where(path => File.ReadLines(path).FirstOrDefault()?.StartsWith("// Licensed under the GNU Lesser General Public License", StringComparison.Ordinal) is true)
            .Select(path => RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, path))
            .ToArray();

        Assert.Empty(filesWithRepositoryLicenseHeaders);
    }

    [Fact]
    public void MsBuildIncludePathsUseForwardSlashSeparators()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var msbuildFiles = Directory
            .EnumerateFiles(repositoryRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}output{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.artifacts{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.referenceprojects{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path =>
                path.EndsWith(".csproj", StringComparison.Ordinal) ||
                path.EndsWith(".props", StringComparison.Ordinal) ||
                path.EndsWith(".targets", StringComparison.Ordinal) ||
                path.EndsWith(".slnx", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var includePathsWithBackslashes = msbuildFiles
            .SelectMany(path => FindIncludePathsWithBackslashes(repositoryRoot, path))
            .ToArray();

        Assert.Empty(includePathsWithBackslashes);
    }

    private static IEnumerable<string> FindIncludePathsWithBackslashes(string repositoryRoot, string path)
    {
        var content = File.ReadAllText(path);
        var relativePath = RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, path);

        foreach (Match match in Regex.Matches(content, "Include=\"(?<value>[^\"]*\\\\[^\"]*)\"", RegexOptions.CultureInvariant))
        {
            yield return $"{relativePath}: {match.Groups["value"].Value}";
        }
    }

    private static IReadOnlyDictionary<string, string> ReadStatuses(string content, string pattern)
    {
        return Regex.Matches(
                content,
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.Multiline)
            .ToDictionary(
                match => match.Groups["id"].Value,
                match => match.Groups["status"].Value,
                StringComparer.Ordinal);
    }
}
