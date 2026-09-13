namespace AtomUI.City.Security;

public sealed class AccountCredentialSnapshot
{
    public const int CurrentSchemaVersion = 1;

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

    public SecurityAccountKey AccountKey { get; }

    public string ResourceName { get; }

    public string AccessToken { get; }

    public string? RefreshToken { get; }

    public string Scheme { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public int SchemaVersion { get; }

    public override string ToString() =>
        $"Credential({AccountKey}, resource={ResourceName}, scheme={Scheme}, expiresAt={ExpiresAt:O}, token=<redacted>)";
}
