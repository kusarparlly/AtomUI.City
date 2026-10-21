namespace AtomUI.City.Routing;

/// <summary>
/// Represents the result of a receipt-guarded conditional restore.
/// </summary>
public sealed class NavigationRestoreResult
{
    private NavigationRestoreResult(
        NavigationRestoreResultStatus status,
        NavigationCommitReceipt receipt,
        NavigationSnapshot current,
        string? code,
        string? message,
        Exception? exception)
    {
        Status = status;
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        Code = code;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Gets the restore status.
    /// </summary>
    public NavigationRestoreResultStatus Status { get; }

    /// <summary>
    /// Gets the receipt supplied to the restore operation.
    /// </summary>
    public NavigationCommitReceipt Receipt { get; }

    /// <summary>
    /// Gets the router snapshot observed after the restore attempt.
    /// </summary>
    public NavigationSnapshot Current { get; }

    /// <summary>
    /// Gets a stable failure or rejection code.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets a diagnostic message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the unexpected exception, when any.
    /// </summary>
    public Exception? Exception { get; }

    internal static NavigationRestoreResult Restored(
        NavigationCommitReceipt receipt,
        NavigationSnapshot current) =>
        new(NavigationRestoreResultStatus.Restored, receipt, current, code: null, message: null, exception: null);

    internal static NavigationRestoreResult Rejected(
        NavigationCommitReceipt receipt,
        NavigationSnapshot current,
        string code,
        string message) =>
        new(NavigationRestoreResultStatus.Rejected, receipt, current, code, message, exception: null);

    internal static NavigationRestoreResult Cancelled(
        NavigationCommitReceipt receipt,
        NavigationSnapshot current) =>
        new(
            NavigationRestoreResultStatus.Cancelled,
            receipt,
            current,
            "CITY-NAVIGATION-RESTORE-CANCELLED",
            "Conditional navigation restore was cancelled before mutation.",
            exception: null);

    internal static NavigationRestoreResult Failed(
        NavigationCommitReceipt receipt,
        NavigationSnapshot current,
        Exception exception) =>
        new(
            NavigationRestoreResultStatus.Failed,
            receipt,
            current,
            "CITY-NAVIGATION-RESTORE-FAILED",
            exception.Message,
            exception);
}
