namespace AtomUI.City.Security;

public sealed class AccountCredentialContext
{
    public AccountCredentialContext(
        string resourceName,
        string? scheme,
        DateTimeOffset? expiresAt,
        bool hasCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        if (scheme is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        }

        if (hasCredential && scheme is null)
        {
            throw new ArgumentException("A stored credential requires an authentication scheme.", nameof(scheme));
        }

        ResourceName = resourceName.Trim();
        Scheme = scheme?.Trim();
        ExpiresAt = expiresAt?.ToUniversalTime();
        HasCredential = hasCredential;
    }

    public string ResourceName { get; }

    public string? Scheme { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public bool HasCredential { get; }

    public bool IsExpired(DateTimeOffset now) =>
        HasCredential && ExpiresAt is not null && ExpiresAt <= now.ToUniversalTime();
}
