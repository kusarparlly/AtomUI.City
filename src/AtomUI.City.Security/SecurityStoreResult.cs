namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported security store result status values.
/// </summary>
public enum SecurityStoreResultStatus
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
    /// Represents the invalid data value.
    /// </summary>
    InvalidData = 2,
    /// <summary>
    /// Represents the unsupported schema value.
    /// </summary>
    UnsupportedSchema = 3,
    /// <summary>
    /// Represents the access denied value.
    /// </summary>
    AccessDenied = 4,
    /// <summary>
    /// Represents the io failed value.
    /// </summary>
    IoFailed = 5,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled = 6,
}

/// <summary>
/// Represents security store result&lt;t&gt;.
/// </summary>
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

    /// <summary>
    /// Gets status.
    /// </summary>
    public SecurityStoreResultStatus Status { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public T? Value { get; }

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
    public bool Succeeded => Status == SecurityStoreResultStatus.Success;

    /// <summary>
    /// Gets success.
    /// </summary>
    public static SecurityStoreResult<T> Success(T value) =>
        new(SecurityStoreResultStatus.Success, value, message: null, exception: null);

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
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
