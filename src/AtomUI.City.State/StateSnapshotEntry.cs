namespace AtomUI.City.State;

/// <summary>
/// Represents state snapshot entry.
/// </summary>
public sealed record StateSnapshotEntry
{
    private string _stateName = null!;
    private Type _valueType = null!;
    private long _version;
    private int _schemaVersion;
    private StateLifetime _lifetime;

    /// <summary>
    /// Initializes a new instance of the <c>StateSnapshotEntry</c> type.
    /// </summary>
    public StateSnapshotEntry(
        string stateName,
        Type valueType,
        object? value,
        long version,
        int schemaVersion,
        string? ownerModule,
        string? pluginId)
        : this(
            stateName,
            valueType,
            value,
            version,
            schemaVersion,
            ownerModule,
            pluginId,
            StateLifetime.Application)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>StateSnapshotEntry</c> type.
    /// </summary>
    public StateSnapshotEntry(
        string stateName,
        Type valueType,
        object? value,
        long version,
        int schemaVersion,
        string? ownerModule,
        string? pluginId,
        StateLifetime lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(valueType);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "State snapshot version must be greater than or equal to 0.");
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "State snapshot schema version must be greater than or equal to 1.");
        }

        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "State lifetime is not supported.");
        }

        StateName = stateName;
        ValueType = valueType;
        Value = value;
        Version = version;
        SchemaVersion = schemaVersion;
        OwnerModule = ownerModule;
        PluginId = pluginId;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Represents the state name value.
    /// </summary>
    public string StateName
    {
        get => _stateName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            _stateName = value;
        }
    }

    /// <summary>
    /// Represents the value type value.
    /// </summary>
    public Type ValueType
    {
        get => _valueType;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _valueType = value;
        }
    }

    /// <summary>
    /// Gets or sets value.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Represents the version value.
    /// </summary>
    public long Version
    {
        get => _version;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State snapshot version must be greater than or equal to 0.");
            }

            _version = value;
        }
    }

    /// <summary>
    /// Represents the schema version value.
    /// </summary>
    public int SchemaVersion
    {
        get => _schemaVersion;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State snapshot schema version must be greater than or equal to 1.");
            }

            _schemaVersion = value;
        }
    }

    /// <summary>
    /// Gets or sets owner module.
    /// </summary>
    public string? OwnerModule { get; init; }

    /// <summary>
    /// Gets or sets plugin id.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Represents the lifetime value.
    /// </summary>
    public StateLifetime Lifetime
    {
        get => _lifetime;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "State lifetime is not supported.");
            }

            _lifetime = value;
        }
    }

    /// <summary>
    /// Gets or sets timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
