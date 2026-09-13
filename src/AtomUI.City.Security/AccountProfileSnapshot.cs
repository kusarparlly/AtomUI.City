namespace AtomUI.City.Security;

public sealed class AccountProfileSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public AccountProfileSnapshot(
        SecurityAccountKey accountKey,
        string displayName,
        Uri? avatarUri = null,
        DateTimeOffset? lastUsedAt = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        AccountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        if (avatarUri is not null && !avatarUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Account avatar URI must be absolute.", nameof(avatarUri));
        }

        DisplayName = displayName.Trim();
        AvatarUri = avatarUri;
        LastUsedAt = (lastUsedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        SchemaVersion = schemaVersion;
    }

    public SecurityAccountKey AccountKey { get; }

    public string DisplayName { get; }

    public Uri? AvatarUri { get; }

    public DateTimeOffset LastUsedAt { get; }

    public int SchemaVersion { get; }
}
