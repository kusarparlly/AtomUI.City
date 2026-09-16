namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate collection&lt;tkey, titem&gt;.
/// </summary>
public interface IStateCollection<TKey, TItem> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Gets version.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Gets items.
    /// </summary>
    IReadOnlyDictionary<TKey, TItem> Items { get; }

    /// <summary>
    /// Executes the try get item version operation.
    /// </summary>
    bool TryGetItemVersion(TKey key, out long version);

    /// <summary>
    /// Executes the create snapshot operation.
    /// </summary>
    StateCollectionSnapshot<TKey, TItem> CreateSnapshot();

    /// <summary>
    /// Executes the restore snapshot operation.
    /// </summary>
    bool RestoreSnapshot(StateCollectionSnapshot<TKey, TItem> snapshot);

    /// <summary>
    /// Executes the add or update operation.
    /// </summary>
    bool AddOrUpdate(TKey key, TItem item);

    /// <summary>
    /// Executes the add or update range operation.
    /// </summary>
    bool AddOrUpdateRange(IEnumerable<KeyValuePair<TKey, TItem>> items);

    /// <summary>
    /// Executes the remove operation.
    /// </summary>
    bool Remove(TKey key);

    /// <summary>
    /// Executes the clear operation.
    /// </summary>
    bool Clear();

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(Action<StateCollectionChangedEventArgs<TKey, TItem>> handler);

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    IStateSubscription OnChange(
        Action<StateCollectionChangedEventArgs<TKey, TItem>> handler,
        StateSubscriptionOptions options);
}
