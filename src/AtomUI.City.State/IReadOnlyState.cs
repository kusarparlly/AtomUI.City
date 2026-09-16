namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for iread only state.
/// </summary>
public interface IReadOnlyState
{
    /// <summary>
    /// Gets value.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Gets value type.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(Action<StateChangedEventArgs> handler);

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(
        Action<StateChangedEventArgs> handler,
        StateSubscriptionOptions options);
}

/// <summary>
/// Defines the contract for iread only state&lt;t&gt;.
/// </summary>
public interface IReadOnlyState<T> : IReadOnlyState, IStateValue<T>
{
    /// <summary>
    /// Gets value.
    /// </summary>
    new T Value { get; }

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(Action<StateChangedEventArgs<T>> handler);

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(
        Action<StateChangedEventArgs<T>> handler,
        StateSubscriptionOptions options);
}
