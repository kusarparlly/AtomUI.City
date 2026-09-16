namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginAttribute</c> type.
    /// </summary>
    public PluginAttribute(string pluginId, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        PluginId = pluginId;
        PackageId = packageId;
        Version = version;
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets package id.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets or sets display name key.
    /// </summary>
    public string? DisplayNameKey { get; set; }

    /// <summary>
    /// Gets or sets description key.
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// Gets or sets publisher.
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Gets or sets main assembly.
    /// </summary>
    public string? MainAssembly { get; set; }

    /// <summary>
    /// Gets or sets target framework.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets plugin api version.
    /// </summary>
    public string? PluginApiVersion { get; set; }

    /// <summary>
    /// Gets or sets min host version.
    /// </summary>
    public string? MinHostVersion { get; set; }

    /// <summary>
    /// Gets or sets max host version.
    /// </summary>
    public string? MaxHostVersion { get; set; }

    /// <summary>
    /// Gets or sets unloadable.
    /// </summary>
    public bool Unloadable { get; set; } = true;

    /// <summary>
    /// Gets or sets aot compatible.
    /// </summary>
    public bool AotCompatible { get; set; }
}
