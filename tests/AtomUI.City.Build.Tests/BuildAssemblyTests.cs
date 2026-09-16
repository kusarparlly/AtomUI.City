using System.Reflection;
using AtomUI.City.Build;

namespace AtomUI.City.Build.Tests;

public sealed class BuildAssemblyTests
{
    [Fact]
    public void BuildAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.Build");

        Assert.Equal("AtomUI.City.Build", assembly.GetName().Name);
    }

    [Fact]
    public void BuildMsBuildContractContainsDocumentedPropertiesItemsTargetsAndAssets()
    {
        Assert.Contains("AtomUICityGenerateManifests", BuildMsBuildContract.Properties);
        Assert.Contains("AtomUICityValidateManifests", BuildMsBuildContract.Properties);
        Assert.Contains("AtomUICityEnableAnalyzers", BuildMsBuildContract.Properties);
        Assert.Contains("AtomUICitySourceGenerationMode", BuildMsBuildContract.Properties);
        Assert.DoesNotContain("AtomUICityAllowDynamicDiscovery", BuildMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginCapability", BuildMsBuildContract.Items);
        Assert.Contains("AtomUICityLanguagePackage", BuildMsBuildContract.Items);
        Assert.Contains("GenerateAtomUICityManifests", BuildMsBuildContract.Targets);
        Assert.Contains("ValidateAtomUICityManifests", BuildMsBuildContract.Targets);
        Assert.Contains("WriteAtomUICityBuildDiagnostics", BuildMsBuildContract.Targets);
        Assert.Contains("buildTransitive/AtomUI.City.Build.props", BuildMsBuildContract.PackageAssets);
        Assert.Contains("buildTransitive/AtomUI.City.Core.Diagnostics.targets", BuildMsBuildContract.PackageAssets);
        Assert.DoesNotContain("buildTransitive/AtomUI.City.Diagnostics.targets", BuildMsBuildContract.PackageAssets);
        Assert.Contains("analyzers/dotnet/cs/AtomUI.City.Generators.dll", BuildMsBuildContract.PackageAssets);
    }

    [Fact]
    public void BuildMsBuildContractDefinesManifestOutputPath()
    {
        var intermediateOutputPath = Path.Combine(Path.GetTempPath(), "atomui-city", "obj", "..", "obj");
        var outputPath = BuildMsBuildContract.GetManifestOutputPath(intermediateOutputPath, "modules.json");

        Assert.Equal(
            Path.Combine(Path.GetFullPath(intermediateOutputPath), "AtomUI.City", "manifests", "modules.json"),
            outputPath);
    }

    [Theory]
    [InlineData("", "modules.json")]
    [InlineData(" ", "modules.json")]
    [InlineData("obj", "")]
    [InlineData("obj", " ")]
    public void BuildMsBuildContractRejectsInvalidManifestOutputArguments(string intermediateOutputPath, string manifestFileName)
    {
        Assert.Throws<ArgumentException>(() => BuildMsBuildContract.GetManifestOutputPath(intermediateOutputPath, manifestFileName));
    }
}
