namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event diagnostic ids.
/// </summary>
public static class EventDiagnosticIds
{
    /// <summary>
    /// Represents the event published value.
    /// </summary>
    public const string EventPublished = "EventBus.EventPublished";
    /// <summary>
    /// Represents the event accepted value.
    /// </summary>
    public const string EventAccepted = "EventBus.EventAccepted";
    /// <summary>
    /// Represents the event rejected value.
    /// </summary>
    public const string EventRejected = "EventBus.EventRejected";
    /// <summary>
    /// Represents the event contract rejected value.
    /// </summary>
    public const string EventContractRejected = "EventBus.EventContractRejected";
    /// <summary>
    /// Represents the event payload projection failed value.
    /// </summary>
    public const string EventPayloadProjectionFailed = "EventBus.EventPayloadProjectionFailed";
    /// <summary>
    /// Represents the event dropped value.
    /// </summary>
    public const string EventDropped = "EventBus.EventDropped";
    /// <summary>
    /// Represents the event channel backpressure value.
    /// </summary>
    public const string EventChannelBackpressure = "EventBus.EventChannelBackpressure";
    /// <summary>
    /// Represents the event delivery started value.
    /// </summary>
    public const string EventDeliveryStarted = "EventBus.EventDeliveryStarted";
    /// <summary>
    /// Represents the event delivery completed value.
    /// </summary>
    public const string EventDeliveryCompleted = "EventBus.EventDeliveryCompleted";
    /// <summary>
    /// Represents the event delivery failed value.
    /// </summary>
    public const string EventDeliveryFailed = "EventBus.EventDeliveryFailed";
    /// <summary>
    /// Represents the event delivery cancelled value.
    /// </summary>
    public const string EventDeliveryCancelled = "EventBus.EventDeliveryCancelled";
    /// <summary>
    /// Represents the event delivery timed out value.
    /// </summary>
    public const string EventDeliveryTimedOut = "EventBus.EventDeliveryTimedOut";
    /// <summary>
    /// Represents the event subscription disabled value.
    /// </summary>
    public const string EventSubscriptionDisabled = "EventBus.EventSubscriptionDisabled";
    /// <summary>
    /// Represents the event subscription added value.
    /// </summary>
    public const string EventSubscriptionAdded = "EventBus.EventSubscriptionAdded";
    /// <summary>
    /// Represents the event subscription quiescing value.
    /// </summary>
    public const string EventSubscriptionQuiescing = "EventBus.EventSubscriptionQuiescing";
    /// <summary>
    /// Represents the event subscription disposed value.
    /// </summary>
    public const string EventSubscriptionDisposed = "EventBus.EventSubscriptionDisposed";
    /// <summary>
    /// Represents the event subscription termination failed value.
    /// </summary>
    public const string EventSubscriptionTerminationFailed = "EventBus.EventSubscriptionTerminationFailed";
    /// <summary>
    /// Represents the plugin contribution activated value.
    /// </summary>
    public const string PluginContributionActivated = "EventBus.PluginContributionActivated";
    /// <summary>
    /// Represents the plugin contribution rejected value.
    /// </summary>
    public const string PluginContributionRejected = "EventBus.PluginContributionRejected";
    /// <summary>
    /// Represents the plugin contribution quiescing value.
    /// </summary>
    public const string PluginContributionQuiescing = "EventBus.PluginContributionQuiescing";
    /// <summary>
    /// Represents the event plugin drain timed out value.
    /// </summary>
    public const string EventPluginDrainTimedOut = "EventBus.EventPluginDrainTimedOut";
    /// <summary>
    /// Represents the plugin contribution disposed value.
    /// </summary>
    public const string PluginContributionDisposed = "EventBus.PluginContributionDisposed";
}
