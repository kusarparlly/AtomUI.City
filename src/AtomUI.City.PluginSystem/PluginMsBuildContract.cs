namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin ms build contract.
/// </summary>
public static class PluginMsBuildContract
{
    /// <summary>
    /// Gets properties.
    /// </summary>
    public static IReadOnlyList<string> Properties { get; } =
    [
        "AtomUICityPlugin",
        "AtomUICityPluginId",
        "AtomUICityPluginVersion",
        "AtomUICityPluginPublisher",
        "AtomUICityPluginDisplayNameKey",
        "AtomUICityPluginDescriptionKey",
        "AtomUICityMinHostVersion",
        "AtomUICityMaxHostVersion",
        "AtomUICityPluginApiVersion",
        "AtomUICityPluginUnloadable",
        "AtomUICityPluginNativeAotCompatible",
        "AtomUICityPluginResourceMode",
        "AtomUICityPluginGenerateManifest",
        "AtomUICityPluginValidateManifest",
        "AtomUICityPackageAsPlugin",
        "AtomUICityPluginDevelopmentMode",
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
    ];

    /// <summary>
    /// Gets targets.
    /// </summary>
    public static IReadOnlyList<string> Targets { get; } =
    [
        "GenerateAtomUICityPluginManifest",
        "GenerateAtomUICityContributionManifests",
        "ValidateAtomUICityPluginManifest",
        "ValidateAtomUICityPluginPackage",
        "PackAtomUICityPlugin",
        "InstallAtomUICityPluginToLocalCache",
        "CleanAtomUICityPluginArtifacts",
    ];

    /// <summary>
    /// Gets package content roots.
    /// </summary>
    public static IReadOnlyList<string> PackageContentRoots { get; } =
    [
        "lib/",
        PluginPackagePaths.ManifestRelativePath,
        "atomui-city/manifests/",
        "atomui-city/locales/",
        "atomui-city/assets/",
        "runtimes/",
    ];

    /// <summary>
    /// Executes the get manifest output path operation.
    /// </summary>
    public static string GetManifestOutputPath(string intermediateOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intermediateOutputPath);

        return Path.Combine(
            Path.GetFullPath(intermediateOutputPath),
            "AtomUI.City",
            "plugin",
            "atomui-city",
            "plugin.json");
    }
}
