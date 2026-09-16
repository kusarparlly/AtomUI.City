namespace AtomUI.City.Routing;

/// <summary>
/// Represents route guard result.
/// </summary>
public sealed class RouteGuardResult
{
    private RouteGuardResult(
        RouteGuardResultStatus status,
        string? code,
        string? message,
        NavigationTarget? redirectTarget,
        Exception? exception)
    {
        Status = status;
        Code = code;
        Message = message;
        RedirectTarget = redirectTarget;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public RouteGuardResultStatus Status { get; }

    /// <summary>
    /// Gets code.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets redirect target.
    /// </summary>
    public NavigationTarget? RedirectTarget { get; }

    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Executes the allow operation.
    /// </summary>
    public static RouteGuardResult Allow()
    {
        return new RouteGuardResult(
            RouteGuardResultStatus.Allow,
            code: null,
            message: null,
            redirectTarget: null,
            exception: null);
    }

    /// <summary>
    /// Executes the reject operation.
    /// </summary>
    public static RouteGuardResult Reject(string code, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new RouteGuardResult(
            RouteGuardResultStatus.Reject,
            code,
            message,
            redirectTarget: null,
            exception: null);
    }

    /// <summary>
    /// Executes the cancel operation.
    /// </summary>
    public static RouteGuardResult Cancel(string? message = null)
    {
        return new RouteGuardResult(
            RouteGuardResultStatus.Cancel,
            code: null,
            message,
            redirectTarget: null,
            exception: null);
    }

    /// <summary>
    /// Executes the redirect operation.
    /// </summary>
    public static RouteGuardResult Redirect(NavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new RouteGuardResult(
            RouteGuardResultStatus.Redirect,
            code: null,
            message: null,
            target,
            exception: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static RouteGuardResult Failed(
        string code,
        string message,
        Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new RouteGuardResult(
            RouteGuardResultStatus.Failed,
            code,
            message,
            redirectTarget: null,
            exception);
    }
}
