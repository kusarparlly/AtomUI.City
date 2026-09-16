namespace AtomUI.City.Security;

/// <summary>
/// Represents security diagnostic ids.
/// </summary>
public static class SecurityDiagnosticIds
{
    /// <summary>
    /// Represents the authentication state changed value.
    /// </summary>
    public const string AuthenticationStateChanged = "AUCSEC001";

    /// <summary>
    /// Represents the authentication observer failed value.
    /// </summary>
    public const string AuthenticationObserverFailed = "AUCSEC002";

    /// <summary>
    /// Represents the permission registry changed value.
    /// </summary>
    public const string PermissionRegistryChanged = "AUCSEC101";

    /// <summary>
    /// Represents the permission observer failed value.
    /// </summary>
    public const string PermissionObserverFailed = "AUCSEC102";

    /// <summary>
    /// Represents the authorization denied value.
    /// </summary>
    public const string AuthorizationDenied = "AUCSEC200";

    /// <summary>
    /// Represents the authorization evaluation failed value.
    /// </summary>
    public const string AuthorizationEvaluationFailed = "AUCSEC201";

    /// <summary>
    /// Represents the route authorization completed value.
    /// </summary>
    public const string RouteAuthorizationCompleted = "AUCSEC300";

    /// <summary>
    /// Represents the route authorization failed value.
    /// </summary>
    public const string RouteAuthorizationFailed = "AUCSEC301";

    /// <summary>
    /// Represents the command authorization changed value.
    /// </summary>
    public const string CommandAuthorizationChanged = "AUCSEC400";

    /// <summary>
    /// Represents the command authorization failed value.
    /// </summary>
    public const string CommandAuthorizationFailed = "AUCSEC401";

    /// <summary>
    /// Represents the command authorization observer failed value.
    /// </summary>
    public const string CommandAuthorizationObserverFailed = "AUCSEC402";

    /// <summary>
    /// Represents the command authorization evaluated value.
    /// </summary>
    public const string CommandAuthorizationEvaluated = "AUCSEC403";

    /// <summary>
    /// Represents the access token resolved value.
    /// </summary>
    public const string AccessTokenResolved = "AUCSEC500";

    /// <summary>
    /// Represents the access token provider failed value.
    /// </summary>
    public const string AccessTokenProviderFailed = "AUCSEC501";

    /// <summary>
    /// Represents the account persistence completed value.
    /// </summary>
    public const string AccountPersistenceCompleted = "AUCSEC600";

    /// <summary>
    /// Represents the account persistence failed value.
    /// </summary>
    public const string AccountPersistenceFailed = "AUCSEC601";

    /// <summary>
    /// Represents the credential persistence completed value.
    /// </summary>
    public const string CredentialPersistenceCompleted = "AUCSEC610";

    /// <summary>
    /// Represents the credential persistence failed value.
    /// </summary>
    public const string CredentialPersistenceFailed = "AUCSEC611";

    /// <summary>
    /// Represents the account session changed value.
    /// </summary>
    public const string AccountSessionChanged = "AUCSEC700";

    /// <summary>
    /// Represents the account session operation failed value.
    /// </summary>
    public const string AccountSessionOperationFailed = "AUCSEC701";

    /// <summary>
    /// Represents the account session observer failed value.
    /// </summary>
    public const string AccountSessionObserverFailed = "AUCSEC702";
}
