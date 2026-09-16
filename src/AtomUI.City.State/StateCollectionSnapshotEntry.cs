namespace AtomUI.City.State;

/// <summary>
/// Represents state collection snapshot entry&lt;tkey, titem&gt;.
/// </summary>
public sealed record StateCollectionSnapshotEntry<TKey, TItem>
    where TKey : notnull
{
    private TKey _key = default!;
    private long _itemVersion;

    /// <summary>
    /// Executes the state collection snapshot entry operation.
    /// </summary>
    public StateCollectionSnapshotEntry(TKey Key, TItem Item, long ItemVersion)
    {
        ArgumentNullException.ThrowIfNull(Key);

        if (ItemVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ItemVersion),
                ItemVersion,
                "State collection snapshot item version must be greater than or equal to 0.");
        }

        this.Key = Key;
        this.Item = Item;
        this.ItemVersion = ItemVersion;
    }

    /// <summary>
    /// Represents the key value.
    /// </summary>
    public TKey Key
    {
        get => _key;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _key = value;
        }
    }

    /// <summary>
    /// Gets or sets item.
    /// </summary>
    public TItem Item { get; init; }

    /// <summary>
    /// Represents the item version value.
    /// </summary>
    public long ItemVersion
    {
        get => _itemVersion;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State collection snapshot item version must be greater than or equal to 0.");
            }

            _itemVersion = value;
        }
    }
}
