using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.State;

/// <summary>
/// Represents application state registry.
/// </summary>
public sealed class ApplicationStateRegistry :
    IApplicationState,
    IApplicationStateWriter,
    IStateRegistry
{
    private readonly IHostDiagnostics? _diagnostics;
    private readonly Dictionary<string, StateRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private static readonly StateWriteAuthority HostAuthority = StateWriteAuthority.Host();

    /// <summary>
    /// Initializes a new instance of the <c>ApplicationStateRegistry</c> type.
    /// </summary>
    public ApplicationStateRegistry(IHostDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the add&lt;t&gt; operation.
    /// </summary>
    public void Add<T>(StateDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var alreadyRegistered = false;

        lock (_syncRoot)
        {
            if (_registrations.ContainsKey(definition.Key.Name))
            {
                alreadyRegistered = true;
            }
            else
            {
                _registrations.Add(
                    definition.Key.Name,
                    new StateRegistration<T>(
                        definition,
                        new WritableState<T>(
                            definition.DefaultValue,
                            definition.Comparer,
                            _diagnostics,
                            definition.Key.Name,
                            definition.Access)));
            }
        }

        if (alreadyRegistered)
        {
            WriteAlreadyRegisteredDiagnostic(definition.Key.Name, typeof(T));
            throw new InvalidOperationException($"State '{definition.Key.Name}' is already registered.");
        }
    }

    /// <summary>
    /// Gets get&lt;t&gt;.
    /// </summary>
    public IReadOnlyState<T> Get<T>(StateKey<T> key)
    {
        return GetRegistration<T>(key).State;
    }

    /// <summary>
    /// Executes the get writable&lt;t&gt; operation.
    /// </summary>
    public IWritableState<T> GetWritable<T>(StateKey<T> key)
    {
        var registration = GetRegistration<T>(key);
        ThrowIfWriteDenied(registration, HostAuthority);

        return registration.State;
    }

    /// <summary>
    /// Executes the create writer operation.
    /// </summary>
    public IApplicationStateWriter CreateWriter(StateWriteAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);

        return new AuthorizedApplicationStateWriter(this, authority);
    }

    /// <summary>
    /// Executes the on change&lt;t&gt; operation.
    /// </summary>
    public IStateSubscription OnChange<T>(
        StateKey<T> key,
        Action<StateChangedEventArgs<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Get(key).OnChange(handler);
    }

    /// <summary>
    /// Gets or sets set&lt;t&gt;.
    /// </summary>
    public bool Set<T>(StateKey<T> key, T value)
    {
        return GetWritable(key).SetValue(value);
    }

    /// <summary>
    /// Executes the update&lt;t&gt; operation.
    /// </summary>
    public bool Update<T>(StateKey<T> key, Func<T, T> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        return GetWritable(key).Update(updater);
    }

    /// <summary>
    /// Executes the create snapshot operation.
    /// </summary>
    public StateSnapshot CreateSnapshot()
    {
        StateRegistration[] registrations;

        lock (_syncRoot)
        {
            registrations = _registrations.Values.ToArray();
        }

        var entries = registrations
            .Where(registration => registration.Definition.SnapshotPolicy == StateSnapshotPolicy.Persisted)
            .OrderBy(registration => registration.Definition.Name, StringComparer.Ordinal)
            .Select(registration => registration.CreateSnapshotEntry())
            .ToArray();

        return new StateSnapshot(entries);
    }

    /// <summary>
    /// Executes the restore operation.
    /// </summary>
    public void Restore(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (var entry in snapshot.Entries)
        {
            StateRegistration? registration;

            lock (_syncRoot)
            {
                _registrations.TryGetValue(entry.StateName, out registration);
            }

            if (registration is null)
            {
                WriteSnapshotRestoreFailedDiagnostic(entry, "state is not registered");
                continue;
            }

            registration.Restore(entry, _diagnostics);
        }
    }

    private StateRegistration<T> GetRegistration<T>(StateKey<T> key)
    {
        StateKey<T>.ThrowIfDefault(key, nameof(key));

        StateRegistration? registration;

        lock (_syncRoot)
        {
            _registrations.TryGetValue(key.Name, out registration);
        }

        if (registration is null)
        {
            WriteNotRegisteredDiagnostic(key.Name, typeof(T));
            throw new StateNotRegisteredException(key.Name);
        }

        if (registration is StateRegistration<T> typedRegistration)
        {
            return typedRegistration;
        }

        WriteNotRegisteredDiagnostic(key.Name, typeof(T));
        var message = $"State '{key.Name}' is not registered with value type '{typeof(T).FullName}'.";

        throw new InvalidOperationException(message);
    }

    private IWritableState<T> GetWritable<T>(
        StateKey<T> key,
        StateWriteAuthority authority)
    {
        var registration = GetRegistration<T>(key);
        ThrowIfWriteDenied(registration, authority);

        return registration.State;
    }

    private void ThrowIfWriteDenied<T>(
        StateRegistration<T> registration,
        StateWriteAuthority authority)
    {
        var definition = registration.Definition;
        var allowed = definition.Access switch
        {
            StateAccessPolicy.ReadOnly => false,
            StateAccessPolicy.OwnerWrite =>
                authority.Kind == StateWriteAuthorityKind.Module &&
                string.Equals(authority.ModuleName, definition.OwnerModule, StringComparison.Ordinal),
            StateAccessPolicy.HostWrite => authority.Kind == StateWriteAuthorityKind.Host,
            StateAccessPolicy.AuthorizedWrite =>
                authority.HasCapability(definition.WriteCapability!),
            StateAccessPolicy.PluginIsolated =>
                authority.Kind == StateWriteAuthorityKind.Plugin &&
                string.Equals(authority.PluginId, definition.PluginId, StringComparison.Ordinal),
            _ => false,
        };

        if (!allowed)
        {
            WriteWriteDeniedDiagnostic(
                definition.Key.Name,
                typeof(T),
                definition.Access,
                authority);
            throw new StateAccessDeniedException(definition.Key.Name);
        }
    }

    private void WriteNotRegisteredDiagnostic(string stateName, Type valueType)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.ApplicationStateNotRegistered,
            $"Application state '{stateName}' with value type '{valueType.FullName}' is not registered.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("stateKey", stateName),
                ("valueType", StateDiagnosticContext.TypeName(valueType)))
        });
    }

    private void WriteAlreadyRegisteredDiagnostic(string stateName, Type valueType)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.ApplicationStateAlreadyRegistered,
            $"Application state '{stateName}' with value type '{valueType.FullName}' is already registered.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("stateKey", stateName),
                ("valueType", StateDiagnosticContext.TypeName(valueType)))
        });
    }

    private void WriteWriteDeniedDiagnostic(
        string stateName,
        Type valueType,
        StateAccessPolicy access,
        StateWriteAuthority authority)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.ApplicationStateWriteDenied,
            $"Application state '{stateName}' with value type '{valueType.FullName}' rejected write because access policy is '{access}'.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("accessPolicy", access.ToString()),
                ("writerKind", authority.Kind.ToString()),
                ("writerModule", authority.ModuleName),
                ("writerPlugin", authority.PluginId),
                ("stateKey", stateName),
                ("valueType", StateDiagnosticContext.TypeName(valueType)))
        });
    }

    private void WriteSnapshotRestoreFailedDiagnostic(
        StateSnapshotEntry entry,
        string reason)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.SnapshotRestoreFailed,
            $"State snapshot restore failed for state '{entry.StateName}': {reason}.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("reason", reason),
                ("stateKey", entry.StateName),
                ("valueType", StateDiagnosticContext.TypeName(entry.ValueType)))
        });
    }

    private abstract class StateRegistration
    {
        protected StateRegistration(StateDefinition definition)
        {
            Definition = definition;
        }

        public StateDefinition Definition { get; }

        public abstract StateSnapshotEntry CreateSnapshotEntry();

        public abstract void Restore(
            StateSnapshotEntry entry,
            IHostDiagnostics? diagnostics);
    }

    private sealed class StateRegistration<T> : StateRegistration
    {
        public StateRegistration(
            StateDefinition<T> definition,
            WritableState<T> state)
            : base(definition)
        {
            Definition = definition;
            State = state;
        }

        public new StateDefinition<T> Definition { get; }

        public WritableState<T> State { get; }

        public override StateSnapshotEntry CreateSnapshotEntry()
        {
            var snapshot = State.CaptureSnapshot();

            return new StateSnapshotEntry(
                Definition.Key.Name,
                typeof(T),
                snapshot.Value,
                snapshot.Version,
                Definition.SchemaVersion,
                Definition.OwnerModule,
                Definition.PluginId,
                Definition.Lifetime);
        }

        public override void Restore(
            StateSnapshotEntry entry,
            IHostDiagnostics? diagnostics)
        {
            if (!string.Equals(entry.PluginId, Definition.PluginId, StringComparison.Ordinal))
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"plugin id '{entry.PluginId ?? "<none>"}' does not match expected plugin id '{Definition.PluginId ?? "<none>"}'");
                return;
            }

            if (!string.Equals(entry.OwnerModule, Definition.OwnerModule, StringComparison.Ordinal))
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"owner module '{entry.OwnerModule ?? "<none>"}' does not match expected owner module '{Definition.OwnerModule ?? "<none>"}'");
                return;
            }

            if (entry.SchemaVersion != Definition.SchemaVersion)
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"schema version '{entry.SchemaVersion}' does not match expected schema version '{Definition.SchemaVersion}'");
                return;
            }

            if (entry.Lifetime != Definition.Lifetime)
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"state lifetime '{entry.Lifetime}' does not match expected state lifetime '{Definition.Lifetime}'");
                return;
            }

            if (Definition.SnapshotPolicy != StateSnapshotPolicy.Persisted)
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"snapshot policy '{Definition.SnapshotPolicy}' does not allow restore");
                return;
            }

            if (entry.ValueType != typeof(T))
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"value type '{entry.ValueType.FullName}' does not match expected value type '{typeof(T).FullName}'");
                return;
            }

            if (entry.Value is null)
            {
                if (default(T) is null)
                {
                    State.Restore(default!, entry.Version);
                    return;
                }

                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"null value cannot be restored as non-nullable value type '{typeof(T).FullName}'");
                return;
            }

            if (entry.Value is T value)
            {
                State.Restore(value, entry.Version);
            }
            else
            {
                WriteRestoreFailedDiagnostic(
                    diagnostics,
                    entry,
                    $"value type '{entry.ValueType.FullName}' cannot be restored as '{typeof(T).FullName}'");
            }
        }

        private void WriteRestoreFailedDiagnostic(
            IHostDiagnostics? diagnostics,
            StateSnapshotEntry entry,
            string reason)
        {
            diagnostics?.Write(new HostDiagnosticRecord(
                StateDiagnosticIds.SnapshotRestoreFailed,
                $"State snapshot restore failed for state '{entry.StateName}': {reason}.",
                HostDiagnosticSeverity.Warning)
            {
                Context = StateDiagnosticContext.Create(
                    ("reason", reason),
                    ("stateKey", entry.StateName),
                    ("valueType", StateDiagnosticContext.TypeName(entry.ValueType)))
            });
        }
    }

    private sealed class AuthorizedApplicationStateWriter : IApplicationStateWriter
    {
        private readonly ApplicationStateRegistry _registry;
        private readonly StateWriteAuthority _authority;

        public AuthorizedApplicationStateWriter(
            ApplicationStateRegistry registry,
            StateWriteAuthority authority)
        {
            _registry = registry;
            _authority = authority;
        }

        public IWritableState<T> GetWritable<T>(StateKey<T> key)
        {
            return _registry.GetWritable(key, _authority);
        }

        public bool Set<T>(StateKey<T> key, T value)
        {
            return GetWritable(key).SetValue(value);
        }

        public bool Update<T>(StateKey<T> key, Func<T, T> updater)
        {
            ArgumentNullException.ThrowIfNull(updater);
            return GetWritable(key).Update(updater);
        }
    }
}
