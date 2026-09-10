namespace AtomUI.City.Presentation;

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

    public bool Succeeded { get; }

    public PresentationError? Error { get; }

    public string? Message { get; }

    public long OperationId { get; }

    public static RouteOutletCommitResult Success(long operationId = 0)
    {
        return new RouteOutletCommitResult(
            succeeded: true,
            error: null,
            message: null,
            operationId);
    }

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
