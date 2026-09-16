namespace AtomUI.City.Testing;

/// <summary>
/// Represents plugin test record.
/// </summary>
public sealed class PluginTestRecord
{
    private readonly List<string> _contributions = [];

    /// <summary>
    /// Initializes a new instance of the <c>PluginTestRecord</c> type.
    /// </summary>
    public PluginTestRecord(string id, string version, string installPath, PluginTestState state)
    {
        Id = id;
        Version = version;
        InstallPath = installPath;
        State = state;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets install path.
    /// </summary>
    public string InstallPath { get; }

    /// <summary>
    /// Gets or sets state.
    /// </summary>
    public PluginTestState State { get; internal set; }

    /// <summary>
    /// Gets contributions.
    /// </summary>
    public IReadOnlyCollection<string> Contributions => Array.AsReadOnly(_contributions.ToArray());

    /// <summary>
    /// Gets or sets revoked contribution count.
    /// </summary>
    public int RevokedContributionCount { get; private set; }

    internal void AddContribution(string contributionId)
    {
        if (!_contributions.Contains(contributionId, StringComparer.Ordinal))
        {
            _contributions.Add(contributionId);
        }
    }

    internal int RevokeContributions()
    {
        var revokedCount = _contributions.Count;

        if (revokedCount == 0)
        {
            return 0;
        }

        _contributions.Clear();
        RevokedContributionCount += revokedCount;

        return revokedCount;
    }
}
