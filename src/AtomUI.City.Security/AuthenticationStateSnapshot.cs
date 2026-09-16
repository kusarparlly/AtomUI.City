using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Represents authentication state snapshot.
/// </summary>
public sealed class AuthenticationStateSnapshot
{
    private readonly ClaimsPrincipal _principal;

    /// <summary>
    /// Initializes a new instance of the <c>AuthenticationStateSnapshot</c> type.
    /// </summary>
    public AuthenticationStateSnapshot(
        AuthenticationState state,
        ClaimsPrincipal principal,
        long revision,
        string? scheme = null,
        DateTimeOffset? expiresAt = null,
        string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Authentication state must be defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (scheme is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        }

        var isAuthenticated = principal.Identities.Any(static identity => identity.IsAuthenticated);
        if ((state is AuthenticationState.Authenticated
            or AuthenticationState.Refreshing
            or AuthenticationState.Expired) && !isAuthenticated)
        {
            throw new ArgumentException(
                "Authenticated, refreshing, and expired snapshots require an authenticated principal.",
                nameof(principal));
        }

        if ((state is AuthenticationState.Unknown
            or AuthenticationState.Anonymous
            or AuthenticationState.SignedOut
            or AuthenticationState.Failed) && isAuthenticated)
        {
            throw new ArgumentException(
                "Unknown, anonymous, signed-out, and failed snapshots cannot contain an authenticated principal.",
                nameof(principal));
        }

        if (state == AuthenticationState.Failed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
        }
        else if (failureMessage is not null)
        {
            throw new ArgumentException(
                "Only a failed authentication snapshot can contain a failure message.",
                nameof(failureMessage));
        }

        if ((state is AuthenticationState.Unknown
            or AuthenticationState.Anonymous
            or AuthenticationState.SignedOut
            or AuthenticationState.Failed)
            && (scheme is not null || expiresAt is not null))
        {
            throw new ArgumentException(
                "This authentication state cannot contain token scheme or expiry hints.",
                nameof(state));
        }

        State = state;
        _principal = SecurityPrincipalSnapshot.Clone(principal);
        Revision = revision;
        Scheme = scheme;
        ExpiresAt = expiresAt;
        FailureMessage = failureMessage;
    }

    /// <summary>
    /// Gets state.
    /// </summary>
    public AuthenticationState State { get; }

    /// <summary>
    /// Gets principal.
    /// </summary>
    public ClaimsPrincipal Principal => SecurityPrincipalSnapshot.Clone(_principal);

    internal ClaimsPrincipal PrincipalSnapshot => _principal;

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string? Scheme { get; }

    /// <summary>
    /// Gets expires at.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets failure message.
    /// </summary>
    public string? FailureMessage { get; }

}
