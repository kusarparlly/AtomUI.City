namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported access token result status values.
/// </summary>
public enum AccessTokenResultStatus
{
    /// <summary>
    /// Represents the none value.
    /// </summary>
    None,
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success,
    /// <summary>
    /// Represents the required value.
    /// </summary>
    Required,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents the unavailable value.
    /// </summary>
    Unavailable,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
}
