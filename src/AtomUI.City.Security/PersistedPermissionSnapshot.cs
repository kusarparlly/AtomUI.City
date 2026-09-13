namespace AtomUI.City.Security;

public sealed class PersistedPermissionSnapshot
{
    public const int CurrentSchemaVersion = 1;

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

    public SecurityAccountKey AccountKey { get; }

    public IReadOnlyList<string> Permissions { get; }

    public long Revision { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public int SchemaVersion { get; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now.ToUniversalTime();
}
