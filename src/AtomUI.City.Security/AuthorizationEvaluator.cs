using System.Security.Claims;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

/// <summary>
/// Represents authorization evaluator.
/// </summary>
public sealed class AuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly IPermissionRegistry _permissions;
    private readonly IAuthorizationPolicyProvider? _policyProvider;
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>AuthorizationEvaluator</c> type.
    /// </summary>
    public AuthorizationEvaluator(IPermissionRegistry permissions)
        : this(permissions, policyProvider: null, diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>AuthorizationEvaluator</c> type.
    /// </summary>
    public AuthorizationEvaluator(
        IPermissionRegistry permissions,
        IAuthorizationPolicyProvider policyProvider)
        : this(permissions, policyProvider, diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>AuthorizationEvaluator</c> type.
    /// </summary>
    public AuthorizationEvaluator(
        IPermissionRegistry permissions,
        IAuthorizationPolicyProvider? policyProvider,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        _permissions = permissions;
        _policyProvider = policyProvider;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the evaluate async operation.
    /// </summary>
    public ValueTask<AuthorizationResult> EvaluateAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(AuthorizationResult.Cancelled());
        }

        try
        {
            foreach (var requirement in request.Policy.Requirements)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromResult(AuthorizationResult.Cancelled());
                }

                var result = EvaluateRequirement(request.PrincipalSnapshot, requirement);
                if (!result.Succeeded)
                {
                    WriteFailureDiagnostic(request, result, requirement);
                    return ValueTask.FromResult(result);
                }
            }

            return ValueTask.FromResult(AuthorizationResult.Allowed());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(AuthorizationResult.Cancelled());
        }
        catch (Exception exception)
        {
            var result = AuthorizationResult.Failed(
                    SecurityFailureKind.EvaluatorFailed,
                    message: exception.Message,
                    exception: exception);
            WriteFailureDiagnostic(request, result, requirement: null);
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Executes the evaluate policy async operation.
    /// </summary>
    public async ValueTask<AuthorizationResult> EvaluatePolicyAsync(
        ClaimsPrincipal? principal,
        string policyName,
        string? resourceName = null,
        string? contributionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }

        if (_policyProvider is null)
        {
            var result = AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                policyName,
                "No authorization policy provider is configured.");
            WriteFailureDiagnostic(
                policyName,
                resourceName,
                contributionId,
                result,
                requirement: null,
                GetPrincipalIdHash(principal));
            return result;
        }

        try
        {
            var policy = await _policyProvider.GetPolicyAsync(policyName, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return AuthorizationResult.Cancelled();
            }

            if (policy is null)
            {
                var result = AuthorizationResult.Failed(
                    SecurityFailureKind.PolicyNotFound,
                    policyName,
                    $"Authorization policy '{policyName}' is not registered.");
                WriteFailureDiagnostic(
                    policyName,
                    resourceName,
                    contributionId,
                    result,
                    requirement: null,
                    GetPrincipalIdHash(principal));
                return result;
            }

            return await EvaluateAsync(
                    new AuthorizationRequest(principal, policy, resourceName, contributionId),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }
        catch (Exception exception)
        {
            var result = AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                policyName,
                exception.Message,
                exception: exception);
            WriteFailureDiagnostic(
                policyName,
                resourceName,
                contributionId,
                result,
                requirement: null,
                GetPrincipalIdHash(principal));
            return result;
        }
    }

    private AuthorizationResult EvaluateRequirement(
        ClaimsPrincipal principal,
        AuthorizationRequirement requirement)
    {
        return requirement.Kind switch
        {
            AuthorizationRequirementKind.Authenticated => EvaluateAuthenticated(principal),
            AuthorizationRequirementKind.Permission => EvaluatePermission(principal, requirement),
            AuthorizationRequirementKind.Claim => EvaluateClaim(principal, requirement),
            AuthorizationRequirementKind.Role => EvaluateRole(principal, requirement),
            _ => AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                requirement.Name,
                "Unsupported authorization requirement."),
        };
    }

    private static AuthorizationResult EvaluateAuthenticated(ClaimsPrincipal principal)
    {
        return IsAuthenticated(principal)
            ? AuthorizationResult.Allowed()
            : AuthorizationResult.Challenge("Authentication is required.");
    }

    private AuthorizationResult EvaluatePermission(
        ClaimsPrincipal principal,
        AuthorizationRequirement requirement)
    {
        if (!_permissions.Contains(requirement.Name))
        {
            return AuthorizationResult.Failed(
                SecurityFailureKind.PermissionNotFound,
                requirement.Name,
                $"Permission '{requirement.Name}' is not registered.");
        }

        if (!IsAuthenticated(principal))
        {
            return AuthorizationResult.Challenge("Authentication is required.");
        }

        return principal.HasClaim(SecurityClaimTypes.Permission, requirement.Name)
            ? AuthorizationResult.Allowed()
            : AuthorizationResult.Forbidden(
                requirement.Name,
                $"Permission '{requirement.Name}' is required.");
    }

    private static AuthorizationResult EvaluateClaim(
        ClaimsPrincipal principal,
        AuthorizationRequirement requirement)
    {
        if (!IsAuthenticated(principal))
        {
            return AuthorizationResult.Challenge("Authentication is required.");
        }

        var satisfied = requirement.Value is null
            ? principal.HasClaim(claim => claim.Type == requirement.Name)
            : principal.HasClaim(requirement.Name, requirement.Value);

        return satisfied
            ? AuthorizationResult.Allowed()
            : AuthorizationResult.Forbidden(
                requirement.Name,
                $"Claim '{requirement.Name}' is required.");
    }

    private static AuthorizationResult EvaluateRole(
        ClaimsPrincipal principal,
        AuthorizationRequirement requirement)
    {
        if (!IsAuthenticated(principal))
        {
            return AuthorizationResult.Challenge("Authentication is required.");
        }

        return principal.IsInRole(requirement.Name)
            || principal.HasClaim(ClaimTypes.Role, requirement.Name)
            ? AuthorizationResult.Allowed()
            : AuthorizationResult.Forbidden(
                requirement.Name,
                $"Role '{requirement.Name}' is required.");
    }

    private static bool IsAuthenticated(ClaimsPrincipal principal)
    {
        return principal.Identities.Any(static identity => identity.IsAuthenticated);
    }

    private void WriteFailureDiagnostic(
        AuthorizationRequest request,
        AuthorizationResult result,
        AuthorizationRequirement? requirement)
    {
        WriteFailureDiagnostic(
            request.Policy.Name,
            request.ResourceName,
            request.ContributionId ?? request.Policy.ContributionId,
            result,
            requirement,
            GetPrincipalIdHash(request.PrincipalSnapshot));
    }

    private void WriteFailureDiagnostic(
        string policyName,
        string? resourceName,
        string? contributionId,
        AuthorizationResult result,
        AuthorizationRequirement? requirement,
        string? principalIdHash)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            result.Status == AuthorizationResultStatus.Failed
                ? SecurityDiagnosticIds.AuthorizationEvaluationFailed
                : SecurityDiagnosticIds.AuthorizationDenied,
            "Authorization evaluation did not allow the request.",
            result.Status == AuthorizationResultStatus.Failed
                ? HostDiagnosticSeverity.Error
                : HostDiagnosticSeverity.Warning,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["policyName"] = policyName,
                ["requirementKind"] = requirement?.Kind.ToString(),
                ["requirementName"] = result.FailedRequirement ?? requirement?.Name,
                ["resourceName"] = resourceName,
                ["contributionId"] = contributionId,
                ["resultStatus"] = result.Status.ToString(),
                ["failureKind"] = result.FailureKind.ToString(),
                ["principalIdHash"] = principalIdHash,
                ["exceptionType"] = result.Exception?.GetType().FullName,
            });
    }

    private static string? GetPrincipalIdHash(ClaimsPrincipal? principal)
    {
        return SecurityDiagnostics.RedactIdentifier(
            principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }
}
