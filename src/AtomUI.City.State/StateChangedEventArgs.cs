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
        : this(oldValue, newValue, version, isInitial: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>StateChangedEventArgs</c> type.
    /// </summary>
    public StateChangedEventArgs(
        object? oldValue,
        object? newValue,
        long version,
        bool isInitial)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Version = version;
        IsInitial = isInitial;
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

    /// <summary>
    /// Gets a value indicating whether this notification is the initial value emitted by an observation.
    /// </summary>
    public bool IsInitial { get; }
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
        : this(oldValue, newValue, version, isInitial: false)
    {
    }

    /// <summary>
    /// Executes the state changed event args operation.
    /// </summary>
    public StateChangedEventArgs(
        T oldValue,
        T newValue,
        long version,
        bool isInitial)
        : base(oldValue, newValue, version, isInitial)
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
