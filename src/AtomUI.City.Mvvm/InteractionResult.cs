namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents interaction result&lt;tresult&gt;.
/// </summary>
public sealed class InteractionResult<TResult>
{
    private InteractionResult(
        InteractionResultStatus status,
        TResult? value,
        Exception? exception)
    {
        Status = status;
        Value = value;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public InteractionResultStatus Status { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public TResult? Value { get; }

    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Executes the completed operation.
    /// </summary>
    public static InteractionResult<TResult> Completed(TResult value)
    {
        return new InteractionResult<TResult>(
            InteractionResultStatus.Completed,
            value,
            exception: null);
    }

    /// <summary>
    /// Executes the canceled operation.
    /// </summary>
    public static InteractionResult<TResult> Canceled()
    {
        return new InteractionResult<TResult>(
            InteractionResultStatus.Canceled,
            value: default,
            exception: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static InteractionResult<TResult> Failed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new InteractionResult<TResult>(
            InteractionResultStatus.Failed,
            value: default,
            exception);
    }

    /// <summary>
    /// Executes the not handled operation.
    /// </summary>
    public static InteractionResult<TResult> NotHandled()
    {
        return new InteractionResult<TResult>(
            InteractionResultStatus.NotHandled,
            value: default,
            exception: null);
    }
}
