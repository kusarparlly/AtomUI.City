namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the contract for ievent background scheduler.
/// </summary>
public interface IEventBackgroundScheduler
{
    /// <summary>
    /// Executes the run async operation.
    /// </summary>
    ValueTask RunAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents thread pool event background scheduler.
/// </summary>
public sealed class ThreadPoolEventBackgroundScheduler : IEventBackgroundScheduler
{
    /// <summary>
    /// Executes the run async operation.
    /// </summary>
    public async ValueTask RunAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        Task operation;
        if (ExecutionContext.IsFlowSuppressed())
        {
            operation = Task.Run(
                async () => await callback(cancellationToken).ConfigureAwait(false),
                cancellationToken);
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                operation = Task.Run(
                    async () => await callback(cancellationToken).ConfigureAwait(false),
                    cancellationToken);
            }
        }

        await operation.ConfigureAwait(false);
    }
}
