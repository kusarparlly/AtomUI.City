namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event channel&lt;tevent&gt;.
/// </summary>
public readonly record struct EventChannel<TEvent>
{
    /// <summary>
    /// Represents the default name value.
    /// </summary>
    public const string DefaultName = "default";

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventChannel<TEvent> Default { get; } = new(DefaultName);

    /// <summary>
    /// Executes the event channel operation.
    /// </summary>
    public EventChannel(string name)
    {
        Name = ValidateName(name);
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    internal static void ThrowIfDefault(EventChannel<TEvent> channel, string paramName)
    {
        if (channel.Name is null)
        {
            throw new ArgumentException("Event channel must be created before use.", paramName);
        }
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal) || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Event channel name cannot contain leading/trailing whitespace or control characters.",
                nameof(name));
        }

        return name;
    }
}
