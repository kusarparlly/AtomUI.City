namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported authorization requirement kind values.
/// </summary>
public enum AuthorizationRequirementKind
{
    /// <summary>
    /// Represents the authenticated value.
    /// </summary>
    Authenticated,
    /// <summary>
    /// Represents the permission value.
    /// </summary>
    Permission,
    /// <summary>
    /// Represents the claim value.
    /// </summary>
    Claim,
    /// <summary>
    /// Represents the role value.
    /// </summary>
    Role,
}
