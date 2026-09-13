using System.Security.Claims;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class AuthenticationStateStore :
    IAuthenticationStateProvider,
    ICurrentPrincipalAccessor
{
    private readonly object _syncRoot = new();
    private readonly OrderedEventPublisher<AuthenticationStateChangedEventArgs> _eventPublisher;
    private readonly IHostDiagnostics? _diagnostics;
    private AuthenticationStateSnapshot _current = new(
        AuthenticationState.Unknown,
        SecurityPrincipals.Anonymous,
        revision: 0);

    public AuthenticationStateStore()
    {
        _eventPublisher = new OrderedEventPublisher<AuthenticationStateChangedEventArgs>(
            diagnostics: null,
            SecurityDiagnosticIds.AuthenticationObserverFailed);
    }

    public AuthenticationStateStore(IHostDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _eventPublisher = new OrderedEventPublisher<AuthenticationStateChangedEventArgs>(
            diagnostics,
            SecurityDiagnosticIds.AuthenticationObserverFailed);
    }

    public event EventHandler<AuthenticationStateChangedEventArgs>? StateChanged;

    public AuthenticationStateSnapshot Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public ClaimsPrincipal Principal => Current.Principal;

    public AuthenticationStateSnapshot SetAnonymous()
    {
        return SetCore(AuthenticationState.Anonymous, SecurityPrincipals.Anonymous);
    }

    public AuthenticationStateSnapshot SetAuthenticating(ClaimsPrincipal? principal = null, string? scheme = null)
    {
        return SetCore(AuthenticationState.Authenticating, principal ?? SecurityPrincipals.Anonymous, scheme);
    }

    public AuthenticationStateSnapshot SetAuthenticated(
        ClaimsPrincipal principal,
        string? scheme = null,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ThrowIfNotAuthenticated(principal);

        return SetCore(AuthenticationState.Authenticated, principal, scheme, expiresAt);
    }

    public AuthenticationStateSnapshot SetRefreshing(ClaimsPrincipal principal, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ThrowIfNotAuthenticated(principal);

        return SetCore(
            AuthenticationState.Refreshing,
            principal,
            scheme,
            preserveCurrentTokenHints: true);
    }

    public AuthenticationStateSnapshot SetExpired(ClaimsPrincipal principal, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ThrowIfNotAuthenticated(principal);

        return SetCore(
            AuthenticationState.Expired,
            principal,
            scheme,
            preserveCurrentTokenHints: true);
    }

    internal AuthenticationStateTransition SetAuthenticatedForAccountDeferred(
        ClaimsPrincipal principal,
        string? scheme,
        DateTimeOffset? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ThrowIfNotAuthenticated(principal);

        return SetCoreDeferred(AuthenticationState.Authenticated, principal, scheme, expiresAt);
    }

    internal AuthenticationStateTransition SetExpiredForAccountDeferred(
        ClaimsPrincipal principal,
        string? scheme,
        DateTimeOffset? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ThrowIfNotAuthenticated(principal);

        return SetCoreDeferred(AuthenticationState.Expired, principal, scheme, expiresAt);
    }

    internal AuthenticationStateTransition SetAnonymousForAccountDeferred(bool signedOut)
    {
        return SetCoreDeferred(
            signedOut ? AuthenticationState.SignedOut : AuthenticationState.Anonymous,
            SecurityPrincipals.Anonymous);
    }

    internal void DrainDeferredNotifications(AuthenticationStateTransition transition)
    {
        if (transition.ShouldDrain)
        {
            _eventPublisher.Drain(this);
        }
    }

    public AuthenticationStateSnapshot SetSignedOut()
    {
        return SetCore(AuthenticationState.SignedOut, SecurityPrincipals.Anonymous);
    }

    public AuthenticationStateSnapshot SetFailed(string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        return SetCore(
            AuthenticationState.Failed,
            SecurityPrincipals.Anonymous,
            scheme: null,
            expiresAt: null,
            failureMessage);
    }

    private AuthenticationStateSnapshot SetCore(
        AuthenticationState state,
        ClaimsPrincipal principal,
        string? scheme = null,
        DateTimeOffset? expiresAt = null,
        string? failureMessage = null,
        bool preserveCurrentTokenHints = false)
    {
        var transition = SetCoreDeferred(
            state,
            principal,
            scheme,
            expiresAt,
            failureMessage,
            preserveCurrentTokenHints);
        DrainDeferredNotifications(transition);
        return transition.Snapshot;
    }

    private AuthenticationStateTransition SetCoreDeferred(
        AuthenticationState state,
        ClaimsPrincipal principal,
        string? scheme = null,
        DateTimeOffset? expiresAt = null,
        string? failureMessage = null,
        bool preserveCurrentTokenHints = false)
    {
        var principalSnapshot = SecurityPrincipalSnapshot.Clone(principal);
        AuthenticationStateSnapshot previous;
        AuthenticationStateSnapshot current;
        AuthenticationStateChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            previous = _current;
            if (preserveCurrentTokenHints)
            {
                scheme ??= previous.Scheme;
                expiresAt ??= previous.ExpiresAt;
            }

            if (Matches(previous, state, principalSnapshot, scheme, expiresAt, failureMessage))
            {
                return new AuthenticationStateTransition(previous, ShouldDrain: false);
            }

            current = new AuthenticationStateSnapshot(
                state,
                principalSnapshot,
                previous.Revision + 1,
                scheme,
                expiresAt,
                failureMessage);
            _current = current;
            args = new AuthenticationStateChangedEventArgs(previous, current);
            shouldDrain = _eventPublisher.Enqueue(StateChanged, args);
        }

        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.AuthenticationStateChanged,
            "Authentication state changed.",
            HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["previousState"] = previous.State.ToString(),
                ["currentState"] = current.State.ToString(),
                ["reason"] = current.State.ToString(),
                ["revision"] = current.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scheme"] = current.Scheme,
                ["principalKind"] = current.PrincipalSnapshot.Identities.Any(static identity => identity.IsAuthenticated)
                    ? "Authenticated"
                    : "Anonymous",
            });

        return new AuthenticationStateTransition(current, shouldDrain);
    }

    private static bool Matches(
        AuthenticationStateSnapshot snapshot,
        AuthenticationState state,
        ClaimsPrincipal principal,
        string? scheme,
        DateTimeOffset? expiresAt,
        string? failureMessage)
    {
        return snapshot.State == state
            && string.Equals(snapshot.Scheme, scheme, StringComparison.Ordinal)
            && snapshot.ExpiresAt == expiresAt
            && string.Equals(snapshot.FailureMessage, failureMessage, StringComparison.Ordinal)
            && PrincipalsEqual(snapshot.PrincipalSnapshot, principal);
    }

    private static bool PrincipalsEqual(ClaimsPrincipal left, ClaimsPrincipal right)
    {
        var leftIdentities = left.Identities.ToArray();
        var rightIdentities = right.Identities.ToArray();

        if (leftIdentities.Length != rightIdentities.Length)
        {
            return false;
        }

        for (var i = 0; i < leftIdentities.Length; i++)
        {
            if (!IdentitiesEqual(leftIdentities[i], rightIdentities[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IdentitiesEqual(ClaimsIdentity left, ClaimsIdentity right)
    {
        return string.Equals(left.AuthenticationType, right.AuthenticationType, StringComparison.Ordinal)
            && string.Equals(left.NameClaimType, right.NameClaimType, StringComparison.Ordinal)
            && string.Equals(left.RoleClaimType, right.RoleClaimType, StringComparison.Ordinal)
            && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
            && ClaimsEqual(left.Claims, right.Claims)
            && ActorIdentitiesEqual(left.Actor, right.Actor);
    }

    private static bool ActorIdentitiesEqual(ClaimsIdentity? left, ClaimsIdentity? right)
    {
        return left is null
            ? right is null
            : right is not null && IdentitiesEqual(left, right);
    }

    private static bool ClaimsEqual(IEnumerable<Claim> left, IEnumerable<Claim> right)
    {
        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();

        while (true)
        {
            var leftMoved = leftEnumerator.MoveNext();
            var rightMoved = rightEnumerator.MoveNext();

            if (leftMoved != rightMoved)
            {
                return false;
            }

            if (!leftMoved)
            {
                return true;
            }

            if (!ClaimsEqual(leftEnumerator.Current, rightEnumerator.Current))
            {
                return false;
            }
        }
    }

    private static bool ClaimsEqual(Claim left, Claim right)
    {
        return string.Equals(left.Type, right.Type, StringComparison.Ordinal)
            && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && string.Equals(left.ValueType, right.ValueType, StringComparison.Ordinal)
            && string.Equals(left.Issuer, right.Issuer, StringComparison.Ordinal)
            && string.Equals(left.OriginalIssuer, right.OriginalIssuer, StringComparison.Ordinal)
            && ClaimPropertiesEqual(left.Properties, right.Properties);
    }

    private static bool ClaimPropertiesEqual(
        IDictionary<string, string> left,
        IDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value)
                || !string.Equals(pair.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ThrowIfNotAuthenticated(ClaimsPrincipal principal)
    {
        if (!principal.Identities.Any(static identity => identity.IsAuthenticated))
        {
            throw new ArgumentException(
                "Authenticated, refreshing, and expired states require an authenticated principal.",
                nameof(principal));
        }
    }

    internal readonly record struct AuthenticationStateTransition(
        AuthenticationStateSnapshot Snapshot,
        bool ShouldDrain);
}
