namespace AtomUI.City.State;

/// <summary>
/// Represents state collection change&lt;tkey, titem&gt;.
/// </summary>
public sealed record StateCollectionChange<TKey, TItem>
    where TKey : notnull
{
    private StateCollectionChangeKind _kind;
    private TKey _key = default!;
    private long _collectionVersion;
    private long _itemVersion;

    /// <summary>
    /// Executes the state collection change operation.
    /// </summary>
    public StateCollectionChange(
        StateCollectionChangeKind Kind,
        TKey Key,
        bool HasOldItem,
        TItem? OldItem,
        bool HasNewItem,
        TItem? NewItem,
        long CollectionVersion,
        long ItemVersion)
    {
        ArgumentNullException.ThrowIfNull(Key);

        if (Kind < StateCollectionChangeKind.Added || Kind > StateCollectionChangeKind.Reset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Kind),
                Kind,
                "State collection change kind is not supported.");
        }

        if (CollectionVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CollectionVersion),
                CollectionVersion,
                "State collection version must be greater than or equal to 0.");
        }

        if (ItemVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ItemVersion),
                ItemVersion,
                "State collection item version must be greater than or equal to 0.");
        }

        this.Kind = Kind;
        this.Key = Key;
        this.HasOldItem = HasOldItem;
        this.OldItem = OldItem;
        this.HasNewItem = HasNewItem;
        this.NewItem = NewItem;
        this.CollectionVersion = CollectionVersion;
        this.ItemVersion = ItemVersion;
    }

    /// <summary>
    /// Represents the kind value.
    /// </summary>
    public StateCollectionChangeKind Kind
    {
        get => _kind;
        init
        {
            if (value < StateCollectionChangeKind.Added || value > StateCollectionChangeKind.Reset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State collection change kind is not supported.");
            }

            _kind = value;
        }
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
    /// Gets or sets has old item.
    /// </summary>
    public bool HasOldItem { get; init; }

    /// <summary>
    /// Gets or sets old item.
    /// </summary>
    public TItem? OldItem { get; init; }

    /// <summary>
    /// Gets or sets has new item.
    /// </summary>
    public bool HasNewItem { get; init; }

    /// <summary>
    /// Gets or sets new item.
    /// </summary>
    public TItem? NewItem { get; init; }

    /// <summary>
    /// Represents the collection version value.
    /// </summary>
    public long CollectionVersion
    {
        get => _collectionVersion;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State collection version must be greater than or equal to 0.");
            }

            _collectionVersion = value;
        }
    }

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
                    "State collection item version must be greater than or equal to 0.");
            }

            _itemVersion = value;
        }
    }
}
