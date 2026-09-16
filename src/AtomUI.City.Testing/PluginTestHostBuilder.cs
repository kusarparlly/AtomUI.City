namespace AtomUI.City.Testing;

/// <summary>
/// Represents plugin test host builder.
/// </summary>
public sealed class PluginTestHostBuilder
{
    private readonly List<PluginTestPackage> _packages = [];
    private readonly TestHostBuilder _hostBuilder = TestHost.CreateBuilder();
    private bool _built;

    /// <summary>
    /// Executes the use plugin operation.
    /// </summary>
    public PluginTestHostBuilder UsePlugin(string id, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ThrowIfBuilt();

        _packages.Add(new PluginTestPackage(id, version));

        return this;
    }

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    public PluginTestHost Build()
    {
        ThrowIfBuilt();
        ThrowIfDuplicatePluginIds();

        var host = _hostBuilder.UseDirectoryName("plugin-host").Build();
        _built = true;

        return new PluginTestHost(host, _packages.ToArray());
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The plugin test host builder has already built a host and is frozen.");
        }
    }

    private void ThrowIfDuplicatePluginIds()
    {
        var duplicateId = _packages
            .GroupBy(package => package.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Duplicate plugin id '{duplicateId.Key}'.");
        }
    }
}
