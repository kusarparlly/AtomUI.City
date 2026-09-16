namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin diagnostic ids.
/// </summary>
public static class PluginDiagnosticIds
{
    /// <summary>
    /// Represents the manifest not found value.
    /// </summary>
    public const string ManifestNotFound = "AUCPLG0000";
    /// <summary>
    /// Represents the missing plugin id value.
    /// </summary>
    public const string MissingPluginId = "AUCPLG0001";
    /// <summary>
    /// Represents the main assembly not found value.
    /// </summary>
    public const string MainAssemblyNotFound = "AUCPLG0002";
    /// <summary>
    /// Represents the plugin id mismatch value.
    /// </summary>
    public const string PluginIdMismatch = "AUCPLG0003";
    /// <summary>
    /// Represents the unsupported manifest schema value.
    /// </summary>
    public const string UnsupportedManifestSchema = "AUCPLG0004";
    /// <summary>
    /// Represents the required contribution manifest not found value.
    /// </summary>
    public const string RequiredContributionManifestNotFound = "AUCPLG0005";
    /// <summary>
    /// Represents the invalid main assembly value.
    /// </summary>
    public const string InvalidMainAssembly = "AUCPLG0006";
    /// <summary>
    /// Represents the plugin already installed value.
    /// </summary>
    public const string PluginAlreadyInstalled = "AUCPLG0007";
    /// <summary>
    /// Represents the plugin dependency missing value.
    /// </summary>
    public const string PluginDependencyMissing = "AUCPLG0008";
    /// <summary>
    /// Represents the plugin dependency cycle value.
    /// </summary>
    public const string PluginDependencyCycle = "AUCPLG0009";
    /// <summary>
    /// Represents the invalid plugin id value.
    /// </summary>
    public const string InvalidPluginId = "AUCPLG0010";
    /// <summary>
    /// Represents the invalid plugin version value.
    /// </summary>
    public const string InvalidPluginVersion = "AUCPLG0011";
    /// <summary>
    /// Represents the invalid contribution path value.
    /// </summary>
    public const string InvalidContributionPath = "AUCPLG0012";
    /// <summary>
    /// Represents the invalid target framework value.
    /// </summary>
    public const string InvalidTargetFramework = "AUCPLG0013";
    /// <summary>
    /// Represents the package extraction failed value.
    /// </summary>
    public const string PackageExtractionFailed = "AUCPLG0014";
    /// <summary>
    /// Represents the plugin id conflict value.
    /// </summary>
    public const string PluginIdConflict = "AUCPLG0015";
    /// <summary>
    /// Represents the plugin dependency version mismatch value.
    /// </summary>
    public const string PluginDependencyVersionMismatch = "AUCPLG0016";
    /// <summary>
    /// Represents the invalid install record value.
    /// </summary>
    public const string InvalidInstallRecord = "AUCPLG0017";
    /// <summary>
    /// Represents the plugin version mismatch value.
    /// </summary>
    public const string PluginVersionMismatch = "AUCPLG0018";
    /// <summary>
    /// Represents the invalid manifest value.
    /// </summary>
    public const string InvalidManifest = "AUCPLG0019";
    /// <summary>
    /// Represents the plugin package id mismatch value.
    /// </summary>
    public const string PluginPackageIdMismatch = "AUCPLG0020";
    /// <summary>
    /// Represents the missing install record value.
    /// </summary>
    public const string MissingInstallRecord = "AUCPLG0021";
    /// <summary>
    /// Represents the invalid plugin directory value.
    /// </summary>
    public const string InvalidPluginDirectory = "AUCPLG0022";
    /// <summary>
    /// Represents the plugin unload pending value.
    /// </summary>
    public const string PluginUnloadPending = "AUCPLG0023";

    /// <summary>
    /// Gets all.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        ManifestNotFound,
        MissingPluginId,
        MainAssemblyNotFound,
        PluginIdMismatch,
        UnsupportedManifestSchema,
        RequiredContributionManifestNotFound,
        InvalidMainAssembly,
        PluginAlreadyInstalled,
        PluginDependencyMissing,
        PluginDependencyCycle,
        InvalidPluginId,
        InvalidPluginVersion,
        InvalidContributionPath,
        InvalidTargetFramework,
        PackageExtractionFailed,
        PluginIdConflict,
        PluginDependencyVersionMismatch,
        InvalidInstallRecord,
        PluginVersionMismatch,
        InvalidManifest,
        PluginPackageIdMismatch,
        MissingInstallRecord,
        InvalidPluginDirectory,
        PluginUnloadPending,
    ]);
}
