namespace AtomUI.City.Security;

/// <summary>
/// Represents account profile snapshot.
/// </summary>
public sealed class AccountProfileSnapshot
{
    /// <summary>
    /// Represents the current schema version value.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <c>AccountProfileSnapshot</c> type.
    /// </summary>
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

    /// <summary>
    /// Gets account key.
    /// </summary>
    public SecurityAccountKey AccountKey { get; }

    /// <summary>
    /// Gets display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets avatar uri.
    /// </summary>
    public Uri? AvatarUri { get; }

    /// <summary>
    /// Gets last used at.
    /// </summary>
    public DateTimeOffset LastUsedAt { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public int SchemaVersion { get; }
}
