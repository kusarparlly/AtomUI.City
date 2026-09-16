namespace AtomUI.City.Testing;

/// <summary>
/// Represents fake ui work item.
/// </summary>
public sealed class FakeUiWorkItem
{
    private readonly Func<CancellationToken, ValueTask> _callback;
    private readonly CancellationToken _cancellationToken;

    internal FakeUiWorkItem(
        long id,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        Id = id;
        _callback = callback;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets or sets is canceled.
    /// </summary>
    public bool IsCanceled { get; private set; }

    /// <summary>
    /// Gets or sets is completed.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets or sets is faulted.
    /// </summary>
    public bool IsFaulted { get; private set; }

    /// <summary>
    /// Gets or sets exception.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Executes the cancel operation.
    /// </summary>
    public void Cancel()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCanceled = true;
    }

    internal void Execute()
    {
        if (IsCanceled || IsCompleted)
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            IsCanceled = true;
            IsCompleted = true;

            return;
        }

        try
        {
            _callback(_cancellationToken).GetAwaiter().GetResult();
            IsCompleted = true;
        }
        catch (Exception exception)
        {
            Exception = exception;
            IsFaulted = true;
            IsCompleted = true;

            throw;
        }
    }
}
