namespace AtomUI.City.Data;

/// <summary>
/// Represents data result&lt;t&gt;.
/// </summary>
public sealed class DataResult<T>
{
    private DataResult(
        DataResultStatus status,
        T? value,
        DataError? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public DataResultStatus Status { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets error.
    /// </summary>
    public DataError? Error { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Status == DataResultStatus.Success;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static DataResult<T> Success(T value)
    {
        return new DataResult<T>(DataResultStatus.Success, value, error: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static DataResult<T> Failed(DataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new DataResult<T>(DataResultStatus.Failed, value: default, error);
    }

    /// <summary>
    /// Executes the partial operation.
    /// </summary>
    public static DataResult<T> Partial(T value, DataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new DataResult<T>(DataResultStatus.Partial, value, error);
    }

    /// <summary>
    /// Executes the cancelled operation.
    /// </summary>
    public static DataResult<T> Cancelled(string? message = null)
    {
        return new DataResult<T>(
            DataResultStatus.Cancelled,
            value: default,
            new DataError(DataErrorKind.Cancelled, message ?? "Data operation was cancelled."));
    }

    /// <summary>
    /// Executes the stale suppressed operation.
    /// </summary>
    public static DataResult<T> StaleSuppressed(string? message = null)
    {
        return new DataResult<T>(
            DataResultStatus.StaleSuppressed,
            value: default,
            new DataError(DataErrorKind.Cancelled, message ?? "Data operation result was suppressed."));
    }

    /// <summary>
    /// Executes the cast&lt;tresponse&gt; operation.
    /// </summary>
    public DataResult<TResponse> Cast<TResponse>()
    {
        if (Succeeded)
        {
            return TryCastValue(Value, out TResponse? response)
                ? DataResult<TResponse>.Success(response!)
                : DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        $"Data result value cannot be cast to '{typeof(TResponse).FullName}'."));
        }

        if (Status == DataResultStatus.Partial)
        {
            return TryCastValue(Value, out TResponse? response)
                ? DataResult<TResponse>.Partial(response!, Error!)
                : DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        $"Partial data result value cannot be cast to '{typeof(TResponse).FullName}'."));
        }

        return Status switch
        {
            DataResultStatus.Cancelled => DataResult<TResponse>.Cancelled(Error?.Message),
            DataResultStatus.StaleSuppressed => DataResult<TResponse>.StaleSuppressed(Error?.Message),
            _ => DataResult<TResponse>.Failed(Error ?? new DataError(DataErrorKind.Unknown, "Data operation failed.")),
        };
    }

    private static bool TryCastValue<TResponse>(T? value, out TResponse? response)
    {
        if (value is TResponse typedValue)
        {
            response = typedValue;
            return true;
        }

        if (value is null && default(TResponse) is null)
        {
            response = default;
            return true;
        }

        response = default;
        return false;
    }
}
