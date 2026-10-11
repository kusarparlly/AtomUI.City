using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed class OutputLayoutTests
{
    [Fact]
    public void BuildArtifactsAreLoadedFromRepositoryOutputDirectory()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var assemblyPath = typeof(OutputLayoutTests).Assembly.Location;
        var relativeAssemblyPath = RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, assemblyPath);

        Assert.Matches(
            "^output/bin/(Debug|Release)/AtomUI\\.City\\.Build\\.Tests/",
            relativeAssemblyPath);
        Assert.EndsWith("/AtomUI.City.Build.Tests.dll", relativeAssemblyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOutputPropsRedirectBinaryIntermediateAndPackageOutputs()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var outputProps = XDocument.Load(Path.Combine(repositoryRoot, "build", "Output.props"));
        var properties = outputProps
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .Where(element => element.Name.LocalName != "BaseIntermediateOutputPath")
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);
        var baseIntermediateOutputPaths = outputProps
            .Descendants("BaseIntermediateOutputPath")
            .Select(element => new
            {
                Condition = element.Attribute("Condition")?.Value,
                Value = element.Value,
            })
            .ToArray();

        Assert.Equal("$(AtomUICityPackagesOutputPath)", properties["PackageOutputPath"]);
        Assert.Equal("$(MSBuildThisFileDirectory)../output/bin/$(Configuration)/$(MSBuildProjectName)", properties["OutputPathWithoutFramework"]);
        Assert.Equal("$(OutputPathWithoutFramework)", properties["OutputPath"]);
        Assert.Contains(
            baseIntermediateOutputPaths,
            property => property.Condition == "'$(AtomUICityIsolatedIntermediateRoot)' == ''" &&
                        property.Value == "$(MSBuildThisFileDirectory)../output/$(MSBuildProjectName)/obj");
        Assert.Contains(
            baseIntermediateOutputPaths,
            property => property.Condition == "'$(AtomUICityIsolatedIntermediateRoot)' != ''" &&
                        property.Value == "$(AtomUICityIsolatedIntermediateRoot)/$(MSBuildProjectName)/obj");
        Assert.Equal("$(BaseIntermediateOutputPath)/$(Configuration)", properties["IntermediateOutputPath"]);
        Assert.Equal("$(MSBuildThisFileDirectory)../output", properties["AtomUICityOutputRoot"]);
        Assert.Equal("$(AtomUICityOutputRoot)/artifacts/$(Configuration)", properties["AtomUICityArtifactsOutputPath"]);
        Assert.Equal("$(AtomUICityOutputRoot)/NuGet/$(Configuration)", properties["AtomUICityPackagesOutputPath"]);
        Assert.Equal("$(AtomUICityOutputRoot)/logs/$(Configuration)", properties["AtomUICityLogsOutputPath"]);
        Assert.Equal("$(AtomUICityOutputRoot)/test-results/$(Configuration)", properties["AtomUICityTestResultsOutputPath"]);
        Assert.Equal("$(AtomUICityTestResultsOutputPath)", properties["VSTestResultsDirectory"]);
    }

    [Fact]
    public void DirectoryBuildPropsImportsOutputLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var directoryBuildProps = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var imports = directoryBuildProps
            .Descendants("Import")
            .Select(import => import.Attribute("Project")?.Value)
            .Where(project => !string.IsNullOrWhiteSpace(project))
            .Select(project => project!)
            .ToArray();

        Assert.Contains("$(MSBuildThisFileDirectory)/build/Output.props", imports);
    }

    [Fact]
    public void DirectoryBuildPropsPreservesConsumerSolutionConfiguration()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var directoryBuildProps = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var value = directoryBuildProps
            .Descendants("ShouldUnsetParentConfigurationAndPlatform")
            .Single()
            .Value;

        Assert.Equal("false", value);
    }

    [Fact]
    public void EngineeringScriptsWriteLogsAndTestResultsUnderRepositoryOutput()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var packScript = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "pack.sh"));
        var testScript = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "test-ci.sh"));

        Assert.Contains("output/logs/$configuration", packScript, StringComparison.Ordinal);
        Assert.Contains("-bl:$log_output/", packScript, StringComparison.Ordinal);
        Assert.Contains("output/test-results/$configuration", testScript, StringComparison.Ordinal);
        Assert.Contains("--results-directory \"$test_results\"", testScript, StringComparison.Ordinal);
        Assert.Contains("--logger \"trx;LogFilePrefix=ci-tests\"", testScript, StringComparison.Ordinal);
    }
}
