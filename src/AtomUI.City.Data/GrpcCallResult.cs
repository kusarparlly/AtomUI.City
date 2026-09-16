namespace AtomUI.City.Data;

/// <summary>
/// Represents grpc call result&lt;t&gt;.
/// </summary>
public sealed class GrpcCallResult<T>
{
    private GrpcCallResult(
        bool succeeded,
        T? value,
        GrpcStatusCode statusCode,
        string? detail)
    {
        Succeeded = succeeded;
        Value = value;
        StatusCode = statusCode;
        Detail = detail;
    }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets status code.
    /// </summary>
    public GrpcStatusCode StatusCode { get; }

    /// <summary>
    /// Gets detail.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static GrpcCallResult<T> Success(T value)
    {
        return new GrpcCallResult<T>(succeeded: true, value, GrpcStatusCode.OK, detail: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static GrpcCallResult<T> Failed(GrpcStatusCode statusCode, string? detail = null)
    {
        if (!Enum.IsDefined(statusCode))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "gRPC status code is not supported.");
        }

        if (statusCode == GrpcStatusCode.OK)
        {
            throw new ArgumentException("A failed gRPC result cannot use the OK status code.", nameof(statusCode));
        }

        if (detail is not null && string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("gRPC failure detail cannot be empty when provided.", nameof(detail));
        }

        return new GrpcCallResult<T>(succeeded: false, value: default, statusCode, detail);
    }
}
