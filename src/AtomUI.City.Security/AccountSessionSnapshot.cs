using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Represents account session snapshot.
/// </summary>
public sealed class AccountSessionSnapshot
{
    private readonly ClaimsPrincipal _principal;

    /// <summary>
    /// Initializes a new instance of the <c>AccountSessionSnapshot</c> type.
    /// </summary>
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

    /// <summary>
    /// Gets mode.
    /// </summary>
    public AccountSessionMode Mode { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets profile.
    /// </summary>
    public AccountProfileSnapshot? Profile { get; }

    /// <summary>
    /// Gets account key.
    /// </summary>
    public SecurityAccountKey? AccountKey => Profile?.AccountKey;

    /// <summary>
    /// Gets permissions.
    /// </summary>
    public PersistedPermissionSnapshot? Permissions { get; }

    /// <summary>
    /// Gets credential.
    /// </summary>
    public AccountCredentialContext? Credential { get; }

    /// <summary>
    /// Gets principal.
    /// </summary>
    public ClaimsPrincipal Principal => SecurityPrincipalSnapshot.Clone(_principal);

    internal ClaimsPrincipal PrincipalSnapshot => _principal;

    /// <summary>
    /// Gets a value indicating whether is active.
    /// </summary>
    public bool IsActive => Mode != AccountSessionMode.Anonymous;

    /// <summary>
    /// Gets anonymous.
    /// </summary>
    public static AccountSessionSnapshot Anonymous(long revision = 0) =>
        new(AccountSessionMode.Anonymous, revision);
}
