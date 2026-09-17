namespace AtomUI.City.State;

/// <summary>
/// Represents state definition.
/// </summary>
public abstract class StateDefinition
{
    /// <summary>
    /// Executes the state definition operation.
    /// </summary>
    internal StateDefinition(
        string name,
        Type valueType,
        StateLifetime lifetime,
        StateAccessPolicy access,
        StateSnapshotPolicy snapshotPolicy,
        int schemaVersion,
        string? ownerModule,
        string? pluginId,
        string? writeCapability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(valueType);

        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "State lifetime is not supported.");
        }

        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "State access policy is not supported.");
        }

        if (!Enum.IsDefined(snapshotPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotPolicy), snapshotPolicy, "State snapshot policy is not supported.");
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "State schema version must be greater than or equal to 1.");
        }

        if (access == StateAccessPolicy.OwnerWrite && string.IsNullOrWhiteSpace(ownerModule))
        {
            throw new ArgumentException("OwnerWrite state requires an owner module.", nameof(ownerModule));
        }

        if (access == StateAccessPolicy.AuthorizedWrite && string.IsNullOrWhiteSpace(writeCapability))
        {
            throw new ArgumentException("AuthorizedWrite state requires a write capability.", nameof(writeCapability));
        }

        if (access == StateAccessPolicy.PluginIsolated && string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("PluginIsolated state requires a plugin id.", nameof(pluginId));
        }

        Name = name;
        ValueType = valueType;
        Lifetime = lifetime;
        Access = access;
        SnapshotPolicy = snapshotPolicy;
        SchemaVersion = schemaVersion;
        OwnerModule = ownerModule;
        PluginId = pluginId;
        WriteCapability = writeCapability;
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets value type.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets lifetime.
    /// </summary>
    public StateLifetime Lifetime { get; }

    /// <summary>
    /// Gets access.
    /// </summary>
    public StateAccessPolicy Access { get; }

    /// <summary>
    /// Gets snapshot policy.
    /// </summary>
    public StateSnapshotPolicy SnapshotPolicy { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets owner module.
    /// </summary>
    public string? OwnerModule { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets write capability.
    /// </summary>
    public string? WriteCapability { get; }

    /// <summary>
    /// Executes the create&lt;t&gt; operation.
    /// </summary>
    public static StateDefinition<T> Create<T>(
        StateKey<T> key,
        T defaultValue,
        StateLifetime lifetime = StateLifetime.Application,
        StateAccessPolicy access = StateAccessPolicy.HostWrite,
        StateSnapshotPolicy snapshotPolicy = StateSnapshotPolicy.Transient,
        int schemaVersion = 1,
        string? ownerModule = null,
        string? pluginId = null,
        IEqualityComparer<T>? comparer = null,
        string? writeCapability = null)
    {
        return StateDefinition<T>.Create(
            key,
            defaultValue,
            lifetime,
            access,
            snapshotPolicy,
            schemaVersion,
            ownerModule,
            pluginId,
            comparer,
            writeCapability);
    }
}

/// <summary>
/// Represents state definition&lt;t&gt;.
/// </summary>
public sealed class StateDefinition<T> : StateDefinition
{
    private StateDefinition(
        StateKey<T> key,
        T defaultValue,
        StateLifetime lifetime,
        StateAccessPolicy access,
        StateSnapshotPolicy snapshotPolicy,
        int schemaVersion,
        string? ownerModule,
        string? pluginId,
        IEqualityComparer<T>? comparer,
        string? writeCapability)
        : base(
            key.Name,
            typeof(T),
            lifetime,
            access,
            snapshotPolicy,
            schemaVersion,
            ownerModule,
            pluginId,
            writeCapability)
    {
        Key = key;
        DefaultValue = defaultValue;
        Comparer = comparer;
    }

    /// <summary>
    /// Gets key.
    /// </summary>
    public StateKey<T> Key { get; }

    /// <summary>
    /// Gets default value.
    /// </summary>
    public T DefaultValue { get; }

    /// <summary>
    /// Gets comparer.
    /// </summary>
    public IEqualityComparer<T>? Comparer { get; }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    internal static StateDefinition<T> Create(
        StateKey<T> key,
        T defaultValue,
        StateLifetime lifetime = StateLifetime.Application,
        StateAccessPolicy access = StateAccessPolicy.HostWrite,
        StateSnapshotPolicy snapshotPolicy = StateSnapshotPolicy.Transient,
        int schemaVersion = 1,
        string? ownerModule = null,
        string? pluginId = null,
        IEqualityComparer<T>? comparer = null,
        string? writeCapability = null)
    {
        StateKey<T>.ThrowIfDefault(key, nameof(key));

        return new StateDefinition<T>(
            key,
            defaultValue,
            lifetime,
            access,
            snapshotPolicy,
            schemaVersion,
            ownerModule,
            pluginId,
            comparer,
            writeCapability);
    }
}
