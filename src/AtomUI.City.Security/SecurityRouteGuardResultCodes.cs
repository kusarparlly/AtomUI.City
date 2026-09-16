namespace AtomUI.City.Security;

/// <summary>
/// Represents security route guard result codes.
/// </summary>
public static class SecurityRouteGuardResultCodes
{
    /// <summary>
    /// Represents the authentication required value.
    /// </summary>
    public const string AuthenticationRequired = "authentication-required";

    /// <summary>
    /// Represents the forbidden value.
    /// </summary>
    public const string Forbidden = "authorization-forbidden";

    /// <summary>
    /// Represents the authorization failed value.
    /// </summary>
    public const string AuthorizationFailed = "authorization-failed";
}
