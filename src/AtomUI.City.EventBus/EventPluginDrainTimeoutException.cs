namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event plugin drain timeout exception.
/// </summary>
public sealed class EventPluginDrainTimeoutException : TimeoutException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPluginDrainTimeoutException"/> type.
    /// </summary>
    public EventPluginDrainTimeoutException(
        string pluginId,
        TimeSpan drainTimeout,
        int activeOperations,
        int activeSubscriptions,
        int pendingRegistrations)
        : base(CreateMessage(pluginId, drainTimeout, activeOperations, activeSubscriptions, pendingRegistrations))
    {
        PluginId = EventAttributeValidation.ValidateName(pluginId, nameof(pluginId));
        if (drainTimeout <= TimeSpan.Zero || drainTimeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(drainTimeout), drainTimeout, "Drain timeout is outside the supported range.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(activeOperations);
        ArgumentOutOfRangeException.ThrowIfNegative(activeSubscriptions);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingRegistrations);
        DrainTimeout = drainTimeout;
        ActiveOperations = activeOperations;
        ActiveSubscriptions = activeSubscriptions;
        PendingRegistrations = pendingRegistrations;
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets drain timeout.
    /// </summary>
    public TimeSpan DrainTimeout { get; }

    /// <summary>
    /// Gets active operations.
    /// </summary>
    public int ActiveOperations { get; }

    /// <summary>
    /// Gets active subscriptions.
    /// </summary>
    public int ActiveSubscriptions { get; }

    /// <summary>
    /// Gets pending registrations.
    /// </summary>
    public int PendingRegistrations { get; }

    private static string CreateMessage(
        string pluginId,
        TimeSpan drainTimeout,
        int activeOperations,
        int activeSubscriptions,
        int pendingRegistrations)
    {
        return $"Plugin EventBus contribution '{pluginId}' did not drain within {drainTimeout}. " +
               $"Active operations: {activeOperations}; active subscriptions: {activeSubscriptions}; " +
               $"pending registrations: {pendingRegistrations}.";
    }
}
