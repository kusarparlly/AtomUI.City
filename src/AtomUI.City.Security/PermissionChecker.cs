using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Represents permission checker.
/// </summary>
public sealed class PermissionChecker : IPermissionChecker
{
    private readonly IAuthorizationEvaluator _authorizationEvaluator;
    private readonly ICurrentPrincipalAccessor? _principalAccessor;

    /// <summary>
    /// Initializes a new instance of the <c>PermissionChecker</c> type.
    /// </summary>
    public PermissionChecker(
        IPermissionRegistry permissions,
        ICurrentPrincipalAccessor? principalAccessor = null)
        : this(new AuthorizationEvaluator(permissions), principalAccessor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>PermissionChecker</c> type.
    /// </summary>
    public PermissionChecker(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor? principalAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(authorizationEvaluator);

        _authorizationEvaluator = authorizationEvaluator;
        _principalAccessor = principalAccessor;
    }

    /// <summary>
    /// Executes the check async operation.
    /// </summary>
    public async ValueTask<AuthorizationResult> CheckAsync(
        ClaimsPrincipal? principal,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

        if (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }

        try
        {
            var result = await _authorizationEvaluator.EvaluateAsync(
                    new AuthorizationRequest(
                        principal,
                        AuthorizationPolicy.RequirePermission($"Permission:{permissionName}", permissionName)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return AuthorizationResult.Cancelled();
            }

            return result ?? AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                permissionName,
                "The authorization evaluator returned null.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }
        catch (Exception exception)
        {
            return AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                permissionName,
                exception.Message,
                exception: exception);
        }
    }

    /// <summary>
    /// Executes the check current async operation.
    /// </summary>
    public async ValueTask<AuthorizationResult> CheckCurrentAsync(
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

        if (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }

        if (_principalAccessor is null)
        {
            return AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                permissionName,
                "No current principal accessor is configured.");
        }

        try
        {
            var principal = _principalAccessor.Principal;
            if (cancellationToken.IsCancellationRequested)
            {
                return AuthorizationResult.Cancelled();
            }

            return await CheckAsync(principal, permissionName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Cancelled();
        }
        catch (Exception exception)
        {
            return AuthorizationResult.Failed(
                SecurityFailureKind.EvaluatorFailed,
                permissionName,
                exception.Message,
                exception: exception);
        }
    }
}
