using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iauthorization evaluator.
/// </summary>
public interface IAuthorizationEvaluator
{
    /// <summary>
    /// Executes the evaluate async operation.
    /// </summary>
    ValueTask<AuthorizationResult> EvaluateAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the evaluate policy async operation.
    /// </summary>
    ValueTask<AuthorizationResult> EvaluatePolicyAsync(
        ClaimsPrincipal? principal,
        string policyName,
        string? resourceName = null,
        string? contributionId = null,
        CancellationToken cancellationToken = default);
}
