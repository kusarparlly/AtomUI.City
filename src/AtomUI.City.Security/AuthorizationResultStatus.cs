namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported authorization result status values.
/// </summary>
public enum AuthorizationResultStatus
{
    /// <summary>
    /// Represents the allowed value.
    /// </summary>
    Allowed,
    /// <summary>
    /// Represents the denied value.
    /// </summary>
    Denied,
    /// <summary>
    /// Represents the forbidden value.
    /// </summary>
    Forbidden,
    /// <summary>
    /// Represents the challenge value.
    /// </summary>
    Challenge,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
}
