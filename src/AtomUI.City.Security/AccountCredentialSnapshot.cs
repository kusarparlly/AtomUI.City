namespace AtomUI.City.Security;

/// <summary>
/// Represents account credential snapshot.
/// </summary>
public sealed class AccountCredentialSnapshot
{
    /// <summary>
    /// Represents the current schema version value.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <c>AccountCredentialSnapshot</c> type.
    /// </summary>
    public AccountCredentialSnapshot(
        SecurityAccountKey accountKey,
        string resourceName,
        string accessToken,
        string scheme,
        string? refreshToken = null,
        DateTimeOffset? expiresAt = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        AccountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        if (refreshToken is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        }

        ResourceName = resourceName.Trim();
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Scheme = scheme.Trim();
        ExpiresAt = expiresAt?.ToUniversalTime();
        SchemaVersion = schemaVersion;
    }

    /// <summary>
    /// Gets account key.
    /// </summary>
    public SecurityAccountKey AccountKey { get; }

    /// <summary>
    /// Gets resource name.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets access token.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets refresh token.
    /// </summary>
    public string? RefreshToken { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets expires at.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() =>
        $"Credential({AccountKey}, resource={ResourceName}, scheme={Scheme}, expiresAt={ExpiresAt:O}, token=<redacted>)";
}
