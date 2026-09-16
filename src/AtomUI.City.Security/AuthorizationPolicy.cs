namespace AtomUI.City.Security;

/// <summary>
/// Represents authorization policy.
/// </summary>
public sealed class AuthorizationPolicy
{
    /// <summary>
    /// Initializes a new instance of the <c>AuthorizationPolicy</c> type.
    /// </summary>
    public AuthorizationPolicy(
        string name,
        IReadOnlyCollection<AuthorizationRequirement> requirements,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(requirements);

        var requirementSnapshot = requirements.ToArray();
        if (requirementSnapshot.Length == 0)
        {
            throw new ArgumentException("An authorization policy must contain at least one requirement.", nameof(requirements));
        }

        if (requirementSnapshot.Any(static requirement => requirement is null))
        {
            throw new ArgumentException("Authorization policy requirements cannot contain null values.", nameof(requirements));
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        Name = name;
        Requirements = Array.AsReadOnly(requirementSnapshot);
        ContributionId = contributionId;
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets requirements.
    /// </summary>
    public IReadOnlyList<AuthorizationRequirement> Requirements { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Executes the require authenticated operation.
    /// </summary>
    public static AuthorizationPolicy RequireAuthenticated(string name)
    {
        return new AuthorizationPolicy(
            name,
            [AuthorizationRequirement.RequireAuthenticated()]);
    }

    /// <summary>
    /// Executes the require permission operation.
    /// </summary>
    public static AuthorizationPolicy RequirePermission(
        string name,
        string permissionName,
        string? contributionId = null)
    {
        return new AuthorizationPolicy(
            name,
            [
                AuthorizationRequirement.RequireAuthenticated(),
                AuthorizationRequirement.RequirePermission(permissionName),
            ],
            contributionId);
    }
}
