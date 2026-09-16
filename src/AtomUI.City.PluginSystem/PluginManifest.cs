namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin manifest.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginManifest</c> type.
    /// </summary>
    public PluginManifest(
        string schemaVersion,
        string pluginId,
        string packageId,
        string version,
        string displayNameKey,
        string? descriptionKey,
        string? publisher,
        string mainAssembly,
        string targetFramework,
        string pluginApiVersion,
        string minHostVersion,
        string? maxHostVersion,
        bool unloadable,
        bool aotCompatible,
        IReadOnlyList<PluginCapabilityDescriptor> capabilities,
        IReadOnlyList<PluginContributionDescriptor> contributions,
        IReadOnlyList<PluginDependencyDescriptor> dependencies,
        IReadOnlyList<PluginModuleDescriptor> modules)
    {
        SchemaVersion = schemaVersion;
        PluginId = pluginId;
        PackageId = packageId;
        Version = version;
        DisplayNameKey = displayNameKey;
        DescriptionKey = descriptionKey;
        Publisher = publisher;
        MainAssembly = mainAssembly;
        TargetFramework = targetFramework;
        PluginApiVersion = pluginApiVersion;
        MinHostVersion = minHostVersion;
        MaxHostVersion = maxHostVersion;
        Unloadable = unloadable;
        AotCompatible = aotCompatible;
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
        Contributions = Array.AsReadOnly(contributions.ToArray());
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        Modules = Array.AsReadOnly(modules.ToArray());
    }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public string SchemaVersion { get; }

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
    /// Gets display name key.
    /// </summary>
    public string DisplayNameKey { get; }

    /// <summary>
    /// Gets description key.
    /// </summary>
    public string? DescriptionKey { get; }

    /// <summary>
    /// Gets publisher.
    /// </summary>
    public string? Publisher { get; }

    /// <summary>
    /// Gets main assembly.
    /// </summary>
    public string MainAssembly { get; }

    /// <summary>
    /// Gets target framework.
    /// </summary>
    public string TargetFramework { get; }

    /// <summary>
    /// Gets plugin api version.
    /// </summary>
    public string PluginApiVersion { get; }

    /// <summary>
    /// Gets min host version.
    /// </summary>
    public string MinHostVersion { get; }

    /// <summary>
    /// Gets max host version.
    /// </summary>
    public string? MaxHostVersion { get; }

    /// <summary>
    /// Gets unloadable.
    /// </summary>
    public bool Unloadable { get; }

    /// <summary>
    /// Gets aot compatible.
    /// </summary>
    public bool AotCompatible { get; }

    /// <summary>
    /// Gets capabilities.
    /// </summary>
    public IReadOnlyList<PluginCapabilityDescriptor> Capabilities { get; }

    /// <summary>
    /// Gets contributions.
    /// </summary>
    public IReadOnlyList<PluginContributionDescriptor> Contributions { get; }

    /// <summary>
    /// Gets dependencies.
    /// </summary>
    public IReadOnlyList<PluginDependencyDescriptor> Dependencies { get; }

    /// <summary>
    /// Gets modules.
    /// </summary>
    public IReadOnlyList<PluginModuleDescriptor> Modules { get; }
}

/// <summary>
/// Represents plugin capability descriptor.
/// </summary>
public sealed record PluginCapabilityDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginCapabilityDescriptor</c> type.
    /// </summary>
    public PluginCapabilityDescriptor(string name, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        Name = name;
        Scope = Array.AsReadOnly(scope.ToArray());
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets scope.
    /// </summary>
    public IReadOnlyList<string> Scope { get; }
}

/// <summary>
/// Represents plugin contribution descriptor.
/// </summary>
public sealed record PluginContributionDescriptor(string Type, string Path, bool Required);

/// <summary>
/// Represents plugin dependency descriptor.
/// </summary>
public sealed record PluginDependencyDescriptor(string PluginId, string? VersionRange);

/// <summary>
/// Represents plugin module descriptor.
/// </summary>
public sealed record PluginModuleDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginModuleDescriptor</c> type.
    /// </summary>
    public PluginModuleDescriptor(
        string name,
        string typeName,
        IReadOnlyList<string>? dependencies = null)
    {
        Name = name;
        TypeName = typeName;
        Dependencies = dependencies is null
            ? null
            : Array.AsReadOnly(dependencies.ToArray());
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets dependencies.
    /// </summary>
    public IReadOnlyList<string>? Dependencies { get; }
}
