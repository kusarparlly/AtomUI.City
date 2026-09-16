using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents generated event handler descriptor.
/// </summary>
public sealed class GeneratedEventHandlerDescriptor
{
    private readonly Func<IServiceProvider, LifecycleScope, IEventSubscriber, IEventSubscription> _activate;

    private GeneratedEventHandlerDescriptor(
        Type ownerModuleType,
        Type eventType,
        Type handlerType,
        string channelName,
        Func<IServiceProvider, LifecycleScope, IEventSubscriber, IEventSubscription> activate)
    {
        OwnerModuleType = EventAttributeValidation.ValidateOwner(ownerModuleType, nameof(ownerModuleType));
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        ChannelName = EventAttributeValidation.ValidateName(channelName, nameof(channelName));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
    }

    /// <summary>
    /// Gets owner module type.
    /// </summary>
    public Type OwnerModuleType { get; }

    /// <summary>
    /// Gets event type.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// Gets handler type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets channel name.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// Executes the create&lt;tevent, thandler&gt; operation.
    /// </summary>
    public static GeneratedEventHandlerDescriptor Create<TEvent, THandler>(
        Type ownerModuleType,
        string channelName,
        EventDispatchPolicy dispatchPolicy,
        EventDispatchMode dispatchMode,
        EventErrorPolicy errorPolicy,
        int handlerTimeoutMilliseconds,
        int disableSubscriptionAfterFailures)
        where THandler : class, IEventHandler<TEvent>
    {
        var options = CreateOptions(
            dispatchPolicy,
            dispatchMode,
            errorPolicy,
            handlerTimeoutMilliseconds,
            disableSubscriptionAfterFailures,
            serviceProvider: null);

        return new GeneratedEventHandlerDescriptor(
            ownerModuleType,
            typeof(TEvent),
            typeof(THandler),
            channelName,
            (serviceProvider, owner, subscriber) =>
            {
                var actualOptions = dispatchPolicy == EventDispatchPolicy.UiThread
                    ? CreateOptions(dispatchPolicy, dispatchMode, errorPolicy, handlerTimeoutMilliseconds,
                        disableSubscriptionAfterFailures, serviceProvider)
                    : options;
                return subscriber.Subscribe(
                    owner,
                    new EventChannel<TEvent>(channelName),
                    serviceProvider.GetRequiredService<THandler>(),
                    actualOptions);
            });
    }

    internal IEventSubscription Activate(
        IServiceProvider serviceProvider,
        LifecycleScope owner,
        IEventSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(subscriber);
        return _activate(serviceProvider, owner, subscriber);
    }

    private static EventSubscriptionOptions CreateOptions(
        EventDispatchPolicy dispatchPolicy,
        EventDispatchMode dispatchMode,
        EventErrorPolicy errorPolicy,
        int handlerTimeoutMilliseconds,
        int disableSubscriptionAfterFailures,
        IServiceProvider? serviceProvider)
    {
        if (!Enum.IsDefined(dispatchPolicy)) throw new ArgumentOutOfRangeException(nameof(dispatchPolicy));
        if (!Enum.IsDefined(dispatchMode)) throw new ArgumentOutOfRangeException(nameof(dispatchMode));
        if (!Enum.IsDefined(errorPolicy)) throw new ArgumentOutOfRangeException(nameof(errorPolicy));
        if (handlerTimeoutMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(handlerTimeoutMilliseconds));
        if (disableSubscriptionAfterFailures <= 0) throw new ArgumentOutOfRangeException(nameof(disableSubscriptionAfterFailures));

        var options = dispatchPolicy switch
        {
            EventDispatchPolicy.Current => EventSubscriptionOptions.Current,
            EventDispatchPolicy.Serialized => EventSubscriptionOptions.Serialized,
            EventDispatchPolicy.Background => EventSubscriptionOptions.Background(),
            EventDispatchPolicy.UiThread when serviceProvider is not null =>
                EventSubscriptionOptions.UiThread(serviceProvider.GetRequiredService<IUiDispatcher>(), dispatchMode),
            EventDispatchPolicy.UiThread => EventSubscriptionOptions.Current,
            _ => throw new ArgumentOutOfRangeException(nameof(dispatchPolicy)),
        };

        return options
            .WithErrorPolicy(errorPolicy)
            .WithHandlerTimeout(handlerTimeoutMilliseconds == 0 ? null : TimeSpan.FromMilliseconds(handlerTimeoutMilliseconds))
            .WithDisableSubscriptionAfterFailures(disableSubscriptionAfterFailures);
    }
}
