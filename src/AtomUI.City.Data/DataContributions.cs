using System.Collections.Concurrent;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data contribution registry.
/// </summary>
public sealed class DataContributionRegistry : IDataRequestHandlerSource, IDataCapabilityAuthorizer
{
    private readonly ConcurrentDictionary<string, DataContributionLease> _contributions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<DataResilienceCoordinator, byte> _resilienceCoordinators = new();
    private readonly DataClientDescriptorCatalog _descriptors;
    private readonly DataClientRegistry _clients;
    private readonly DataConnectionManager _connections;
    private readonly IDataCacheInvalidator _cacheInvalidator;
    private readonly IDataDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>DataContributionRegistry</c> type.
    /// </summary>
    public DataContributionRegistry(
        DataClientDescriptorCatalog descriptors,
        DataClientRegistry clients,
        DataConnectionManager connections,
        IDataCacheInvalidator cacheInvalidator,
        IDataDiagnostics? diagnostics = null)
    {
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the begin contribution operation.
    /// </summary>
    public DataResult<DataContributionLease> BeginContribution(
        string pluginId,
        string contributionId,
        DataCapability grantedCapabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        if ((grantedCapabilities & ~DataCapabilityRules.All) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grantedCapabilities),
                grantedCapabilities,
                "Data contribution capabilities contain unsupported values.");
        }

        if ((grantedCapabilities & DataCapability.UseDataClient) == 0)
        {
            return DataResult<DataContributionLease>.Failed(new DataError(
                DataErrorKind.PluginUnavailable,
                "A data contribution requires the UseDataClient capability."));
        }

        var token = new object();
        var lease = new DataContributionLease(
            this,
            pluginId,
            contributionId,
            grantedCapabilities,
            token,
            _descriptors,
            _clients,
            _connections,
            _cacheInvalidator,
            _diagnostics);
        if (!_contributions.TryAdd(contributionId, lease))
        {
            return DataResult<DataContributionLease>.Failed(new DataError(
                DataErrorKind.Conflict,
                $"Data contribution '{contributionId}' is already registered."));
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ContributionRegistered,
            $"Data contribution '{contributionId}' registered for plugin '{pluginId}'.",
            DataDiagnosticSeverity.Info));
        return DataResult<DataContributionLease>.Success(lease);
    }

    /// <summary>
    /// Executes the get handlers&lt;tresponse&gt; operation.
    /// </summary>
    public IReadOnlyList<IDataRequestHandler> GetHandlers<TResponse>(DataRequest<TResponse> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Origin.Kind != DataRequestOriginKind.Plugin)
        {
            return [];
        }

        if (request.Origin.ContributionId is null
            || !_contributions.TryGetValue(request.Origin.ContributionId, out var contribution)
            || !contribution.Owns(request.Origin))
        {
            return [RejectedContributionHandler.Instance];
        }

        return contribution.GetHandlers();
    }

    /// <summary>
    /// Executes the is authorized operation.
    /// </summary>
    public bool IsAuthorized(DataRequestOrigin origin, DataCapability capability)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if ((capability & ~DataCapabilityRules.All) != 0)
        {
            return false;
        }

        if (origin.Kind == DataRequestOriginKind.Host)
        {
            return (origin.Capabilities & capability) == capability;
        }

        return origin.ContributionId is not null
            && _contributions.TryGetValue(origin.ContributionId, out var contribution)
            && contribution.Owns(origin)
            && (origin.Capabilities & capability) == capability;
    }

    internal void Remove(string contributionId, DataContributionLease lease)
    {
        if (!_contributions.TryRemove(new KeyValuePair<string, DataContributionLease>(contributionId, lease)))
        {
            return;
        }

        foreach (var coordinator in _resilienceCoordinators.Keys)
        {
            coordinator.InvalidateContribution(contributionId);
        }
    }

    internal IDisposable TrackResilienceCoordinator(DataResilienceCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _resilienceCoordinators.TryAdd(coordinator, 0);
        return new ResilienceCoordinatorLease(this, coordinator);
    }

    private sealed class ResilienceCoordinatorLease(
        DataContributionRegistry registry,
        DataResilienceCoordinator coordinator) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry._resilienceCoordinators.TryRemove(coordinator, out _);
            }
        }
    }

    private sealed class RejectedContributionHandler : IDataRequestHandler
    {
        public static RejectedContributionHandler Instance { get; } = new();

        public int Order => int.MinValue + 1;

        public ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.PluginUnavailable, "Data contribution is no longer active.")));
    }
}

/// <summary>
/// Represents data contribution lease.
/// </summary>
public sealed class DataContributionLease : IAsyncDisposable
{
    private static readonly AsyncLocal<object?> CurrentContribution = new();
    private readonly object _syncRoot = new();
    private readonly DataContributionRegistry _registry;
    private readonly object _token;
    private readonly DataClientDescriptorCatalog _descriptors;
    private readonly DataClientRegistry _clients;
    private readonly DataConnectionManager _connections;
    private readonly IDataCacheInvalidator _cacheInvalidator;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<IDisposable> _descriptorLeases = [];
    private readonly List<IDisposable> _clientLeases = [];
    private readonly List<DataConnectionRegistration> _connectionLeases = [];
    private readonly List<IDataRequestHandler> _handlers = [];
    private DataContributionState _state;
    private Task? _revokeTask;
    private TaskCompletionSource? _operationsDrained;
    private int _activeOperations;

    internal DataContributionLease(
        DataContributionRegistry registry,
        string pluginId,
        string contributionId,
        DataCapability grantedCapabilities,
        object token,
        DataClientDescriptorCatalog descriptors,
        DataClientRegistry clients,
        DataConnectionManager connections,
        IDataCacheInvalidator cacheInvalidator,
        IDataDiagnostics? diagnostics)
    {
        _registry = registry;
        PluginId = pluginId;
        ContributionId = contributionId;
        GrantedCapabilities = grantedCapabilities;
        _token = token;
        _descriptors = descriptors;
        _clients = clients;
        _connections = connections;
        _cacheInvalidator = cacheInvalidator;
        _diagnostics = diagnostics;
        Origin = DataRequestOrigin.Plugin(pluginId, contributionId, grantedCapabilities, token);
    }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string ContributionId { get; }

    /// <summary>
    /// Gets granted capabilities.
    /// </summary>
    public DataCapability GrantedCapabilities { get; }

    /// <summary>
    /// Gets origin.
    /// </summary>
    public DataRequestOrigin Origin { get; }

    /// <summary>
    /// Represents the is active value.
    /// </summary>
    public bool IsActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _state == DataContributionState.Active;
            }
        }
    }

    /// <summary>
    /// Executes the register client descriptor operation.
    /// </summary>
    public IDisposable RegisterClientDescriptor(DataClientDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        EnsureCapability(RequiredCapability(descriptor.TransportKind));
        EnsureActive();
        var lease = _descriptors.Register(descriptor.WithPluginContribution(ContributionId));
        return TrackDisposable(lease, _descriptorLeases);
    }

    /// <summary>
    /// Executes the register client&lt;tclient&gt; operation.
    /// </summary>
    public IDisposable RegisterClient<TClient>(TClient client)
        where TClient : class, IDataClient
    {
        EnsureActive();
        var lease = _clients.RegisterOwned(client);
        return TrackDisposable(lease, _clientLeases);
    }

    /// <summary>
    /// Executes the register handler operation.
    /// </summary>
    public void RegisterHandler(IDataRequestHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureActive();
        lock (_syncRoot)
        {
            EnsureActiveUnderLock();
            _handlers.Add(handler);
        }
    }

    /// <summary>
    /// Executes the register connection async operation.
    /// </summary>
    public async ValueTask<DataResult<DataConnectionRegistration>> RegisterConnectionAsync(
        IDataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        EnsureCapability(connection switch
        {
            SignalRRealtimeConnection => DataCapability.UseRealtimeConnection | DataCapability.UseSignalRHub,
            GrpcChannelConnection => DataCapability.UseGrpcClient,
            _ => DataCapability.UseDataClient,
        });
        if (connection.Owner.Kind != DataConnectionOwnerKind.Plugin
            || !string.Equals(connection.Owner.Id, PluginId, StringComparison.Ordinal))
        {
            return DataResult<DataConnectionRegistration>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                "Plugin data connections must use a matching Plugin owner."));
        }

        EnsureActive();
        var result = _connections.Register(connection);
        if (!result.Succeeded)
        {
            return result;
        }

        lock (_syncRoot)
        {
            if (_state == DataContributionState.Active)
            {
                _connectionLeases.Add(result.Value!);
                return result;
            }
        }

        await result.Value!.RevokeAsync().ConfigureAwait(false);
        return DataResult<DataConnectionRegistration>.Failed(new DataError(
            DataErrorKind.PluginUnavailable,
            "Data contribution stopped while its connection was being registered."));
    }

    /// <summary>
    /// Executes the revoke async operation.
    /// </summary>
    public ValueTask RevokeAsync()
    {
        TaskCompletionSource? completion = null;
        Task revokeTask;
        lock (_syncRoot)
        {
            if (_revokeTask is null)
            {
                _state = DataContributionState.Revoking;
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _revokeTask = completion.Task;
            }

            revokeTask = _revokeTask;
        }

        if (completion is not null)
        {
            _ = RevokeCoreAsync(completion);
        }

        return ReferenceEquals(CurrentContribution.Value, _token)
            ? ValueTask.CompletedTask
            : new ValueTask(revokeTask);
    }

    /// <summary>
    /// Gets dispose async.
    /// </summary>
    public ValueTask DisposeAsync() => RevokeAsync();

    internal bool Owns(DataRequestOrigin origin)
    {
        lock (_syncRoot)
        {
            return _state == DataContributionState.Active && ReferenceEquals(origin.Token, _token);
        }
    }

    internal IReadOnlyList<IDataRequestHandler> GetHandlers()
    {
        IDataRequestHandler[] handlers;
        CancellationToken cancellationToken;
        lock (_syncRoot)
        {
            if (_state != DataContributionState.Active)
            {
                return [ContributionCancellationHandler.Rejected];
            }

            handlers = _handlers.ToArray();
            cancellationToken = _cancellation.Token;
        }

        return new IDataRequestHandler[] { new ContributionCancellationHandler(this, cancellationToken) }
            .Concat(handlers)
            .OrderBy(static handler => handler.Order)
            .ToArray();
    }

    private IDisposable TrackDisposable(IDisposable lease, List<IDisposable> target)
    {
        lock (_syncRoot)
        {
            if (_state == DataContributionState.Active)
            {
                target.Add(lease);
                return lease;
            }
        }

        lease.Dispose();
        throw new InvalidOperationException("Data contribution is no longer active.");
    }

    private async Task RevokeCoreAsync(TaskCompletionSource completion)
    {
        List<IDisposable> descriptors;
        List<IDisposable> clients;
        List<DataConnectionRegistration> connections;
        Task operationsDrained;
        var failures = new List<Exception>();
        lock (_syncRoot)
        {
            descriptors = [.. _descriptorLeases];
            clients = [.. _clientLeases];
            connections = [.. _connectionLeases];
            _descriptorLeases.Clear();
            _clientLeases.Clear();
            _connectionLeases.Clear();
            _handlers.Clear();
            operationsDrained = _activeOperations == 0
                ? Task.CompletedTask
                : (_operationsDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        try
        {
            _cancellation.Cancel(throwOnFirstException: false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        foreach (var connection in connections.AsEnumerable().Reverse())
        {
            try
            {
                await connection.RevokeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        await operationsDrained.ConfigureAwait(false);

        foreach (var lease in descriptors.Concat(clients).Reverse())
        {
            try
            {
                lease.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            await _cacheInvalidator
                .InvalidateAsync(DataCacheInvalidation.ForPlugin(ContributionId), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        lock (_syncRoot)
        {
            _state = DataContributionState.Revoked;
        }

        _registry.Remove(ContributionId, this);
        try
        {
            _cancellation.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ContributionRevoked,
            $"Data contribution '{ContributionId}' revoked.",
            failures.Count == 0 ? DataDiagnosticSeverity.Info : DataDiagnosticSeverity.Warning));

        if (failures.Count == 0)
        {
            completion.SetResult();
        }
        else if (failures.Count == 1)
        {
            completion.SetException(failures[0]);
        }
        else
        {
            completion.SetException(new AggregateException(failures));
        }
    }

    private void EnsureCapability(DataCapability capability)
    {
        if ((GrantedCapabilities & capability) != capability)
        {
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ContributionRejected,
                $"Data contribution '{ContributionId}' lacks capability '{capability}'.",
                DataDiagnosticSeverity.Warning,
                ErrorKind: DataErrorKind.PluginUnavailable));
            throw new UnauthorizedAccessException($"Data capability '{capability}' was not granted.");
        }
    }

    private void EnsureActive()
    {
        lock (_syncRoot)
        {
            EnsureActiveUnderLock();
        }
    }

    private void EnsureActiveUnderLock()
    {
        if (_state != DataContributionState.Active)
        {
            throw new InvalidOperationException("Data contribution is no longer active.");
        }
    }

    private bool TryEnterOperation(out IDisposable? activity)
    {
        lock (_syncRoot)
        {
            if (_state != DataContributionState.Active)
            {
                activity = null;
                return false;
            }

            _activeOperations++;
            activity = new OperationActivity(this);
            return true;
        }
    }

    private void ExitOperation()
    {
        TaskCompletionSource? drained = null;
        lock (_syncRoot)
        {
            _activeOperations--;
            if (_activeOperations == 0 && _state != DataContributionState.Active)
            {
                drained = _operationsDrained;
            }
        }

        drained?.TrySetResult();
    }

    private static DataCapability RequiredCapability(DataTransportKind kind) => kind switch
    {
        DataTransportKind.Http => DataCapability.UseHttpClient,
        DataTransportKind.Grpc => DataCapability.UseGrpcClient,
        DataTransportKind.SignalR => DataCapability.UseSignalRHub,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Data transport kind is not supported."),
    };

    private enum DataContributionState
    {
        Active,
        Revoking,
        Revoked,
    }

    private sealed class OperationActivity(DataContributionLease owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.ExitOperation();
            }
        }
    }

    private sealed class ContributionCancellationHandler : IDataRequestHandler
    {
        private readonly DataContributionLease? _owner;
        private readonly CancellationToken _contributionCancellation;
        private readonly bool _rejected;

        public ContributionCancellationHandler(
            DataContributionLease owner,
            CancellationToken contributionCancellation)
        {
            _owner = owner;
            _contributionCancellation = contributionCancellation;
        }

        private ContributionCancellationHandler(bool rejected)
        {
            _rejected = rejected;
        }

        public static ContributionCancellationHandler Rejected { get; } = new(rejected: true);

        public int Order => int.MinValue + 2;

        public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
        {
            if (_rejected || _contributionCancellation.IsCancellationRequested)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(DataErrorKind.PluginUnavailable, "Data contribution is no longer active."));
            }

            if (!_owner!.TryEnterOperation(out var activity))
            {
                return DataResult<TResponse>.Failed(
                    new DataError(DataErrorKind.PluginUnavailable, "Data contribution is no longer active."));
            }

            using (activity)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _contributionCancellation);
                var previousContribution = CurrentContribution.Value;
                CurrentContribution.Value = _owner._token;
                try
                {
                    var result = await next(linked.Token).ConfigureAwait(false);
                    return _contributionCancellation.IsCancellationRequested
                        ? DataResult<TResponse>.Failed(
                            new DataError(DataErrorKind.PluginUnavailable, "Data contribution was revoked during the request."))
                        : result;
                }
                catch (OperationCanceledException) when (_contributionCancellation.IsCancellationRequested)
                {
                    return DataResult<TResponse>.Failed(
                        new DataError(DataErrorKind.PluginUnavailable, "Data contribution was revoked during the request."));
                }
                finally
                {
                    CurrentContribution.Value = previousContribution;
                }
            }
        }
    }
}
