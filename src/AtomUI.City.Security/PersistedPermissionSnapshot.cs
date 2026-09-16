namespace AtomUI.City.Security;

/// <summary>
/// Represents persisted permission snapshot.
/// </summary>
public sealed class PersistedPermissionSnapshot
{
    /// <summary>
    /// Represents the current schema version value.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <c>PersistedPermissionSnapshot</c> type.
    /// </summary>
    public PersistedPermissionSnapshot(
        SecurityAccountKey accountKey,
        IReadOnlyCollection<string> permissions,
        long revision,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int schemaVersion = CurrentSchemaVersion)
    {
        AccountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException("Permission expiry must be later than its issue time.", nameof(expiresAt));
        }

        var values = permissions
            .Select(static permission =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(permission);
                return permission.Trim();
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static permission => permission, StringComparer.Ordinal)
            .ToArray();

        AccountKey = accountKey;
        Permissions = Array.AsReadOnly(values);
        Revision = revision;
        IssuedAt = issuedAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
        SchemaVersion = schemaVersion;
    }

    /// <summary>
    /// Gets account key.
    /// </summary>
    public SecurityAccountKey AccountKey { get; }

    /// <summary>
    /// Gets permissions.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets a value indicating whether issued at.
    /// </summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>
    /// Gets expires at.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets a value indicating whether is expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now.ToUniversalTime();
}
