namespace AtomUI.City.Security;

/// <summary>
/// Represents access token result.
/// </summary>
public sealed class AccessTokenResult
{
    private AccessTokenResult(
        AccessTokenResultStatus status,
        string? token,
        string? scheme,
        DateTimeOffset? expiresAt,
        string? message,
        Exception? exception)
    {
        Status = status;
        Token = token;
        Scheme = scheme;
        ExpiresAt = expiresAt;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public AccessTokenResultStatus Status { get; }

    /// <summary>
    /// Gets token.
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string? Scheme { get; }

    /// <summary>
    /// Gets expires at.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Status == AccessTokenResultStatus.Success;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static AccessTokenResult Success(
        string token,
        string scheme,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

        return new AccessTokenResult(
            AccessTokenResultStatus.Success,
            token,
            scheme,
            expiresAt,
            message: null,
            exception: null);
    }

    /// <summary>
    /// Executes the none operation.
    /// </summary>
    public static AccessTokenResult None()
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.None,
            token: null,
            scheme: null,
            expiresAt: null,
            message: null,
            exception: null);
    }

    /// <summary>
    /// Executes the required operation.
    /// </summary>
    public static AccessTokenResult Required(string? message = null)
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.Required,
            token: null,
            scheme: null,
            expiresAt: null,
            message,
            exception: null);
    }

    /// <summary>
    /// Executes the expired operation.
    /// </summary>
    public static AccessTokenResult Expired(string? message = null)
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.Expired,
            token: null,
            scheme: null,
            expiresAt: null,
            message,
            exception: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static AccessTokenResult Failed(
        string? message = null,
        Exception? exception = null)
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.Failed,
            token: null,
            scheme: null,
            expiresAt: null,
            message,
            exception);
    }

    /// <summary>
    /// Executes the unavailable operation.
    /// </summary>
    public static AccessTokenResult Unavailable(string? message = null)
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.Unavailable,
            token: null,
            scheme: null,
            expiresAt: null,
            message,
            exception: null);
    }

    /// <summary>
    /// Executes the cancelled operation.
    /// </summary>
    public static AccessTokenResult Cancelled(string? message = null)
    {
        return new AccessTokenResult(
            AccessTokenResultStatus.Cancelled,
            token: null,
            scheme: null,
            expiresAt: null,
            message,
            exception: null);
    }
}
