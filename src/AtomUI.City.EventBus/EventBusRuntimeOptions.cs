namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus runtime options.
/// </summary>
public sealed class EventBusRuntimeOptions
{
    /// <summary>
    /// Represents the default maximum channel runtimes value.
    /// </summary>
    public const int DefaultMaximumChannelRuntimes = 256;
    /// <summary>
    /// Represents the maximum allowed channel runtimes value.
    /// </summary>
    public const int MaximumAllowedChannelRuntimes = 65_536;

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventBusRuntimeOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets maximum channel runtimes.
    /// </summary>
    public int MaximumChannelRuntimes { get; init; } = DefaultMaximumChannelRuntimes;

    internal void Validate()
    {
        if (MaximumChannelRuntimes is <= 0 or > MaximumAllowedChannelRuntimes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumChannelRuntimes),
                MaximumChannelRuntimes,
                $"EventBus maximum channel runtimes must be between 1 and {MaximumAllowedChannelRuntimes}.");
        }
    }
}
