using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus;

/// <summary>
/// Provides the application-wide event publication and subscription surface.
/// </summary>
public interface IEventBus : IEventPublisher, IEventSubscriber, IDisposable, IAsyncDisposable
{
}

/// <summary>
/// Publishes typed events through the application EventBus.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event through its default channel and completes after delivery reaches a terminal result.
    /// </summary>
    ValueTask<EventPublishResult> PublishAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event through the specified channel and completes after delivery reaches a terminal result.
    /// </summary>
    ValueTask<EventPublishResult> PublishAsync<TEvent>(
        EventChannel<TEvent> channel,
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Admits an event to its default channel without waiting for handler delivery to complete.
    /// </summary>
    ValueTask<EventPostResult> PostAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Admits an event to the specified channel without waiting for handler delivery to complete.
    /// </summary>
    ValueTask<EventPostResult> PostAsync<TEvent>(
        EventChannel<TEvent> channel,
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates lifecycle-owned subscriptions for typed events.
/// </summary>
public interface IEventSubscriber
{
    /// <summary>
    /// Subscribes a delegate to the default channel for the lifetime of the supplied owner scope.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    /// <summary>
    /// Subscribes a delegate to a channel for the lifetime of the supplied owner scope.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    /// <summary>
    /// Subscribes a handler instance to the default channel for the lifetime of the supplied owner scope.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);

    /// <summary>
    /// Subscribes a handler instance to a channel for the lifetime of the supplied owner scope.
    /// </summary>
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);
}
