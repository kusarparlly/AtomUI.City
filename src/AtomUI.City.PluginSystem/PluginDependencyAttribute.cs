namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PluginDependencyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginDependencyAttribute</c> type.
    /// </summary>
    public PluginDependencyAttribute(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        PluginId = pluginId;
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets or sets version range.
    /// </summary>
    public string? VersionRange { get; set; }
}
