using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for ipermission checker.
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Executes the check async operation.
    /// </summary>
    ValueTask<AuthorizationResult> CheckAsync(
        ClaimsPrincipal? principal,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the check current async operation.
    /// </summary>
    ValueTask<AuthorizationResult> CheckCurrentAsync(
        string permissionName,
        CancellationToken cancellationToken = default);
}
