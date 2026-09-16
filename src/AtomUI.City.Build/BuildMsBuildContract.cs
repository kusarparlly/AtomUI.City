namespace AtomUI.City.Build;

/// <summary>
/// Represents build ms build contract.
/// </summary>
public static class BuildMsBuildContract
{
    /// <summary>
    /// Gets properties.
    /// </summary>
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

    /// <summary>
    /// Gets items.
    /// </summary>
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

    /// <summary>
    /// Gets targets.
    /// </summary>
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

    /// <summary>
    /// Gets package assets.
    /// </summary>
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

    /// <summary>
    /// Executes the get manifest output path operation.
    /// </summary>
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
