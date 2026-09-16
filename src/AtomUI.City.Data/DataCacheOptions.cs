namespace AtomUI.City.Data;

/// <summary>
/// Represents data cache options.
/// </summary>
public sealed class DataCacheOptions
{
    private const string DefaultRevision = "default";

    private DataCacheOptions(
        bool isEnabled,
        string requestFingerprint,
        string principalRevision,
        string permissionRevision,
        string? pluginContributionId,
        string clientVersion,
        string policyVersion,
        TimeSpan? timeToLive)
    {
        IsEnabled = isEnabled;
        RequestFingerprint = requestFingerprint;
        PrincipalRevision = principalRevision;
        PermissionRevision = permissionRevision;
        PluginContributionId = pluginContributionId;
        ClientVersion = clientVersion;
        PolicyVersion = policyVersion;
        TimeToLive = timeToLive;
    }

    /// <summary>
    /// Gets disabled.
    /// </summary>
    public static DataCacheOptions Disabled { get; } = new(
        isEnabled: false,
        requestFingerprint: string.Empty,
        principalRevision: DefaultRevision,
        permissionRevision: DefaultRevision,
        pluginContributionId: null,
        clientVersion: DefaultRevision,
        policyVersion: DefaultRevision,
        timeToLive: null);

    /// <summary>
    /// Gets a value indicating whether is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets request fingerprint.
    /// </summary>
    public string RequestFingerprint { get; }

    /// <summary>
    /// Gets principal revision.
    /// </summary>
    public string PrincipalRevision { get; }

    /// <summary>
    /// Gets permission revision.
    /// </summary>
    public string PermissionRevision { get; }

    /// <summary>
    /// Gets plugin contribution id.
    /// </summary>
    public string? PluginContributionId { get; }

    /// <summary>
    /// Gets client version.
    /// </summary>
    public string ClientVersion { get; }

    /// <summary>
    /// Gets policy version.
    /// </summary>
    public string PolicyVersion { get; }

    /// <summary>
    /// Gets time to live.
    /// </summary>
    public TimeSpan? TimeToLive { get; }

    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    public static DataCacheOptions Enabled(
        string requestFingerprint,
        string principalRevision = "anonymous",
        string permissionRevision = DefaultRevision,
        string? pluginContributionId = null,
        string clientVersion = DefaultRevision,
        string policyVersion = DefaultRevision,
        TimeSpan? timeToLive = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionRevision);
        if (pluginContributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginContributionId);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        if (timeToLive is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Cache time-to-live must be greater than zero.");
        }

        return new DataCacheOptions(
            isEnabled: true,
            requestFingerprint,
            principalRevision,
            permissionRevision,
            pluginContributionId,
            clientVersion,
            policyVersion,
            timeToLive);
    }
}
