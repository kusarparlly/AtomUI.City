namespace AtomUI.City.Security;

public enum SecurityStoreResultStatus
{
    Success = 0,
    NotFound = 1,
    InvalidData = 2,
    UnsupportedSchema = 3,
    AccessDenied = 4,
    IoFailed = 5,
    Cancelled = 6,
}

public sealed class SecurityStoreResult<T>
{
    private SecurityStoreResult(
        SecurityStoreResultStatus status,
        T? value,
        string? message,
        Exception? exception)
    {
        Status = status;
        Value = value;
        Message = message;
        Exception = exception;
    }

    public SecurityStoreResultStatus Status { get; }

    public T? Value { get; }

    public string? Message { get; }

    public Exception? Exception { get; }

    public bool Succeeded => Status == SecurityStoreResultStatus.Success;

    public static SecurityStoreResult<T> Success(T value) =>
        new(SecurityStoreResultStatus.Success, value, message: null, exception: null);

    public static SecurityStoreResult<T> Failed(
        SecurityStoreResultStatus status,
        string? message = null,
        Exception? exception = null)
    {
        if (status == SecurityStoreResultStatus.Success || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new SecurityStoreResult<T>(status, default, message, exception);
    }
}
