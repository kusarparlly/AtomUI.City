namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the contract for ievent subscription.
/// </summary>
public interface IEventSubscription : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets id.
    /// </summary>
    EventSubscriptionId Id { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    EventSubscriptionState State { get; }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
