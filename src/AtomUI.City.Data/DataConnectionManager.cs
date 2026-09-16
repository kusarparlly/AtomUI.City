using System.Runtime.ExceptionServices;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data connection manager.
/// </summary>
public sealed class DataConnectionManager
{
    private readonly Dictionary<string, ConnectionEntry> _connections = new(StringComparer.Ordinal);
    private readonly HashSet<DataConnectionOwner> _stoppedOwners = [];
    private readonly IDataDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();
    private bool _acceptingRegistrations = true;
    private long _nextRegistrationOrder;

    /// <summary>
    /// Initializes a new instance of the <c>DataConnectionManager</c> type.
    /// </summary>
    public DataConnectionManager(IDataDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    public DataResult<DataConnectionRegistration> Register(IDataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var connectionId = connection.ConnectionId;
        var owner = connection.Owner;
        var state = connection.State;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(connection),
                state,
                "Data connection state is not supported.");
        }

        if (owner == DataConnectionOwner.None)
        {
            return RejectRegistration(
                connectionId,
                "Long-running data connections must declare an owner.");
        }

        ConnectionEntry entry;
        string? rejection = null;
        lock (_syncRoot)
        {
            if (!_acceptingRegistrations || _stoppedOwners.Contains(owner))
            {
                rejection = $"Data connection owner '{owner.Kind}:{owner.Id}' is stopped.";
            }
            else if (_connections.ContainsKey(connectionId))
            {
                rejection = $"Data connection id '{connectionId}' is already registered.";
            }

            entry = new ConnectionEntry(
                connection,
                connectionId,
                owner,
                ++_nextRegistrationOrder,
                state);
            if (rejection is null)
            {
                _connections.Add(connectionId, entry);
            }
        }

        if (rejection is not null)
        {
            return RejectRegistration(connectionId, rejection);
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ConnectionRegistered,
            $"Data connection '{connectionId}' registered.",
            DataDiagnosticSeverity.Info));

        return DataResult<DataConnectionRegistration>.Success(
            new DataConnectionRegistration(connection, () => RevokeAsync(entry)));
    }

    /// <summary>
    /// Executes the start owner async operation.
    /// </summary>
    public async ValueTask StartOwnerAsync(
        DataConnectionOwner owner,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        var connections = GetOwnerConnectionsForStart(owner);
        var started = new List<ConnectionEntry>(connections.Length);

        try
        {
            foreach (var connection in connections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await StartConnectionAsync(connection, cancellationToken).ConfigureAwait(false))
                {
                    started.Add(connection);
                }
            }
        }
        catch (Exception startFailure)
        {
            var failures = new List<Exception> { startFailure };
            for (var index = started.Count - 1; index >= 0; index--)
            {
                try
                {
                    await StopConnectionAsync(started[index], CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception rollbackFailure)
                {
                    failures.Add(rollbackFailure);
                }
            }

            ThrowFailures(failures);
        }
    }

    /// <summary>
    /// Executes the stop owner async operation.
    /// </summary>
    public async ValueTask StopOwnerAsync(
        DataConnectionOwner owner,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);

        ConnectionEntry[] connections;
        lock (_syncRoot)
        {
            _stoppedOwners.Add(owner);
            connections = _connections.Values
                .Where(entry => entry.Owner == owner)
                .OrderByDescending(entry => entry.RegistrationOrder)
                .ToArray();
        }

        try
        {
            await StopEntriesAsync(connections, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RemoveTerminated(connections);
        }
    }

    /// <summary>
    /// Executes the stop all async operation.
    /// </summary>
    public async ValueTask StopAllAsync(CancellationToken cancellationToken = default)
    {
        ConnectionEntry[] connections;
        lock (_syncRoot)
        {
            _acceptingRegistrations = false;
            connections = _connections.Values
                .OrderByDescending(entry => entry.RegistrationOrder)
                .ToArray();
            foreach (var entry in connections)
            {
                _stoppedOwners.Add(entry.Owner);
            }
        }

        try
        {
            await StopEntriesAsync(connections, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RemoveTerminated(connections);
        }
    }

    private DataResult<DataConnectionRegistration> RejectRegistration(
        string connectionId,
        string message)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ConnectionRegistrationRejected,
            $"Data connection '{connectionId}' registration rejected. {message}",
            DataDiagnosticSeverity.Warning,
            ErrorKind: DataErrorKind.PolicyRejected));

        return DataResult<DataConnectionRegistration>.Failed(
            new DataError(DataErrorKind.PolicyRejected, message));
    }

    private async ValueTask<bool> StartConnectionAsync(
        ConnectionEntry entry,
        CancellationToken cancellationToken)
    {
        DataInvocationGuard.ThrowIfReentrant(entry, entry.ConnectionId, "start");

        Task operationTask;
        TaskCompletionSource? completion = null;
        lock (entry.SyncRoot)
        {
            if (entry.Terminated
                || entry.Retiring
                || entry.Started
                || entry.StopTask is not null)
            {
                return false;
            }

            if (entry.StartTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                entry.StartTask = completion.Task;
            }

            operationTask = entry.StartTask;
        }

        if (completion is not null)
        {
            _ = CompleteStartAsync(entry, completion, cancellationToken);
            await operationTask.ConfigureAwait(false);
            return true;
        }

        await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async Task CompleteStartAsync(
        ConnectionEntry entry,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        try
        {
            using var invocation = DataInvocationGuard.Enter(entry, "start");
            ValueTask startOperation;
            using (DataInvocationGuard.EnterSynchronous(entry, "start"))
            {
                startOperation = entry.Connection.StartAsync(cancellationToken);
            }

            await startOperation.ConfigureAwait(false);

            lock (entry.SyncRoot)
            {
                entry.Started = true;
                entry.StartTask = null;
            }

            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ConnectionStarted,
                $"Data connection '{entry.ConnectionId}' started.",
                DataDiagnosticSeverity.Info));
            completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            ResetStartTask(entry, completion.Task);
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            ResetStartTask(entry, completion.Task);
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ConnectionStartFailed,
                $"Data connection '{entry.ConnectionId}' start failed.",
                DataDiagnosticSeverity.Error,
                ErrorKind: DataErrorKind.ConnectionFailed));
            completion.TrySetException(exception);
        }
    }

    private async ValueTask StopEntriesAsync(
        IReadOnlyList<ConnectionEntry> connections,
        CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        foreach (var connection in connections)
        {
            try
            {
                await StopConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                failures.Add(exception);
                break;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        ThrowFailures(failures);
    }

    private async ValueTask StopConnectionAsync(
        ConnectionEntry entry,
        CancellationToken cancellationToken)
    {
        DataInvocationGuard.ThrowIfReentrant(entry, entry.ConnectionId, "stop");

        Task operationTask;
        TaskCompletionSource? completion = null;
        lock (entry.SyncRoot)
        {
            if (entry.Terminated)
            {
                return;
            }

            if (entry.StopTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                entry.StopTask = completion.Task;
            }

            operationTask = entry.StopTask;
        }

        if (completion is not null)
        {
            _ = CompleteStopAsync(entry, completion, cancellationToken);
            await operationTask.ConfigureAwait(false);
            return;
        }

        await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteStopAsync(
        ConnectionEntry entry,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        try
        {
            Task? startTask;
            lock (entry.SyncRoot)
            {
                startTask = entry.StartTask;
            }

            if (startTask is not null)
            {
                try
                {
                    await startTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    // A failed start may still have acquired resources that StopAsync must release.
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var invocation = DataInvocationGuard.Enter(entry, "stop");
            ValueTask stopOperation;
            using (DataInvocationGuard.EnterSynchronous(entry, "stop"))
            {
                stopOperation = entry.Connection.StopAsync(cancellationToken);
            }

            await stopOperation.ConfigureAwait(false);

            lock (entry.SyncRoot)
            {
                entry.Terminated = true;
                entry.Started = false;
            }

            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ConnectionStopped,
                $"Data connection '{entry.ConnectionId}' stopped.",
                DataDiagnosticSeverity.Info));
            completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            ResetStopTask(entry, completion.Task);
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            ResetStopTask(entry, completion.Task);
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ConnectionStopFailed,
                $"Data connection '{entry.ConnectionId}' stop failed.",
                DataDiagnosticSeverity.Error,
                ErrorKind: DataErrorKind.ConnectionFailed));
            completion.TrySetException(exception);
        }
    }

    private static void ResetStartTask(ConnectionEntry entry, Task operationTask)
    {
        lock (entry.SyncRoot)
        {
            if (ReferenceEquals(entry.StartTask, operationTask))
            {
                entry.StartTask = null;
            }
        }
    }

    private static void ResetStopTask(ConnectionEntry entry, Task operationTask)
    {
        lock (entry.SyncRoot)
        {
            if (ReferenceEquals(entry.StopTask, operationTask))
            {
                entry.StopTask = null;
            }
        }
    }

    private ConnectionEntry[] GetOwnerConnectionsForStart(DataConnectionOwner owner)
    {
        lock (_syncRoot)
        {
            if (!_acceptingRegistrations || _stoppedOwners.Contains(owner))
            {
                throw new InvalidOperationException(
                    $"Data connection owner '{owner.Kind}:{owner.Id}' is stopped.");
            }

            return _connections.Values
                .Where(entry => entry.Owner == owner)
                .OrderBy(entry => entry.RegistrationOrder)
                .ToArray();
        }
    }

    private async ValueTask RevokeAsync(ConnectionEntry entry)
    {
        lock (_syncRoot)
        {
            if (!_connections.TryGetValue(entry.ConnectionId, out var registered)
                || !ReferenceEquals(registered, entry))
            {
                return;
            }

            entry.Retiring = true;
        }

        await StopConnectionAsync(entry, CancellationToken.None).ConfigureAwait(false);
        RemoveTerminated([entry]);
    }

    private void RemoveTerminated(IEnumerable<ConnectionEntry> entries)
    {
        lock (_syncRoot)
        {
            foreach (var entry in entries)
            {
                if (!entry.Terminated)
                {
                    continue;
                }

                if (_connections.TryGetValue(entry.ConnectionId, out var registered)
                    && ReferenceEquals(registered, entry))
                {
                    _connections.Remove(entry.ConnectionId);
                }
            }
        }
    }

    private static void ValidateOwner(DataConnectionOwner owner)
    {
        if (owner == DataConnectionOwner.None)
        {
            throw new ArgumentException("A data connection owner is required.", nameof(owner));
        }
    }

    private static void ThrowFailures(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("One or more data connection lifecycle operations failed.", failures);
    }

    private sealed class ConnectionEntry
    {
        public ConnectionEntry(
            IDataConnection connection,
            string connectionId,
            DataConnectionOwner owner,
            long registrationOrder,
            DataConnectionState state)
        {
            Connection = connection;
            ConnectionId = connectionId;
            Owner = owner;
            RegistrationOrder = registrationOrder;
            Started = state is
                DataConnectionState.Connecting or
                DataConnectionState.Connected or
                DataConnectionState.Reconnecting or
                DataConnectionState.Disconnecting;
            Terminated = state == DataConnectionState.Stopped;
        }

        public IDataConnection Connection { get; }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public long RegistrationOrder { get; }

        public object SyncRoot { get; } = new();

        public Task? StartTask { get; set; }

        public Task? StopTask { get; set; }

        public bool Started { get; set; }

        public bool Terminated { get; set; }

        public bool Retiring { get; set; }
    }

}

/// <summary>
/// Represents data connection registration.
/// </summary>
public sealed class DataConnectionRegistration : IAsyncDisposable
{
    private readonly Func<ValueTask>? _revoke;
    private readonly object _syncRoot = new();
    private Task? _revokeTask;

    /// <summary>
    /// Initializes a new instance of the <c>DataConnectionRegistration</c> type.
    /// </summary>
    [Obsolete(
        "Direct construction creates a detached compatibility handle. " +
        "Use DataConnectionManager.Register to obtain a revocable registration.")]
    public DataConnectionRegistration(IDataConnection connection)
        : this(connection, revoke: null)
    {
    }

    internal DataConnectionRegistration(IDataConnection connection, Func<ValueTask>? revoke)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _revoke = revoke;
    }

    /// <summary>
    /// Gets connection.
    /// </summary>
    public IDataConnection Connection { get; }

    /// <summary>
    /// Executes the revoke async operation.
    /// </summary>
    public ValueTask RevokeAsync()
    {
        Task revokeTask;
        TaskCompletionSource? completion = null;
        lock (_syncRoot)
        {
            if (_revokeTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _revokeTask = completion.Task;
            }

            revokeTask = _revokeTask;
        }

        if (completion is not null)
        {
            _ = CompleteRevokeAsync(completion);
        }

        return new ValueTask(revokeTask);
    }

    /// <summary>
    /// Gets dispose async.
    /// </summary>
    public ValueTask DisposeAsync() => RevokeAsync();

    private async Task CompleteRevokeAsync(TaskCompletionSource completion)
    {
        try
        {
            if (_revoke is not null)
            {
                await _revoke().ConfigureAwait(false);
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_revokeTask, completion.Task))
                {
                    _revokeTask = null;
                }
            }

            completion.TrySetException(exception);
        }
    }
}
