using System.Security.Claims;

namespace AtomUI.City.Security;

public sealed class AccountSessionSnapshot
{
    private readonly ClaimsPrincipal _principal;

    public AccountSessionSnapshot(
        AccountSessionMode mode,
        long revision,
        AccountProfileSnapshot? profile = null,
        PersistedPermissionSnapshot? permissions = null,
        AccountCredentialContext? credential = null,
        ClaimsPrincipal? principal = null)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        if (mode == AccountSessionMode.Anonymous)
        {
            if (profile is not null || permissions is not null || credential is not null ||
                principal?.Identities.Any(static identity => identity.IsAuthenticated) == true)
            {
                throw new ArgumentException("An anonymous account session cannot contain account data.", nameof(mode));
            }

            _principal = SecurityPrincipals.Anonymous;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(permissions);
            ArgumentNullException.ThrowIfNull(credential);
            ArgumentNullException.ThrowIfNull(principal);
            if (!profile.AccountKey.Equals(permissions.AccountKey))
            {
                throw new ArgumentException("Account profile and permissions must belong to the same account.");
            }

            if (!principal.Identities.Any(static identity => identity.IsAuthenticated))
            {
                throw new ArgumentException("An active account session requires an authenticated principal.", nameof(principal));
            }

            _principal = SecurityPrincipalSnapshot.Clone(principal);
        }

        Mode = mode;
        Revision = revision;
        Profile = profile;
        Permissions = permissions;
        Credential = credential;
    }

    public AccountSessionMode Mode { get; }

    public long Revision { get; }

    public AccountProfileSnapshot? Profile { get; }

    public SecurityAccountKey? AccountKey => Profile?.AccountKey;

    public PersistedPermissionSnapshot? Permissions { get; }

    public AccountCredentialContext? Credential { get; }

    public ClaimsPrincipal Principal => SecurityPrincipalSnapshot.Clone(_principal);

    internal ClaimsPrincipal PrincipalSnapshot => _principal;

    public bool IsActive => Mode != AccountSessionMode.Anonymous;

    public static AccountSessionSnapshot Anonymous(long revision = 0) =>
        new(AccountSessionMode.Anonymous, revision);
}
