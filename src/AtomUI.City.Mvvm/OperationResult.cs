namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents operation result.
/// </summary>
/// <param name="OperationId">The operation id value.</param>
/// <param name="Status">The status value.</param>
/// <param name="Elapsed">The elapsed value.</param>
/// <param name="Error">The error value.</param>
public sealed record OperationResult(
    Guid OperationId,
    OperationStatus Status,
    TimeSpan Elapsed,
    Exception? Error = null);
