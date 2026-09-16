namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for iwritable state&lt;t&gt;.
/// </summary>
public interface IWritableState<T> : IReadOnlyState<T>
{
    /// <summary>
    /// Occurs when changed.
    /// </summary>
    event EventHandler<StateChangedEventArgs<T>>? Changed;

    /// <summary>
    /// Executes the set value operation.
    /// </summary>
    bool SetValue(T value);

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    bool Update(Func<T, T> updater);

    /// <summary>
    /// Gets or sets set.
    /// </summary>
    void Set(T value);
}
