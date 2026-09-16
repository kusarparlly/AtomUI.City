using System.Security.Claims;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for icurrent principal accessor.
/// </summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// Gets principal.
    /// </summary>
    ClaimsPrincipal Principal { get; }
}
