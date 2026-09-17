using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed class SourceGeneratorProjectStructureTests
{
    [Fact]
    public void GeneratorsProjectUsesDedicatedSourceGeneratorLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorRoot = Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators");

        Assert.True(File.Exists(Path.Combine(generatorRoot, "AtomUI.City.Generators.csproj")));

        AssertDirectoryExists(generatorRoot, "Common");
        AssertDirectoryExists(generatorRoot, "Analyzers");
        AssertDirectoryExists(generatorRoot, "Diagnostics");
        AssertDirectoryExists(generatorRoot, "EventBus");
        AssertDirectoryExists(generatorRoot, "Localization");
        AssertDirectoryExists(generatorRoot, "Manifest");
        AssertDirectoryExists(generatorRoot, "Modularity");
        AssertDirectoryExists(generatorRoot, "PluginSystem");
        AssertDirectoryExists(generatorRoot, "Presentation");
        AssertDirectoryExists(generatorRoot, "Routing");
        AssertDirectoryExists(generatorRoot, "Security");
    }

    [Fact]
    public void GeneratorsProjectHasDedicatedTestProject()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "tests", "AtomUI.City.Generators.Tests", "AtomUI.City.Generators.Tests.csproj")));
    }

    [Fact]
    public void RuntimeProjectsOnlyReferenceGeneratorsAsPrivateCompileTimeAnalyzers()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var runtimeProjects = RepositoryPaths
            .EnumerateSourceProjects(repositoryRoot)
            .Where(path => Path.GetFileNameWithoutExtension(path) is not "AtomUI.City.Build"
                and not "AtomUI.City.Cli"
                and not "AtomUI.City.Generators"
                and not "AtomUI.City.Templates"
                and not "AtomUI.City.Testing");

        foreach (var projectPath in runtimeProjects)
        {
            var project = XDocument.Load(projectPath);
            var generatorReferences = project
                .Descendants("ProjectReference")
                .Where(reference => reference.Attribute("Include")?.Value.EndsWith(
                    "AtomUI.City.Generators.csproj",
                    StringComparison.Ordinal) == true);

            foreach (var reference in generatorReferences)
            {
                Assert.Equal("Analyzer", reference.Attribute("OutputItemType")?.Value);
                Assert.Equal("false", reference.Attribute("ReferenceOutputAssembly")?.Value);
                Assert.Equal("all", reference.Attribute("PrivateAssets")?.Value);
            }
        }
    }

    [Fact]
    public void GeneratorPackageUsesAnalyzerOnlyPackageLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators", "AtomUI.City.Generators.csproj"));
        var validatePackagesScript = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "validate-packages.sh"));

        var properties = generatorProject
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToDictionary(element => element.Name.LocalName, element => element.Value.Trim(), StringComparer.Ordinal);

        Assert.Equal("netstandard2.0", properties["TargetFramework"]);
        Assert.Equal("false", properties["IncludeBuildOutput"]);
        Assert.Equal("true", properties["SuppressDependenciesWhenPacking"]);
        Assert.Contains("analyzers/dotnet/cs/$project_name.dll", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("analyzers/dotnet/cs/$project_name.pdb", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("reject_entry_pattern", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("^lib/", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("Generator package contains runtime lib asset", validatePackagesScript, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectPackagesBuildTransitiveAssetsAndGeneratorAnalyzer()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var buildRoot = Path.Combine(repositoryRoot, "src", "AtomUI.City.Build");
        var buildProject = XDocument.Load(Path.Combine(buildRoot, "AtomUI.City.Build.csproj"));

        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Build.props")));
        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Build.targets")));
        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Build.contract.json")));
        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Application.targets")));
        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Plugin.targets")));
        Assert.True(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Core.Diagnostics.targets")));
        Assert.False(File.Exists(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Diagnostics.targets")));

        var buildProps = XDocument.Load(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Build.props"));
        Assert.Contains(
            buildProps.Descendants("CompilerVisibleProperty"),
            property => property.Attribute("Include")?.Value == "IsTestProject");
        Assert.Empty(buildProps.Descendants("AtomUICityAllowDynamicDiscovery"));

        var buildTargets = XDocument.Load(Path.Combine(buildRoot, "buildTransitive", "AtomUI.City.Build.targets"));
        Assert.Contains(
            buildTargets.Descendants("Import"),
            import => import.Attribute("Project")?.Value ==
                      "$(MSBuildThisFileDirectory)AtomUI.City.Core.Diagnostics.targets");
        Assert.DoesNotContain(
            buildTargets.Descendants("Import"),
            import => import.Attribute("Project")?.Value ==
                      "$(MSBuildThisFileDirectory)AtomUI.City.Diagnostics.targets");

        var packedItems = buildProject
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
            item => item.Include == "buildTransitive/AtomUI.City.Build.props" &&
                    item.PackagePath == "buildTransitive/" &&
                    item.Pack == "true");
        Assert.Contains(
            packedItems,
            item => item.Include == "buildTransitive/AtomUI.City.Build.targets" &&
                    item.PackagePath == "buildTransitive/" &&
                    item.Pack == "true");
        Assert.Contains(
            packedItems,
            item => item.Include == "buildTransitive/AtomUI.City.Build.contract.json" &&
                    item.PackagePath == "buildTransitive/" &&
                    item.Pack == "true");
        Assert.Contains(
            packedItems,
            item => item.Include == "buildTransitive/AtomUI.City.Core.Diagnostics.targets" &&
                    item.PackagePath == "buildTransitive/" &&
                    item.Pack == "true");
        Assert.DoesNotContain(
            packedItems,
            item => item.Include == "buildTransitive/AtomUI.City.Diagnostics.targets");
        Assert.Contains(
            packedItems,
            item => item.Include == "$(AtomUICityGeneratorAnalyzerPath)" &&
                    item.PackagePath == "analyzers/dotnet/cs" &&
                    item.Pack == "true");

        var generatorReference = buildProject
            .Descendants("ProjectReference")
            .Single(reference => reference.Attribute("Include")?.Value == "../AtomUI.City.Generators/AtomUI.City.Generators.csproj");

        Assert.Equal("false", generatorReference.Attribute("ReferenceOutputAssembly")?.Value);
        Assert.Equal("all", generatorReference.Attribute("PrivateAssets")?.Value);
    }

    [Fact]
    public void PackageValidationRequiresBuildTransitiveAssetsAndGeneratorAnalyzer()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var validatePackagesScript = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "validate-packages.sh"));

        Assert.Contains("AtomUI.City.Build)", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Build.props", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Build.targets", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Build.contract.json", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("Build package must remain build-asset-only", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Application.targets", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Plugin.targets", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("buildTransitive/AtomUI.City.Core.Diagnostics.targets", validatePackagesScript, StringComparison.Ordinal);
        Assert.DoesNotContain("buildTransitive/AtomUI.City.Diagnostics.targets", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("analyzers/dotnet/cs/AtomUI.City.Generators.dll", validatePackagesScript, StringComparison.Ordinal);
    }

    private static void AssertDirectoryExists(string root, string relativePath)
    {
        Assert.True(Directory.Exists(Path.Combine(root, relativePath)), $"Expected generator directory '{relativePath}'.");
    }
}
