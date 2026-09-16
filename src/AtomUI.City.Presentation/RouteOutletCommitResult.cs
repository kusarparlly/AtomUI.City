namespace AtomUI.City.Presentation;

/// <summary>
/// Represents route outlet commit result.
/// </summary>
public sealed class RouteOutletCommitResult
{
    private RouteOutletCommitResult(
        bool succeeded,
        PresentationError? error,
        string? message,
        long operationId = 0)
    {
        Succeeded = succeeded;
        Error = error;
        Message = message;
        OperationId = operationId;
    }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets error.
    /// </summary>
    public PresentationError? Error { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets operation id.
    /// </summary>
    public long OperationId { get; }

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static RouteOutletCommitResult Success(long operationId = 0)
    {
        return new RouteOutletCommitResult(
            succeeded: true,
            error: null,
            message: null,
            operationId);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static RouteOutletCommitResult Failed(
        PresentationError error,
        string message,
        long operationId = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new RouteOutletCommitResult(
            succeeded: false,
            error,
            message,
            operationId);
    }
}
