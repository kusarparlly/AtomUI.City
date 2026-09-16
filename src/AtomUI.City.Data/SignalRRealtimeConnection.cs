using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;

namespace AtomUI.City.Data;

/// <summary>
/// Represents signal rconnection options.
/// </summary>
public sealed class SignalRConnectionOptions
{
    private IReadOnlyList<TimeSpan> _reconnectDelays = [];

    /// <summary>
    /// Gets or sets connection id.
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Gets or sets endpoint.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// Gets or sets owner.
    /// </summary>
    public required DataConnectionOwner Owner { get; init; }

    /// <summary>
    /// Gets or sets access token provider.
    /// </summary>
    public Func<ValueTask<string?>>? AccessTokenProvider { get; init; }

    /// <summary>
    /// Represents the reconnect delays value.
    /// </summary>
    public IReadOnlyList<TimeSpan> ReconnectDelays
    {
        get => _reconnectDelays;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(static delay => delay < TimeSpan.Zero))
            {
                throw new ArgumentOutOfRangeException(nameof(ReconnectDelays), "Reconnect delays cannot be negative.");
            }

            _reconnectDelays = Array.AsReadOnly(value.ToArray());
        }
    }
}

/// <summary>
/// Represents data connection state changed event args.
/// </summary>
public sealed class DataConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>DataConnectionStateChangedEventArgs</c> type.
    /// </summary>
    public DataConnectionStateChangedEventArgs(
        DataConnectionState previousState,
        DataConnectionState currentState,
        Exception? error = null)
    {
        if (!Enum.IsDefined(previousState))
        {
            throw new ArgumentOutOfRangeException(nameof(previousState), previousState, "Previous connection state is not supported.");
        }

        if (!Enum.IsDefined(currentState))
        {
            throw new ArgumentOutOfRangeException(nameof(currentState), currentState, "Current connection state is not supported.");
        }

        PreviousState = previousState;
        CurrentState = currentState;
        Error = error;
    }

    /// <summary>
    /// Gets previous state.
    /// </summary>
    public DataConnectionState PreviousState { get; }

    /// <summary>
    /// Gets current state.
    /// </summary>
    public DataConnectionState CurrentState { get; }

    /// <summary>
    /// Gets error.
    /// </summary>
    public Exception? Error { get; }
}

/// <summary>
/// Defines the contract for irealtime connection transport.
/// </summary>
public interface IRealtimeConnectionTransport : IDataConnection, IAsyncDisposable
{
    /// <summary>
    /// Occurs when state changed.
    /// </summary>
    event EventHandler<DataConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Executes the invoke async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the subscribe&lt;tmessage&gt; operation.
    /// </summary>
    IDataSubscription Subscribe<TMessage>(
        string methodName,
        Func<TMessage, CancellationToken, ValueTask> handler,
        DataSubscriptionOptions? options = null);

    /// <summary>
    /// Executes the switch principal async operation.
    /// </summary>
    ValueTask SwitchPrincipalAsync(
        string principalRevision,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents signal rrealtime connection.
/// </summary>
public sealed class SignalRRealtimeConnection : IRealtimeConnectionTransport
{
    private static readonly AsyncLocal<SignalRRealtimeConnection?> CurrentStateObserver = new();
    private readonly HubConnection _connection;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly ConcurrentDictionary<Guid, IDataSubscription> _subscriptions = new();
    private readonly Channel<DataConnectionStateChangedEventArgs> _stateChanges;
    private readonly Task _stateObserverTask;
    private readonly object _subscriptionSyncRoot = new();
    private readonly object _disposeSyncRoot = new();
    private Task? _disposeTask;
    private bool _acceptingSubscriptions = true;
    private int _state = (int)DataConnectionState.Created;
    private int _disposed;
    private string? _principalRevision;

    /// <summary>
    /// Initializes a new instance of the <c>SignalRRealtimeConnection</c> type.
    /// </summary>
    public SignalRRealtimeConnection(
        string connectionId,
        DataConnectionOwner owner,
        HubConnection connection,
        IDataDiagnostics? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(connection);
        if (owner == DataConnectionOwner.None)
        {
            throw new ArgumentException("A SignalR connection must declare a connection owner.", nameof(owner));
        }
        ConnectionId = connectionId;
        Owner = owner;
        _connection = connection;
        _diagnostics = diagnostics;
        _stateChanges = Channel.CreateUnbounded<DataConnectionStateChangedEventArgs>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
        _stateObserverTask = DispatchStateChangesAsync();
        _connection.Reconnecting += OnReconnectingAsync;
        _connection.Reconnected += OnReconnectedAsync;
        _connection.Closed += OnClosedAsync;
    }

    /// <summary>
    /// Occurs when state changed.
    /// </summary>
    public event EventHandler<DataConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets connection id.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// Gets owner.
    /// </summary>
    public DataConnectionOwner Owner { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    public DataConnectionState State => (DataConnectionState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets principal revision.
    /// </summary>
    public string? PrincipalRevision => Volatile.Read(ref _principalRevision);

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static SignalRRealtimeConnection Create(
        SignalRConnectionOptions options,
        IDataDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionId);
        ArgumentNullException.ThrowIfNull(options.Endpoint);
        if (!options.Endpoint.IsAbsoluteUri
            || options.Endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("SignalR endpoint must be an absolute HTTP or HTTPS URI.", nameof(options));
        }

        if (options.Owner == DataConnectionOwner.None)
        {
            throw new ArgumentException("A SignalR connection must declare a connection owner.", nameof(options));
        }

        var builder = new HubConnectionBuilder().WithUrl(options.Endpoint, httpOptions =>
        {
            if (options.AccessTokenProvider is not null)
            {
                httpOptions.AccessTokenProvider = () => options.AccessTokenProvider().AsTask();
            }
        });
        if (options.ReconnectDelays.Count > 0)
        {
            builder.WithAutomaticReconnect(options.ReconnectDelays.ToArray());
        }

        return new SignalRRealtimeConnection(options.ConnectionId, options.Owner, builder.Build(), diagnostics);
    }

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Task[] revocations = [];
        Exception? lifecycleFailure = null;
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            revocations = BeginSubscriptionRevocation();
            await StopTransportCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lifecycleFailure = exception;
        }
        finally
        {
            _lifecycle.Release();
        }

        await CompleteLifecycleAsync(revocations, lifecycleFailure).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the invoke async&lt;tresponse&gt; operation.
    /// </summary>
    public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (State != DataConnectionState.Connected)
        {
            return DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.ConnectionClosed, "SignalR connection is not connected."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _connection
                .InvokeCoreAsync(methodName, typeof(TResponse), arguments?.ToArray() ?? [], cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (response is TResponse typed)
            {
                return DataResult<TResponse>.Success(typed);
            }

            if (response is null && default(TResponse) is null)
            {
                return DataResult<TResponse>.Success(default!);
            }

            return DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.SerializationError, "SignalR response type did not match the requested response type."));
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(new DataError(
                State == DataConnectionState.Reconnecting
                    ? DataErrorKind.ReconnectFailed
                    : DataErrorKind.TransportError,
                DataErrorMessage.FromException(exception, "SignalR invocation failed."),
                Exception: exception));
        }
    }

    /// <summary>
    /// Executes the subscribe&lt;tmessage&gt; operation.
    /// </summary>
    public IDataSubscription Subscribe<TMessage>(
        string methodName,
        Func<TMessage, CancellationToken, ValueTask> handler,
        DataSubscriptionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var subscription = new DataSubscription<TMessage>(
            Owner,
            handler,
            options ?? DataSubscriptionOptions.Default,
            _diagnostics);
        var registration = _connection.On<TMessage>(
            methodName,
            message => subscription.PublishAsync(message).AsTask());
        subscription.Attach(registration);
        var accepted = false;
        lock (_subscriptionSyncRoot)
        {
            if (_acceptingSubscriptions && Volatile.Read(ref _disposed) == 0)
            {
                _subscriptions[subscription.SubscriptionId] = subscription;
                accepted = true;
            }
        }

        if (!accepted)
        {
            _ = subscription.RevokeAsync();
            throw new InvalidOperationException("The SignalR connection is stopping and no longer accepts subscriptions.");
        }

        _ = RemoveCompletedSubscriptionAsync(subscription);
        return subscription;
    }

    /// <summary>
    /// Executes the switch principal async operation.
    /// </summary>
    public async ValueTask SwitchPrincipalAsync(
        string principalRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalRevision);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Task[] revocations = [];
        Exception? lifecycleFailure = null;
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.Equals(_principalRevision, principalRevision, StringComparison.Ordinal))
            {
                return;
            }

            var restart = State is DataConnectionState.Connected or DataConnectionState.Reconnecting;
            if (restart)
            {
                revocations = BeginSubscriptionRevocation();
                await StopTransportCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            Volatile.Write(ref _principalRevision, principalRevision);
            if (restart)
            {
                await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            lifecycleFailure = exception;
        }
        finally
        {
            _lifecycle.Release();
        }

        await CompleteLifecycleAsync(revocations, lifecycleFailure).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;
        lock (_disposeSyncRoot)
        {
            if (_disposeTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = completion.Task;
                Volatile.Write(ref _disposed, 1);
            }

            disposeTask = _disposeTask;
        }

        if (completion is not null)
        {
            _ = CompleteDisposeAsync(completion);
        }

        return ReferenceEquals(CurrentStateObserver.Value, this)
            ? ValueTask.CompletedTask
            : new ValueTask(disposeTask);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        Task[] revocations = [];
        var failures = new List<Exception>();
        try
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                revocations = BeginSubscriptionRevocation();
                try
                {
                    await StopTransportCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                try
                {
                    await _connection.DisposeAsync().ConfigureAwait(false);
                    ChangeState(DataConnectionState.Stopped);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    ChangeState(DataConnectionState.Faulted, exception);
                }
            }
            finally
            {
                _lifecycle.Release();
            }

            try
            {
                await CompleteLifecycleAsync(revocations, lifecycleFailure: null).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            _stateChanges.Writer.TryComplete();
            try
            {
                await _stateObserverTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
        }
        else if (failures.Count == 1)
        {
            completion.TrySetException(failures[0]);
        }
        else
        {
            completion.TrySetException(new AggregateException("SignalR connection disposal failed.", failures));
        }
    }

    private async ValueTask StartCoreAsync(CancellationToken cancellationToken)
    {
        if (State == DataConnectionState.Connected)
        {
            return;
        }

        ChangeState(DataConnectionState.Connecting);
        try
        {
            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            ChangeState(DataConnectionState.Connected);
            lock (_subscriptionSyncRoot)
            {
                _acceptingSubscriptions = Volatile.Read(ref _disposed) == 0;
            }
        }
        catch (Exception exception)
        {
            ChangeState(DataConnectionState.Faulted, exception);
            throw;
        }
    }

    private Task[] BeginSubscriptionRevocation()
    {
        IDataSubscription[] subscriptions;
        lock (_subscriptionSyncRoot)
        {
            _acceptingSubscriptions = false;
            subscriptions = _subscriptions.Values.ToArray();
            _subscriptions.Clear();
        }

        return subscriptions
            .Select(static subscription => subscription.RevokeAsync().AsTask())
            .ToArray();
    }

    private async ValueTask StopTransportCoreAsync(CancellationToken cancellationToken)
    {
        if (State == DataConnectionState.Stopped)
        {
            return;
        }

        ChangeState(DataConnectionState.Disconnecting);
        await _connection.StopAsync(cancellationToken).ConfigureAwait(false);
        ChangeState(DataConnectionState.Stopped);
    }

    private static async Task CompleteLifecycleAsync(
        IReadOnlyList<Task> revocations,
        Exception? lifecycleFailure)
    {
        var failures = lifecycleFailure is null
            ? new List<Exception>()
            : [lifecycleFailure];
        if (revocations.Count > 0)
        {
            try
            {
                await Task.WhenAll(revocations).ConfigureAwait(false);
            }
            catch
            {
                foreach (var revocation in revocations)
                {
                    if (revocation.Exception is { } exception)
                    {
                        failures.AddRange(exception.InnerExceptions);
                    }
                    else if (revocation.IsCanceled)
                    {
                        failures.Add(new OperationCanceledException("A SignalR subscription revocation was cancelled."));
                    }
                }
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("SignalR connection shutdown failed.", failures);
        }
    }

    private Task OnReconnectingAsync(Exception? exception)
    {
        ChangeState(DataConnectionState.Reconnecting, exception);
        return Task.CompletedTask;
    }

    private Task OnReconnectedAsync(string? connectionId)
    {
        ChangeState(DataConnectionState.Connected);
        return Task.CompletedTask;
    }

    private Task OnClosedAsync(Exception? exception)
    {
        ChangeState(exception is null ? DataConnectionState.Stopped : DataConnectionState.Faulted, exception);
        return Task.CompletedTask;
    }

    private async Task RemoveCompletedSubscriptionAsync(IDataSubscription subscription)
    {
        try
        {
            await subscription.Completion.ConfigureAwait(false);
        }
        finally
        {
            _subscriptions.TryRemove(subscription.SubscriptionId, out _);
        }
    }

    private void ChangeState(DataConnectionState state, Exception? error = null)
    {
        var previous = (DataConnectionState)Interlocked.Exchange(ref _state, (int)state);
        if (previous == state)
        {
            return;
        }

        _stateChanges.Writer.TryWrite(
            new DataConnectionStateChangedEventArgs(previous, state, error));
    }

    private async Task DispatchStateChangesAsync()
    {
        await foreach (var stateChange in _stateChanges.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var observers = StateChanged?.GetInvocationList() ?? [];
            foreach (var observer in observers)
            {
                try
                {
                    var previous = CurrentStateObserver.Value;
                    CurrentStateObserver.Value = this;
                    try
                    {
                        ((EventHandler<DataConnectionStateChangedEventArgs>)observer)(this, stateChange);
                    }
                    finally
                    {
                        CurrentStateObserver.Value = previous;
                    }
                }
                catch (Exception exception)
                {
                    DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                        DataDiagnosticIds.HandlerFailed,
                        $"SignalR state observer failed: {exception.Message}",
                        DataDiagnosticSeverity.Warning,
                        ErrorKind: DataErrorKind.StreamProtocolError));
                }
            }
        }
    }
}
