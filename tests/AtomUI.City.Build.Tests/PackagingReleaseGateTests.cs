using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed class PackagingReleaseGateTests
{
    private const string EngineeringScriptsDirectoryName = "engineering";

    [Fact]
    public void PackageMetadataDefinesReleaseReadyNuGetProperties()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var packageMetadata = XDocument.Load(Path.Combine(repositoryRoot, "build", "PackageMetaInfo.props"));
        var properties = ReadProperties(packageMetadata);

        Assert.Equal("true", properties["GenerateDocumentationFile"]);
        Assert.Equal("true", properties["IncludeSymbols"]);
        Assert.Equal("snupkg", properties["SymbolPackageFormat"]);
        Assert.Equal("true", properties["PublishRepositoryUrl"]);
        Assert.Equal("true", properties["EmbedUntrackedSources"]);
        Assert.Equal("git", properties["RepositoryType"]);
        Assert.Equal("README.nuget.md", properties["PackageReadmeFile"]);
        Assert.Contains("RELEASE_NOTES.md", properties["PackageReleaseNotes"], StringComparison.Ordinal);
    }

    [Fact]
    public void PackageMetadataIncludesReadmeReleaseNotesAndSourceLink()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var packageMetadata = XDocument.Load(Path.Combine(repositoryRoot, "build", "PackageMetaInfo.props"));

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "README.nuget.md")), "Expected NuGet package readme.");
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "RELEASE_NOTES.md")), "Expected release notes seed file.");

        var packedFiles = packageMetadata
            .Descendants("None")
            .Select(item => new
            {
                Include = item.Attribute("Include")?.Value,
                PackagePath = item.Attribute("PackagePath")?.Value,
                Pack = item.Attribute("Pack")?.Value,
            })
            .ToArray();

        Assert.Contains(
            packedFiles,
            item => item.Include?.EndsWith("../README.nuget.md", StringComparison.Ordinal) is true &&
                    item.Pack == "True" &&
                    item.PackagePath == "/");
        Assert.Contains(
            packedFiles,
            item => item.Include?.EndsWith("../RELEASE_NOTES.md", StringComparison.Ordinal) is true &&
                    item.Pack == "True" &&
                    item.PackagePath == "/");

        var sourceLinkReference = packageMetadata
            .Descendants("PackageReference")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.SourceLink.GitHub");

        Assert.Equal("all", sourceLinkReference.Attribute("PrivateAssets")?.Value);
    }

    [Fact]
    public void CentralPackageVersionsDefineSourceLinkVersion()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var centralPackages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var sourceLinkVersion = centralPackages
            .Descendants("PackageVersion")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.SourceLink.GitHub");

        Assert.Equal("10.0.303", sourceLinkVersion.Attribute("Version")?.Value);
    }

    [Fact]
    public void CorePublicApiGateUsesFrozenBaselineAnalyzersAndPackageValidation()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var centralPackages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var coreProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Core", "AtomUI.City.Core.csproj"));
        var coreProperties = ReadProperties(coreProject);
        var shippedApiPath = Path.Combine(repositoryRoot, "src", "AtomUI.City.Core", "PublicAPI.Shipped.txt");
        var unshippedApiPath = Path.Combine(repositoryRoot, "src", "AtomUI.City.Core", "PublicAPI.Unshipped.txt");

        var analyzerVersion = centralPackages
            .Descendants("PackageVersion")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.CodeAnalysis.PublicApiAnalyzers")
            .Attribute("Version")?.Value;
        var analyzerReference = coreProject
            .Descendants("PackageReference")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.CodeAnalysis.PublicApiAnalyzers");

        Assert.False(string.IsNullOrWhiteSpace(analyzerVersion));
        Assert.Equal("all", analyzerReference.Attribute("PrivateAssets")?.Value);
        Assert.Equal("true", coreProperties["EnablePackageValidation"]);
        Assert.Equal("true", coreProperties["EnableStrictModeForCompatibleFrameworksInPackage"]);
        Assert.Contains("CS1591", coreProperties["WarningsAsErrors"], StringComparison.Ordinal);
        Assert.Contains("RS0016", coreProperties["WarningsAsErrors"], StringComparison.Ordinal);
        Assert.Contains("RS0017", coreProperties["WarningsAsErrors"], StringComparison.Ordinal);
        Assert.True(File.Exists(shippedApiPath));
        Assert.True(File.Exists(unshippedApiPath));
        Assert.Contains(
            File.ReadLines(shippedApiPath),
            line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
    }

    [Fact]
    public void PresentationPublicApiGateUsesFrozenBaselineAnalyzersAndPackageValidation()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Presentation",
            "AtomUI.City.Presentation.csproj"));
        var properties = ReadProperties(project);
        var shippedApiPath = Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Presentation",
            "PublicAPI.Shipped.txt");
        var unshippedApiPath = Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Presentation",
            "PublicAPI.Unshipped.txt");
        var analyzerReference = project
            .Descendants("PackageReference")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.CodeAnalysis.PublicApiAnalyzers");

        Assert.Equal("all", analyzerReference.Attribute("PrivateAssets")?.Value);
        Assert.Equal("true", properties["EnablePackageValidation"]);
        Assert.Equal("true", properties["EnableStrictModeForCompatibleFrameworksInPackage"]);
        Assert.Contains("RS0016", properties["WarningsAsErrors"], StringComparison.Ordinal);
        Assert.Contains("RS0017", properties["WarningsAsErrors"], StringComparison.Ordinal);
        Assert.True(File.Exists(shippedApiPath));
        Assert.True(File.Exists(unshippedApiPath));
        Assert.Contains(
            File.ReadLines(shippedApiPath),
            line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
        Assert.DoesNotContain(
            File.ReadLines(unshippedApiPath),
            line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
    }

    [Fact]
    public void GeneratorPackageSuppressesDependenciesWhenPacking()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators", "AtomUI.City.Generators.csproj"));
        var properties = ReadProperties(generatorProject);

        Assert.Equal("false", properties["IncludeBuildOutput"]);
        Assert.Equal("true", properties["SuppressDependenciesWhenPacking"]);
    }

    [Fact]
    public void MainPackageVersionsAreUnified()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var versionProps = XDocument.Load(Path.Combine(repositoryRoot, "build", "Version.props"));
        var properties = ReadProperties(versionProps);

        Assert.Equal("1.0.0", properties["AtomUICityVersion"]);
        Assert.Equal("$(AtomUICityVersion)", properties["AtomUICityTemplatesVersion"]);

        var sourceProjectsWithLiteralVersions = RepositoryPaths
            .EnumerateSourceProjects(repositoryRoot)
            .SelectMany(projectPath => ReadLiteralVersionProperties(repositoryRoot, projectPath))
            .ToArray();

        Assert.Empty(sourceProjectsWithLiteralVersions);
    }

    [Fact]
    public void ReleaseNotesDescribeUnreleasedOneDotZeroCandidate()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var releaseNotes = File.ReadAllText(Path.Combine(repositoryRoot, "RELEASE_NOTES.md"));

        Assert.Contains("## 1.0.0 (unreleased candidate)", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Plugin API compatibility", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Plugin API compatibility starts only when stable `1.0` is published", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("full 1.0 package family is not publishable yet", releaseNotes, StringComparison.Ordinal);
        Assert.DoesNotContain("pre-1.0", releaseNotes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("may change before the first stable release", releaseNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackagingScriptsExistAndUseRepositoryOutputLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var packScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "pack.sh");
        var validatePackagesScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "validate-packages.sh");
        var templateSmokeScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-template-smoke.sh");
        var releaseNotesScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "generate-release-notes.sh");
        var publicApiScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-public-api.sh");
        var localPackageConsumerScriptPath = Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-local-package-consumer.ps1");

        Assert.True(File.Exists(packScriptPath), "Expected package generation script at engineering/pack.sh.");
        Assert.True(File.Exists(validatePackagesScriptPath), "Expected package validation script at engineering/validate-packages.sh.");
        Assert.True(File.Exists(templateSmokeScriptPath), "Expected template smoke script at engineering/check-template-smoke.sh.");
        Assert.True(File.Exists(releaseNotesScriptPath), "Expected release notes script at engineering/generate-release-notes.sh.");
        Assert.True(File.Exists(publicApiScriptPath), "Expected public API review script at engineering/check-public-api.sh.");
        Assert.True(File.Exists(localPackageConsumerScriptPath), "Expected local package consumer gate at engineering/check-local-package-consumer.ps1.");

        var packScript = File.ReadAllText(packScriptPath);
        Assert.Contains("dotnet pack", packScript, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.", packScript, StringComparison.Ordinal);
        Assert.Contains("output/NuGet", packScript, StringComparison.Ordinal);
        Assert.DoesNotContain("tests/", packScript, StringComparison.Ordinal);

        var validatePackagesScript = File.ReadAllText(validatePackagesScriptPath);
        Assert.Contains("output/NuGet", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains(".nupkg", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains(".snupkg", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("LICENSE", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("README.nuget.md", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("RELEASE_NOTES.md", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("analyzers/dotnet/cs", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("templates/atomui-city-app/.template.config/template.json", validatePackagesScript, StringComparison.Ordinal);

        var templateSmokeScript = File.ReadAllText(templateSmokeScriptPath);
        Assert.Contains("atomui city new app", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("NuGet.Config", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("output/NuGet", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("dotnet build", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("dotnet test", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("NUGET_PACKAGES", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("DefaultServiceProviderFactory", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("Host.CreateApplicationBuilder().Build()", templateSmokeScript, StringComparison.Ordinal);
        Assert.Contains("AUCANL0001", templateSmokeScript, StringComparison.Ordinal);

        var releaseNotesScript = File.ReadAllText(releaseNotesScriptPath);
        Assert.Contains("output/release-notes", releaseNotesScript, StringComparison.Ordinal);
        Assert.Contains("New features", releaseNotesScript, StringComparison.Ordinal);
        Assert.Contains("Breaking changes", releaseNotesScript, StringComparison.Ordinal);
        Assert.Contains("Known limitations", releaseNotesScript, StringComparison.Ordinal);
        Assert.Contains("Plugin API compatibility", releaseNotesScript, StringComparison.Ordinal);

        var publicApiScript = File.ReadAllText(publicApiScriptPath);
        Assert.Contains("PublicAPI.Shipped.txt", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("PublicAPI.Unshipped.txt", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("Microsoft.CodeAnalysis.PublicApiAnalyzers", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("dotnet build", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("dotnet pack", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("EnablePackageValidation", publicApiScript, StringComparison.Ordinal);
        Assert.Contains("$assembly_name.xml", publicApiScript, StringComparison.Ordinal);

        var localPackageConsumerScript = File.ReadAllText(localPackageConsumerScriptPath);
        Assert.Contains("-local-gate", localPackageConsumerScript, StringComparison.Ordinal);
        Assert.Contains("NUGET_PACKAGES", localPackageConsumerScript, StringComparison.Ordinal);
        Assert.Contains("must not contain ProjectReference", localPackageConsumerScript, StringComparison.Ordinal);
        Assert.Contains("did not originate from the candidate local feed", localPackageConsumerScript, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorsPackageUsesAnalyzerAssetLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorsProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators", "AtomUI.City.Generators.csproj"));
        var properties = ReadProperties(generatorsProject);

        Assert.Equal("false", properties["IncludeBuildOutput"]);

        var packedItems = generatorsProject
            .Descendants("None")
            .Select(item => new
            {
                Include = item.Attribute("Include")?.Value,
                PackagePath = item.Attribute("PackagePath")?.Value,
                Pack = item.Attribute("Pack")?.Value,
            })
            .ToArray();

        Assert.Contains(
            packedItems,
            item => item.Include == "$(OutputPath)$(AssemblyName).dll" &&
                    item.Pack == "true" &&
                    item.PackagePath == "analyzers/dotnet/cs");
        Assert.Contains(
            packedItems,
            item => item.Include == "$(OutputPath)$(AssemblyName).pdb" &&
                    item.Pack == "true" &&
                    item.PackagePath == "analyzers/dotnet/cs");
    }

    [Fact]
    public void TemplatesPackageIncludesRuntimeApiAndTemplateContent()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var templatesProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Templates", "AtomUI.City.Templates.csproj"));
        var properties = ReadProperties(templatesProject);

        Assert.Equal("Template", properties["PackageType"]);
        Assert.Equal("true", properties["IncludeBuildOutput"]);
        Assert.Equal("false", properties["IncludeSymbols"]);
    }

    [Fact]
    public void ContinuousIntegrationRunsPackagingReleaseGates()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml"));
        var releaseGate = File.ReadAllText(Path.Combine(repositoryRoot, EngineeringScriptsDirectoryName, "check-release.sh"));

        Assert.Contains("bash engineering/check-release.sh --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-public-api.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("bash engineering/pack.sh --configuration \"$configuration\" --no-build", releaseGate, StringComparison.Ordinal);
        Assert.Contains("bash engineering/validate-packages.sh --configuration \"$configuration\"", releaseGate, StringComparison.Ordinal);
        Assert.Contains("export CONFIGURATION=\"$configuration\"", releaseGate, StringComparison.Ordinal);
        Assert.Contains("bash engineering/check-template-smoke.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("bash engineering/generate-release-notes.sh", releaseGate, StringComparison.Ordinal);

        var packIndex = releaseGate.IndexOf("run_gate \"pack\"", StringComparison.Ordinal);
        var testIndex = releaseGate.IndexOf("run_gate \"test\"", StringComparison.Ordinal);
        var templateSmokeIndex = releaseGate.IndexOf("run_gate \"template-smoke\"", StringComparison.Ordinal);

        Assert.True(packIndex >= 0);
        Assert.True(testIndex >= 0);
        Assert.True(templateSmokeIndex >= 0);
        Assert.True(packIndex < testIndex, "Package generation must run before tests that restore generated template projects.");
        Assert.True(packIndex < templateSmokeIndex, "Package generation must run before template smoke.");
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(XDocument document)
    {
        return document
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ReadLiteralVersionProperties(string repositoryRoot, string projectPath)
    {
        var project = XDocument.Load(projectPath);
        var relativePath = RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, projectPath);

        return project
            .Descendants("Version")
            .Select(version => version.Value)
            .Where(value => !value.StartsWith("$(", StringComparison.Ordinal))
            .Select(value => $"{relativePath}: {value}");
    }
}
