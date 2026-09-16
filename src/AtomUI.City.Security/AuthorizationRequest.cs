using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Represents authorization request.
/// </summary>
public sealed class AuthorizationRequest
{
    private readonly ClaimsPrincipal _principal;

    /// <summary>
    /// Initializes a new instance of the <c>AuthorizationRequest</c> type.
    /// </summary>
    public AuthorizationRequest(
        ClaimsPrincipal? principal,
        AuthorizationPolicy policy,
        string? resourceName = null,
        string? contributionId = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        ValidateOptional(resourceName, nameof(resourceName));
        ValidateOptional(contributionId, nameof(contributionId));

        _principal = SecurityPrincipalSnapshot.Clone(principal ?? SecurityPrincipals.Anonymous);
        Policy = policy;
        ResourceName = resourceName;
        ContributionId = contributionId;
    }

    /// <summary>
    /// Gets principal.
    /// </summary>
    public ClaimsPrincipal Principal => SecurityPrincipalSnapshot.Clone(_principal);

    internal ClaimsPrincipal PrincipalSnapshot => _principal;

    /// <summary>
    /// Gets policy.
    /// </summary>
    public AuthorizationPolicy Policy { get; }

    /// <summary>
    /// Gets resource name.
    /// </summary>
    public string? ResourceName { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
