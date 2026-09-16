namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for iapplication state.
/// </summary>
public interface IApplicationState
{
    /// <summary>
    /// Gets get&lt;t&gt;.
    /// </summary>
    IReadOnlyState<T> Get<T>(StateKey<T> key);

    /// <summary>
    /// Executes the on change&lt;t&gt; operation.
    /// </summary>
    IStateSubscription OnChange<T>(
        StateKey<T> key,
        Action<StateChangedEventArgs<T>> handler);
}
