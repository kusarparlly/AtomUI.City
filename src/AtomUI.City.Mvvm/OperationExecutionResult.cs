namespace AtomUI.City.Mvvm;

/// <summary>
/// Describes the runtime outcome of one operation execution.
/// </summary>
/// <param name="OperationId">The operation identifier.</param>
/// <param name="Status">The terminal execution status.</param>
/// <param name="Elapsed">The elapsed execution time.</param>
/// <param name="Error">The original execution error, if any.</param>
public sealed record OperationExecutionResult(
    Guid OperationId,
    OperationStatus Status,
    TimeSpan Elapsed,
    Exception? Error = null);
