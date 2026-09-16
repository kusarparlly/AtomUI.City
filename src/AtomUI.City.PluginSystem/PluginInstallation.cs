namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin installation.
/// </summary>
/// <param name="PluginId">The plugin id value.</param>
/// <param name="PackageId">The package id value.</param>
/// <param name="Version">The version value.</param>
/// <param name="RootPath">The root path value.</param>
/// <param name="ManifestPath">The manifest path value.</param>
public sealed record PluginInstallation(
    string PluginId,
    string PackageId,
    string Version,
    string RootPath,
    string ManifestPath);

/// <summary>
/// Represents plugin install result.
/// </summary>
public sealed class PluginInstallResult
{
    private PluginInstallResult(
        PluginInstallation? installation,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        Installation = installation;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets installation.
    /// </summary>
    public PluginInstallation? Installation { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Diagnostics.Count == 0;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static PluginInstallResult Success(PluginInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);

        return new PluginInstallResult(installation, []);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static PluginInstallResult Failed(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new PluginInstallResult(null, diagnostics);
    }
}
