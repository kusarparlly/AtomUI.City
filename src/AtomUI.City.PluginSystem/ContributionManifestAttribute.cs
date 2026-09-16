namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents contribution manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContributionManifestAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>ContributionManifestAttribute</c> type.
    /// </summary>
    public ContributionManifestAttribute(string type, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Type = type;
        Path = path;
    }

    /// <summary>
    /// Gets type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets or sets required.
    /// </summary>
    public bool Required { get; set; }
}
