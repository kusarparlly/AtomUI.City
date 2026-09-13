namespace AtomUI.City.Security;

public sealed class AccountRecordSnapshot
{
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

    public SecurityAccountKey AccountKey => Profile.AccountKey;

    public AccountProfileSnapshot Profile { get; }

    public PersistedPermissionSnapshot Permissions { get; }
}
