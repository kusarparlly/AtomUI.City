namespace AtomUI.City.State;

/// <summary>
/// Represents either the initial collection snapshot or a subsequent collection change.
/// </summary>
public sealed class StateCollectionObservation<TKey, TItem>
    where TKey : notnull
{
    private StateCollectionObservation(
        bool isInitial,
        long version,
        StateCollectionSnapshot<TKey, TItem>? snapshot,
        IReadOnlyList<StateCollectionChange<TKey, TItem>> changes)
    {
        IsInitial = isInitial;
        Version = version;
        Snapshot = snapshot;
        Changes = changes;
    }

    /// <summary>
    /// Gets a value indicating whether this observation contains the initial snapshot.
    /// </summary>
    public bool IsInitial { get; }

    /// <summary>
    /// Gets the collection version represented by this observation.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the initial collection snapshot, or <see langword="null"/> for a change observation.
    /// </summary>
    public StateCollectionSnapshot<TKey, TItem>? Snapshot { get; }

    /// <summary>
    /// Gets the changes, or an empty list for the initial snapshot.
    /// </summary>
    public IReadOnlyList<StateCollectionChange<TKey, TItem>> Changes { get; }

    internal static StateCollectionObservation<TKey, TItem> Initial(
        StateCollectionSnapshot<TKey, TItem> snapshot)
    {
        return new StateCollectionObservation<TKey, TItem>(
            isInitial: true,
            snapshot.CollectionVersion,
            snapshot,
            Array.Empty<StateCollectionChange<TKey, TItem>>());
    }

    internal static StateCollectionObservation<TKey, TItem> Changed(
        StateCollectionChangedEventArgs<TKey, TItem> args)
    {
        return new StateCollectionObservation<TKey, TItem>(
            isInitial: false,
            args.Version,
            snapshot: null,
            args.Changes);
    }
}
