namespace AtomUI.City.Security;

public enum AccountSwitchResultStatus
{
    Success = 0,
    NotFound = 1,
    CredentialUnavailable = 2,
    PermissionUnavailable = 3,
    InvalidData = 4,
    Cancelled = 5,
    Failed = 6,
}

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

    public AccountSwitchResultStatus Status { get; }

    public AccountSessionSnapshot Session { get; }

    public Guid OperationId { get; }

    public string? FailureStage { get; }

    public string? Message { get; }

    public Exception? Exception { get; }

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
