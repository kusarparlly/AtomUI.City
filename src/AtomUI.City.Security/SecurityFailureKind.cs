namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported security failure kind values.
/// </summary>
public enum SecurityFailureKind
{
    /// <summary>
    /// Represents the none value.
    /// </summary>
    None,
    /// <summary>
    /// Represents the authentication required value.
    /// </summary>
    AuthenticationRequired,
    /// <summary>
    /// Represents the authentication expired value.
    /// </summary>
    AuthenticationExpired,
    /// <summary>
    /// Represents the forbidden value.
    /// </summary>
    Forbidden,
    /// <summary>
    /// Represents the policy not found value.
    /// </summary>
    PolicyNotFound,
    /// <summary>
    /// Represents the permission not found value.
    /// </summary>
    PermissionNotFound,
    /// <summary>
    /// Represents the requirement failed value.
    /// </summary>
    RequirementFailed,
    /// <summary>
    /// Represents the evaluator failed value.
    /// </summary>
    EvaluatorFailed,
    /// <summary>
    /// Represents the contribution revoked value.
    /// </summary>
    ContributionRevoked,
    /// <summary>
    /// Represents the capability denied value.
    /// </summary>
    CapabilityDenied,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
}
