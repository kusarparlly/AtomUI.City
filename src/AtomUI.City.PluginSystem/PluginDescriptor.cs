namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin descriptor.
/// </summary>
public sealed class PluginDescriptor
{
    private PluginDescriptor(PluginManifest manifest, string rootPath)
    {
        Manifest = manifest;
        RootPath = rootPath;
    }

    /// <summary>
    /// Gets manifest.
    /// </summary>
    public PluginManifest Manifest { get; }

    /// <summary>
    /// Gets root path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId => Manifest.PluginId;

    /// <summary>
    /// Gets package id.
    /// </summary>
    public string PackageId => Manifest.PackageId;

    /// <summary>
    /// Gets version.
    /// </summary>
    public string Version => Manifest.Version;

    /// <summary>
    /// Gets main assembly path.
    /// </summary>
    public string MainAssemblyPath => PluginPackagePaths.GetMainAssemblyPath(
        RootPath,
        Manifest.TargetFramework,
        Manifest.MainAssembly);

    /// <summary>
    /// Executes the from manifest operation.
    /// </summary>
    public static PluginDescriptor FromManifest(PluginManifest manifest, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return new PluginDescriptor(manifest, rootPath);
    }
}
