namespace AtomUI.City.Build;

public static class BuildMsBuildContract
{
    public static IReadOnlyList<string> Properties { get; } =
    [
        "AtomUICityOutputRoot",
        "AtomUICityGenerateManifests",
        "AtomUICityValidateManifests",
        "AtomUICityEnableAnalyzers",
        "AtomUICitySourceGenerationMode",
        "AtomUICityStrictAot",
        "AtomUICityPackagePlugin",
        "AtomUICityPackageApplication",
        "AtomUICityApplicationId",
        "AtomUICityPluginProfile",
        "AtomUICityBuildDiagnosticsLevel",
    ];

    public static IReadOnlyList<string> Items { get; } =
    [
        "AtomUICityPluginCapability",
        "AtomUICityPluginDependency",
        "AtomUICityPluginContract",
        "AtomUICityLanguagePackage",
        "AtomUICityPluginAsset",
        "AtomUICityPluginNativeAsset",
        "AtomUICityContributionManifest",
        "AtomUICityStaticPlugin",
        "AtomUICityResourcePack",
    ];

    public static IReadOnlyList<string> Targets { get; } =
    [
        "GenerateAtomUICityManifests",
        "ValidateAtomUICityManifests",
        "GenerateAtomUICityPluginManifest",
        "GenerateAtomUICityContributionManifests",
        "ValidateAtomUICityPluginManifest",
        "ValidateAtomUICityPluginPackage",
        "PackAtomUICityPlugin",
        "InstallAtomUICityPluginToLocalCache",
        "CleanAtomUICityPluginArtifacts",
        "PublishAtomUICityApplication",
        "ValidateAtomUICityAotCompatibility",
        "WriteAtomUICityBuildDiagnostics",
        "CleanAtomUICityOutput",
    ];

    public static IReadOnlyList<string> PackageAssets { get; } =
    [
        "buildTransitive/AtomUI.City.Build.props",
        "buildTransitive/AtomUI.City.Build.targets",
        "buildTransitive/AtomUI.City.Application.targets",
        "buildTransitive/AtomUI.City.Plugin.targets",
        "buildTransitive/AtomUI.City.Core.Diagnostics.targets",
        "analyzers/dotnet/cs/AtomUI.City.Generators.dll",
        "tools/net10.0/AtomUI.City.Build.Tasks.dll",
    ];

    public static string GetManifestOutputPath(string intermediateOutputPath, string manifestFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intermediateOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);

        return Path.Combine(
            Path.GetFullPath(intermediateOutputPath),
            "AtomUI.City",
            "manifests",
            manifestFileName);
    }
}
