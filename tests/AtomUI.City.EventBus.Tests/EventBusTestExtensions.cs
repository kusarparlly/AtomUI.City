using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus.Tests;

/// <summary>
/// Supplies a process-lifetime owner for tests whose subject is not subscription ownership.
/// Ownership-specific tests always pass their own scope explicitly.
/// </summary>
internal static class EventBusTestExtensions
{
    private static readonly LifecycleScope ProcessOwner = LifecycleScope.CreateRoot(
        LifecycleScopeKind.Application,
        "eventbus-test-process");

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe(ProcessOwner, handler, options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe(ProcessOwner, channel, handler, options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe<TEvent>(
            ProcessOwner,
            (context, _) => handler(context),
            options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe<TEvent>(
            ProcessOwner,
            channel,
            (context, _) => handler(context),
            options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe<TEvent>(
            ProcessOwner,
            (context, _) =>
            {
                handler(context);
                return ValueTask.CompletedTask;
            },
            options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        EventChannel<TEvent> channel,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe<TEvent>(
            ProcessOwner,
            channel,
            (context, _) =>
            {
                handler(context);
                return ValueTask.CompletedTask;
            },
            options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this InMemoryEventBus eventBus,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null)
    {
        return eventBus.Subscribe(ProcessOwner, handler, options);
    }
}
