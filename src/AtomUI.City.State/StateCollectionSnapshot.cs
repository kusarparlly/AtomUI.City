namespace AtomUI.City.State;

/// <summary>
/// Represents state collection snapshot&lt;tkey, titem&gt;.
/// </summary>
public sealed class StateCollectionSnapshot<TKey, TItem>
    where TKey : notnull
{
    /// <summary>
    /// Executes the state collection snapshot operation.
    /// </summary>
    public StateCollectionSnapshot(
        long collectionVersion,
        IReadOnlyList<StateCollectionSnapshotEntry<TKey, TItem>> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (collectionVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collectionVersion),
                collectionVersion,
                "State collection snapshot version must be greater than or equal to 0.");
        }

        var snapshotItems = items.ToArray();

        if (snapshotItems.Any(item => item is null))
        {
            throw new ArgumentException("State collection snapshot items must not contain null.", nameof(items));
        }

        CollectionVersion = collectionVersion;
        Items = Array.AsReadOnly(snapshotItems);
    }

    /// <summary>
    /// Gets collection version.
    /// </summary>
    public long CollectionVersion { get; }

    /// <summary>
    /// Gets item count.
    /// </summary>
    public int ItemCount => Items.Count;

    /// <summary>
    /// Gets items.
    /// </summary>
    public IReadOnlyList<StateCollectionSnapshotEntry<TKey, TItem>> Items { get; }
}
