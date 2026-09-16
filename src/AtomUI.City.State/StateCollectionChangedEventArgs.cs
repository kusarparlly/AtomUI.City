namespace AtomUI.City.State;

/// <summary>
/// Represents state collection changed event args&lt;tkey, titem&gt;.
/// </summary>
public sealed class StateCollectionChangedEventArgs<TKey, TItem> : StateChangedEventArgs
    where TKey : notnull
{
    /// <summary>
    /// Executes the state collection changed event args operation.
    /// </summary>
    public StateCollectionChangedEventArgs(StateCollectionChange<TKey, TItem> change)
        : this(CreateChangeList(change))
    {
    }

    /// <summary>
    /// Executes the state collection changed event args operation.
    /// </summary>
    public StateCollectionChangedEventArgs(IReadOnlyList<StateCollectionChange<TKey, TItem>> changes)
        : base(oldValue: null, newValue: null, GetCollectionVersion(changes))
    {
        var snapshotChanges = changes.ToArray();

        if (snapshotChanges.Any(change => change is null))
        {
            throw new ArgumentException("State collection changes must not contain null.", nameof(changes));
        }

        Changes = Array.AsReadOnly(snapshotChanges);
    }

    /// <summary>
    /// Gets change.
    /// </summary>
    public StateCollectionChange<TKey, TItem> Change => Changes[0];

    /// <summary>
    /// Gets changes.
    /// </summary>
    public IReadOnlyList<StateCollectionChange<TKey, TItem>> Changes { get; }

    private static long GetCollectionVersion(IReadOnlyList<StateCollectionChange<TKey, TItem>> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            throw new ArgumentException("State collection change list cannot be empty.", nameof(changes));
        }

        var lastChange = changes[^1]
            ?? throw new ArgumentException("State collection changes must not contain null.", nameof(changes));

        return lastChange.CollectionVersion;
    }

    private static StateCollectionChange<TKey, TItem>[] CreateChangeList(
        StateCollectionChange<TKey, TItem> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return [change];
    }
}
