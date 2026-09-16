namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event channel descriptor.
/// </summary>
public sealed class EventChannelDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventChannelDescriptor"/> type.
    /// </summary>
    public EventChannelDescriptor(
        Type eventType,
        string channelName,
        EventChannelOptions options)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        ChannelName = new EventChannel<object>(channelName).Name;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Options.Validate();
    }

    /// <summary>
    /// Gets event type.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// Gets channel name.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// Gets options.
    /// </summary>
    public EventChannelOptions Options { get; }

    /// <summary>
    /// Executes the create&lt;tevent&gt; operation.
    /// </summary>
    public static EventChannelDescriptor Create<TEvent>(
        EventChannel<TEvent> channel,
        EventChannelOptions options)
    {
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        return new EventChannelDescriptor(typeof(TEvent), channel.Name, options);
    }
}
