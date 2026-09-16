namespace AtomUI.City.Security;

/// <summary>
/// Represents authorization result.
/// </summary>
public sealed class AuthorizationResult
{
    private AuthorizationResult(
        AuthorizationResultStatus status,
        SecurityFailureKind failureKind,
        string? failedRequirement,
        string? message,
        string? messageKey,
        IReadOnlyList<object?>? messageArguments,
        Exception? exception)
    {
        Status = status;
        FailureKind = failureKind;
        FailedRequirement = failedRequirement;
        Message = message;
        MessageKey = messageKey;
        MessageArguments = messageArguments is null ? null : Array.AsReadOnly(messageArguments.ToArray());
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public AuthorizationResultStatus Status { get; }

    /// <summary>
    /// Gets failure kind.
    /// </summary>
    public SecurityFailureKind FailureKind { get; }

    /// <summary>
    /// Gets failed requirement.
    /// </summary>
    public string? FailedRequirement { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets message key.
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// Gets message arguments.
    /// </summary>
    public IReadOnlyList<object?>? MessageArguments { get; }

    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Status == AuthorizationResultStatus.Allowed;

    /// <summary>
    /// Executes the allowed operation.
    /// </summary>
    public static AuthorizationResult Allowed()
    {
        return new AuthorizationResult(
            AuthorizationResultStatus.Allowed,
            SecurityFailureKind.None,
            failedRequirement: null,
            message: null,
            messageKey: null,
            messageArguments: null,
            exception: null);
    }

    /// <summary>
    /// Executes the challenge operation.
    /// </summary>
    public static AuthorizationResult Challenge(
        string? message = null,
        string? messageKey = "Errors.AuthenticationRequired",
        IReadOnlyList<object?>? messageArguments = null)
    {
        return new AuthorizationResult(
            AuthorizationResultStatus.Challenge,
            SecurityFailureKind.AuthenticationRequired,
            failedRequirement: "authenticated",
            message,
            messageKey,
            messageArguments,
            exception: null);
    }

    /// <summary>
    /// Executes the forbidden operation.
    /// </summary>
    public static AuthorizationResult Forbidden(
        string failedRequirement,
        string? message = null,
        string? messageKey = "Errors.AuthorizationForbidden",
        IReadOnlyList<object?>? messageArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failedRequirement);

        return new AuthorizationResult(
            AuthorizationResultStatus.Forbidden,
            SecurityFailureKind.Forbidden,
            failedRequirement,
            message,
            messageKey,
            messageArguments ?? [failedRequirement],
            exception: null);
    }

    /// <summary>
    /// Executes the denied operation.
    /// </summary>
    public static AuthorizationResult Denied(
        string failedRequirement,
        string? message = null,
        string? messageKey = "Errors.AuthorizationDenied",
        IReadOnlyList<object?>? messageArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failedRequirement);

        return new AuthorizationResult(
            AuthorizationResultStatus.Denied,
            SecurityFailureKind.RequirementFailed,
            failedRequirement,
            message,
            messageKey,
            messageArguments ?? [failedRequirement],
            exception: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static AuthorizationResult Failed(
        SecurityFailureKind failureKind,
        string? failedRequirement = null,
        string? message = null,
        string? messageKey = null,
        IReadOnlyList<object?>? messageArguments = null,
        Exception? exception = null)
    {
        if (!Enum.IsDefined(failureKind)
            || failureKind is SecurityFailureKind.None
                or SecurityFailureKind.AuthenticationRequired
                or SecurityFailureKind.Forbidden
                or SecurityFailureKind.RequirementFailed
                or SecurityFailureKind.Cancelled)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "The failure kind must represent a framework failure, not another authorization result status.");
        }

        return new AuthorizationResult(
            AuthorizationResultStatus.Failed,
            failureKind,
            failedRequirement,
            message,
            messageKey,
            messageArguments,
            exception);
    }

    /// <summary>
    /// Executes the cancelled operation.
    /// </summary>
    public static AuthorizationResult Cancelled(
        string? message = null,
        string? messageKey = "Errors.Cancelled",
        IReadOnlyList<object?>? messageArguments = null)
    {
        return new AuthorizationResult(
            AuthorizationResultStatus.Cancelled,
            SecurityFailureKind.Cancelled,
            failedRequirement: null,
            message,
            messageKey,
            messageArguments,
            exception: null);
    }
}
