namespace AtomUI.City.State;

/// <summary>
/// Represents state changed event args.
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>StateChangedEventArgs</c> type.
    /// </summary>
    public StateChangedEventArgs(
        object? oldValue,
        object? newValue,
        long version)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Version = version;
    }

    /// <summary>
    /// Gets old value.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets new value.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public long Version { get; }
}

/// <summary>
/// Represents state changed event args&lt;t&gt;.
/// </summary>
public sealed class StateChangedEventArgs<T> : StateChangedEventArgs
{
    /// <summary>
    /// Executes the state changed event args operation.
    /// </summary>
    public StateChangedEventArgs(T oldValue, T newValue)
        : this(oldValue, newValue, version: 0)
    {
    }

    /// <summary>
    /// Executes the state changed event args operation.
    /// </summary>
    public StateChangedEventArgs(
        T oldValue,
        T newValue,
        long version)
        : base(oldValue, newValue, version)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// Gets old value.
    /// </summary>
    public new T OldValue { get; }

    /// <summary>
    /// Gets new value.
    /// </summary>
    public new T NewValue { get; }
}
