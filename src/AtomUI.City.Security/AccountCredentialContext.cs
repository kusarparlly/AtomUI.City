namespace AtomUI.City.Security;

/// <summary>
/// Represents account credential context.
/// </summary>
public sealed class AccountCredentialContext
{
    /// <summary>
    /// Initializes a new instance of the <c>AccountCredentialContext</c> type.
    /// </summary>
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

    /// <summary>
    /// Gets resource name.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string? Scheme { get; }

    /// <summary>
    /// Gets expires at.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets a value indicating whether has credential.
    /// </summary>
    public bool HasCredential { get; }

    /// <summary>
    /// Gets a value indicating whether is expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) =>
        HasCredential && ExpiresAt is not null && ExpiresAt <= now.ToUniversalTime();
}
