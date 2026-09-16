namespace AtomUI.City.Security;

/// <summary>
/// Represents authorization requirement.
/// </summary>
public sealed class AuthorizationRequirement
{
    private AuthorizationRequirement(
        AuthorizationRequirementKind kind,
        string name,
        string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Kind = kind;
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public AuthorizationRequirementKind Kind { get; }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Executes the require authenticated operation.
    /// </summary>
    public static AuthorizationRequirement RequireAuthenticated()
    {
        return new AuthorizationRequirement(
            AuthorizationRequirementKind.Authenticated,
            "authenticated",
            value: null);
    }

    /// <summary>
    /// Executes the require permission operation.
    /// </summary>
    public static AuthorizationRequirement RequirePermission(string permissionName)
    {
        return new AuthorizationRequirement(
            AuthorizationRequirementKind.Permission,
            permissionName,
            value: null);
    }

    /// <summary>
    /// Executes the require claim operation.
    /// </summary>
    public static AuthorizationRequirement RequireClaim(string claimType, string? claimValue = null)
    {
        return new AuthorizationRequirement(
            AuthorizationRequirementKind.Claim,
            claimType,
            claimValue);
    }

    /// <summary>
    /// Executes the require role operation.
    /// </summary>
    public static AuthorizationRequirement RequireRole(string roleName)
    {
        return new AuthorizationRequirement(
            AuthorizationRequirementKind.Role,
            roleName,
            value: null);
    }
}
