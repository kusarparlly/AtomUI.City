namespace AtomUI.City.Security;

/// <summary>
/// Represents account record snapshot.
/// </summary>
public sealed class AccountRecordSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <c>AccountRecordSnapshot</c> type.
    /// </summary>
    public AccountRecordSnapshot(
        AccountProfileSnapshot profile,
        PersistedPermissionSnapshot permissions)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        if (!profile.AccountKey.Equals(permissions.AccountKey))
        {
            throw new ArgumentException("Account profile and permission snapshot must belong to the same account.");
        }
    }

    /// <summary>
    /// Gets account key.
    /// </summary>
    public SecurityAccountKey AccountKey => Profile.AccountKey;

    /// <summary>
    /// Gets profile.
    /// </summary>
    public AccountProfileSnapshot Profile { get; }

    /// <summary>
    /// Gets permissions.
    /// </summary>
    public PersistedPermissionSnapshot Permissions { get; }
}
