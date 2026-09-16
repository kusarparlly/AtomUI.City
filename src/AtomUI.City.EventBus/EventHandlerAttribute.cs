namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventHandlerAttribute : Attribute
{
    private string _channelName = EventChannel<object>.DefaultName;
    private EventDispatchPolicy _dispatchPolicy = EventDispatchPolicy.Serialized;
    private EventDispatchMode _dispatchMode = EventDispatchMode.InlineIfAllowed;
    private EventErrorPolicy _errorPolicy = EventErrorPolicy.ContinueAndReport;
    private int _handlerTimeoutMilliseconds = 30_000;
    private int _disableSubscriptionAfterFailures = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventHandlerAttribute"/> type.
    /// </summary>
    public EventHandlerAttribute(Type ownerModuleType)
    {
        OwnerModuleType = EventAttributeValidation.ValidateOwner(ownerModuleType, nameof(ownerModuleType));
    }

    /// <summary>
    /// Gets owner module type.
    /// </summary>
    public Type OwnerModuleType { get; }

    /// <summary>
    /// Represents the channel name value.
    /// </summary>
    public string ChannelName
    {
        get => _channelName;
        init => _channelName = EventAttributeValidation.ValidateName(value, nameof(value));
    }

    /// <summary>
    /// Represents the dispatch policy value.
    /// </summary>
    public EventDispatchPolicy DispatchPolicy
    {
        get => _dispatchPolicy;
        init => _dispatchPolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Represents the dispatch mode value.
    /// </summary>
    public EventDispatchMode DispatchMode
    {
        get => _dispatchMode;
        init => _dispatchMode = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Represents the error policy value.
    /// </summary>
    public EventErrorPolicy ErrorPolicy
    {
        get => _errorPolicy;
        init => _errorPolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Represents the handler timeout milliseconds value.
    /// </summary>
    public int HandlerTimeoutMilliseconds
    {
        get => _handlerTimeoutMilliseconds;
        init => _handlerTimeoutMilliseconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Represents the disable subscription after failures value.
    /// </summary>
    public int DisableSubscriptionAfterFailures
    {
        get => _disableSubscriptionAfterFailures;
        init => _disableSubscriptionAfterFailures = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
