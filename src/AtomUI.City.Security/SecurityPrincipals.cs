using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Represents security principals.
/// </summary>
public static class SecurityPrincipals
{
    /// <summary>
    /// Gets anonymous.
    /// </summary>
    public static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}
