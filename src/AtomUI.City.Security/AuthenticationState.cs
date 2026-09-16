namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported authentication state values.
/// </summary>
public enum AuthenticationState
{
    /// <summary>
    /// Represents the unknown value.
    /// </summary>
    Unknown,
    /// <summary>
    /// Represents the anonymous value.
    /// </summary>
    Anonymous,
    /// <summary>
    /// Represents the authenticating value.
    /// </summary>
    Authenticating,
    /// <summary>
    /// Represents the authenticated value.
    /// </summary>
    Authenticated,
    /// <summary>
    /// Represents the refreshing value.
    /// </summary>
    Refreshing,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired,
    /// <summary>
    /// Represents the signed out value.
    /// </summary>
    SignedOut,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
}
