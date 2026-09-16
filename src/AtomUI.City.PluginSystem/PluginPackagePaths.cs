namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin package paths.
/// </summary>
public static class PluginPackagePaths
{
    /// <summary>
    /// Represents the manifest relative path value.
    /// </summary>
    public const string ManifestRelativePath = "atomui-city/plugin.json";
    /// <summary>
    /// Represents the installed directory name value.
    /// </summary>
    public const string InstalledDirectoryName = "installed";
    /// <summary>
    /// Represents the staging directory name value.
    /// </summary>
    public const string StagingDirectoryName = "staging";
    /// <summary>
    /// Represents the runtime root directory name value.
    /// </summary>
    public const string RuntimeRootDirectoryName = "root";
    /// <summary>
    /// Represents the install record file name value.
    /// </summary>
    public const string InstallRecordFileName = "install.json";

    /// <summary>
    /// Executes the get manifest path operation.
    /// </summary>
    public static string GetManifestPath(string rootPath)
    {
        return CombinePackagePath(rootPath, ManifestRelativePath);
    }

    /// <summary>
    /// Executes the get main assembly path operation.
    /// </summary>
    public static string GetMainAssemblyPath(
        string rootPath,
        string targetFramework,
        string mainAssembly)
    {
        return Path.Combine(rootPath, "lib", targetFramework, mainAssembly);
    }

    /// <summary>
    /// Executes the get installed version path operation.
    /// </summary>
    public static string GetInstalledVersionPath(
        string pluginsRoot,
        string pluginId,
        string version)
    {
        return Path.Combine(pluginsRoot, InstalledDirectoryName, pluginId, version);
    }

    /// <summary>
    /// Executes the get installed root path operation.
    /// </summary>
    public static string GetInstalledRootPath(
        string pluginsRoot,
        string pluginId,
        string version)
    {
        return Path.Combine(
            GetInstalledVersionPath(pluginsRoot, pluginId, version),
            RuntimeRootDirectoryName);
    }

    /// <summary>
    /// Executes the get install record path operation.
    /// </summary>
    public static string GetInstallRecordPath(
        string pluginsRoot,
        string pluginId,
        string version)
    {
        return Path.Combine(
            GetInstalledVersionPath(pluginsRoot, pluginId, version),
            InstallRecordFileName);
    }

    internal static string CombinePackagePath(string rootPath, string relativePath)
    {
        return Path.Combine([rootPath, .. relativePath.Split('/')]);
    }
}
