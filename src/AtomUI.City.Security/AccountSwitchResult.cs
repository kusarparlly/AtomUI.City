namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported account switch result status values.
/// </summary>
public enum AccountSwitchResultStatus
{
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success = 0,
    /// <summary>
    /// Represents the not found value.
    /// </summary>
    NotFound = 1,
    /// <summary>
    /// Represents the credential unavailable value.
    /// </summary>
    CredentialUnavailable = 2,
    /// <summary>
    /// Represents the permission unavailable value.
    /// </summary>
    PermissionUnavailable = 3,
    /// <summary>
    /// Represents the invalid data value.
    /// </summary>
    InvalidData = 4,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled = 5,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed = 6,
}

/// <summary>
/// Represents account switch result.
/// </summary>
public sealed class AccountSwitchResult
{
    private AccountSwitchResult(
        AccountSwitchResultStatus status,
        AccountSessionSnapshot session,
        Guid operationId,
        string? failureStage,
        string? message,
        Exception? exception)
    {
        Status = status;
        Session = session;
        OperationId = operationId;
        FailureStage = failureStage;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public AccountSwitchResultStatus Status { get; }

    /// <summary>
    /// Gets session.
    /// </summary>
    public AccountSessionSnapshot Session { get; }

    /// <summary>
    /// Gets operation id.
    /// </summary>
    public Guid OperationId { get; }

    /// <summary>
    /// Gets failure stage.
    /// </summary>
    public string? FailureStage { get; }

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
    public bool Succeeded => Status == AccountSwitchResultStatus.Success;

    internal static AccountSwitchResult Success(AccountSessionSnapshot session, Guid operationId) =>
        new(AccountSwitchResultStatus.Success, session, operationId, null, null, null);

    internal static AccountSwitchResult Failed(
        AccountSwitchResultStatus status,
        AccountSessionSnapshot session,
        Guid operationId,
        string failureStage,
        string? message = null,
        Exception? exception = null)
    {
        if (status == AccountSwitchResultStatus.Success || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(failureStage);
        return new AccountSwitchResult(status, session, operationId, failureStage, message, exception);
    }
}
