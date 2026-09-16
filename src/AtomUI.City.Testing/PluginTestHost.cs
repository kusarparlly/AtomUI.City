namespace AtomUI.City.Testing;

/// <summary>
/// Represents plugin test host.
/// </summary>
public sealed class PluginTestHost : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<string, PluginTestPackage> _packages;
    private readonly Dictionary<string, PluginTestRecord> _records = new(StringComparer.Ordinal);
    private bool _disposed;

    internal PluginTestHost(TestHost host, IReadOnlyList<PluginTestPackage> packages)
    {
        Host = host;
        _packages = packages.ToDictionary(package => package.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets host.
    /// </summary>
    public TestHost Host { get; }

    /// <summary>
    /// Gets records.
    /// </summary>
    public IReadOnlyCollection<PluginTestRecord> Records => Array.AsReadOnly(_records.Values.ToArray());

    /// <summary>
    /// Executes the create builder operation.
    /// </summary>
    public static PluginTestHostBuilder CreateBuilder()
    {
        return new PluginTestHostBuilder();
    }

    /// <summary>
    /// Executes the install async operation.
    /// </summary>
    public ValueTask<PluginTestRecord> InstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var package = GetPackage(pluginId);
        var installPath = Path.Combine(Host.Directory.RootPath, "plugins", "installed", package.Id, package.Version);

        Directory.CreateDirectory(installPath);
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(
            Path.Combine(installPath, "plugin.json"),
            $$"""
            {
              "id": "{{package.Id}}",
              "version": "{{package.Version}}"
            }
            """);

        var record = new PluginTestRecord(package.Id, package.Version, installPath, PluginTestState.Installed);
        _records[pluginId] = record;

        return ValueTask.FromResult(record);
    }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public ValueTask<PluginTestRecord> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var record = GetRecord(pluginId);

        record.State = PluginTestState.Active;

        return ValueTask.FromResult(record);
    }

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    public ValueTask<PluginTestRecord> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var record = GetRecord(pluginId);

        record.State = PluginTestState.Inactive;

        return ValueTask.FromResult(record);
    }

    /// <summary>
    /// Executes the register contribution operation.
    /// </summary>
    public PluginTestRecord RegisterContribution(string pluginId, string contributionId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        var record = GetRecord(pluginId);

        record.AddContribution(contributionId);

        return record;
    }

    /// <summary>
    /// Executes the unload async operation.
    /// </summary>
    public ValueTask<PluginTestRecord> UnloadAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var record = GetRecord(pluginId);

        RevokeContributions(record);
        record.State = PluginTestState.Unloaded;

        return ValueTask.FromResult(record);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnloadRecords();
        Host.Dispose();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnloadRecords();
        await Host.DisposeAsync().ConfigureAwait(false);
    }

    private PluginTestPackage GetPackage(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (!_packages.TryGetValue(pluginId, out var package))
        {
            throw new KeyNotFoundException($"Plugin package '{pluginId}' is not registered.");
        }

        return package;
    }

    private PluginTestRecord GetRecord(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (!_records.TryGetValue(pluginId, out var record))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not installed.");
        }

        return record;
    }

    private void UnloadRecords()
    {
        foreach (var record in _records.Values)
        {
            RevokeContributions(record);

            if (record.State != PluginTestState.Unloaded)
            {
                record.State = PluginTestState.Unloaded;
            }
        }
    }

    private void RevokeContributions(PluginTestRecord record)
    {
        var revokedCount = record.RevokeContributions();

        if (revokedCount == 0)
        {
            return;
        }

        Host.Diagnostics.Add(
            "AUCTEST401",
            $"Plugin '{record.Id}' revoked {revokedCount} contribution owner(s).");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PluginTestHost));
        }
    }
}
