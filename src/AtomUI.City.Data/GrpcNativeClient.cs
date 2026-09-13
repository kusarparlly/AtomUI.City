using Grpc.Core;
using Grpc.Net.Client;
using System.Runtime.CompilerServices;

namespace AtomUI.City.Data;

public sealed class GrpcCallOptions
{
    private IReadOnlyDictionary<string, string> _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private DateTime? _deadlineUtc;
    private DataStreamOptions _stream = DataStreamOptions.Default;

    public IReadOnlyDictionary<string, string> Metadata
    {
        get => _metadata;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null))
            {
                throw new ArgumentException("gRPC metadata keys cannot be blank and values cannot be null.", nameof(Metadata));
            }

            if (value.Keys.Any(static key => key.EndsWith("-bin", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Binary gRPC metadata is not supported by the string metadata contract.", nameof(Metadata));
            }

            _metadata = new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public DateTime? DeadlineUtc
    {
        get => _deadlineUtc;
        init
        {
            if (value is { Kind: not DateTimeKind.Utc })
            {
                throw new ArgumentException("gRPC deadlines must use DateTimeKind.Utc.", nameof(DeadlineUtc));
            }

            _deadlineUtc = value;
        }
    }

    public DataStreamOptions Stream
    {
        get => _stream;
        init => _stream = value ?? throw new ArgumentNullException(nameof(Stream));
    }

    public static GrpcCallOptions Default { get; } = new();
}

public sealed class GrpcChannelConnection : IDataConnection, IDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private int _state = (int)DataConnectionState.Created;
    private int _disposed;

    public GrpcChannelConnection(
        string connectionId,
        DataConnectionOwner owner,
        GrpcChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(channel);
        if (owner == DataConnectionOwner.None)
        {
            throw new ArgumentException("A gRPC channel must declare a connection owner.", nameof(owner));
        }

        ConnectionId = connectionId;
        Owner = owner;
        Channel = channel;
    }

    public string ConnectionId { get; }

    public DataConnectionOwner Owner { get; }

    public GrpcChannel Channel { get; }

    public CallInvoker CallInvoker => Channel.CreateCallInvoker();

    public DataConnectionState State
    {
        get => (DataConnectionState)Volatile.Read(ref _state);
        private set => Volatile.Write(ref _state, (int)value);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == DataConnectionState.Connected)
            {
                return;
            }

            State = DataConnectionState.Connecting;
            try
            {
                await Channel.ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (Volatile.Read(ref _disposed) != 0)
                {
                    State = DataConnectionState.Stopped;
                    throw new ObjectDisposedException(nameof(GrpcChannelConnection));
                }

                State = DataConnectionState.Connected;
                if (Volatile.Read(ref _disposed) != 0)
                {
                    State = DataConnectionState.Stopped;
                    throw new ObjectDisposedException(nameof(GrpcChannelConnection));
                }
            }
            catch
            {
                State = Volatile.Read(ref _disposed) == 0
                    ? DataConnectionState.Faulted
                    : DataConnectionState.Stopped;
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == DataConnectionState.Stopped)
            {
                return;
            }

            State = DataConnectionState.Disconnecting;
            Channel.Dispose();
            Interlocked.Exchange(ref _disposed, 1);
            State = DataConnectionState.Stopped;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Channel.Dispose();
            State = DataConnectionState.Stopped;
        }
    }
}

public sealed class NativeGrpcClient
{
    private readonly CallInvoker _invoker;
    private readonly IDataDiagnostics? _diagnostics;

    public NativeGrpcClient(GrpcChannelConnection connection, IDataDiagnostics? diagnostics = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _invoker = connection.CallInvoker;
        _diagnostics = diagnostics;
    }

    public GrpcChannelConnection Connection { get; }

    public async ValueTask<DataResult<TResponse>> UnaryAsync<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        TRequest request,
        GrpcCallOptions? options = null,
        DataCredential? credential = null,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Connection.State != DataConnectionState.Connected)
        {
            return DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.ConnectionClosed, "The gRPC channel is not connected."));
        }

        try
        {
            using var call = _invoker.AsyncUnaryCall(
                method,
                host: null,
                CreateCallOptions(options, credential, cancellationToken),
                request);
            var response = await call.ResponseAsync.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return DataResult<TResponse>.Success(response);
        }
        catch (RpcException exception)
        {
            return MapRpcFailure<TResponse>(exception);
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.TransportError,
                DataErrorMessage.FromException(exception, "Native gRPC unary call failed."),
                Exception: exception));
        }
    }

    public IDataStream<TResponse> ServerStreaming<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        TRequest request,
        GrpcCallOptions? options = null,
        DataCredential? credential = null,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        options ??= GrpcCallOptions.Default;
        var call = _invoker.AsyncServerStreamingCall(
            method,
            host: null,
            CreateCallOptions(options, credential, cancellationToken),
            request);

        return DataStream<TResponse>.CreateOwned(
            ReadResponses(call.ResponseStream, cancellationToken),
            options.Stream,
            _diagnostics,
            MapStreamError,
            () =>
            {
                call.Dispose();
                return ValueTask.CompletedTask;
            });
    }

    public IGrpcClientStream<TRequest, TResponse> ClientStreaming<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        GrpcCallOptions? options = null,
        DataCredential? credential = null,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        var call = _invoker.AsyncClientStreamingCall(
            method,
            host: null,
            CreateCallOptions(options, credential, cancellationToken));
        return new GrpcClientStream<TRequest, TResponse>(call);
    }

    public IGrpcDuplexStream<TRequest, TResponse> DuplexStreaming<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        GrpcCallOptions? options = null,
        DataCredential? credential = null,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        options ??= GrpcCallOptions.Default;
        var call = _invoker.AsyncDuplexStreamingCall(
            method,
            host: null,
            CreateCallOptions(options, credential, cancellationToken));
        return new GrpcDuplexStream<TRequest, TResponse>(call, options.Stream, _diagnostics, cancellationToken);
    }

    private static CallOptions CreateCallOptions(
        GrpcCallOptions? options,
        DataCredential? credential,
        CancellationToken cancellationToken)
    {
        options ??= GrpcCallOptions.Default;
        var metadata = new Metadata();
        foreach (var pair in options.Metadata)
        {
            if (credential is null || !string.Equals(pair.Key, "authorization", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Add(pair.Key, pair.Value);
            }
        }

        if (credential is not null)
        {
            metadata.Add("authorization", $"{credential.Scheme} {credential.Parameter}");
        }

        return new CallOptions(metadata, options.DeadlineUtc, cancellationToken);
    }

    private void EnsureConnected()
    {
        if (Connection.State != DataConnectionState.Connected)
        {
            throw new InvalidOperationException("The gRPC channel must be connected before starting a stream.");
        }
    }

    private static async IAsyncEnumerable<TResponse> ReadResponses<TResponse>(
        IAsyncStreamReader<TResponse> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            yield return reader.Current;
        }
    }

    private static DataResult<TResponse> MapRpcFailure<TResponse>(RpcException exception)
    {
        var error = GrpcRpcExceptionMapper.Map(exception);
        if (error.Kind == DataErrorKind.Cancelled)
        {
            return DataResult<TResponse>.Cancelled(error.Message);
        }

        return DataResult<TResponse>.Failed(error);
    }

    private static DataError MapStreamError(Exception exception)
    {
        if (exception is RpcException rpcException)
        {
            var mapped = GrpcRpcExceptionMapper.Map(rpcException);
            return mapped with
            {
                Kind = mapped.Kind == DataErrorKind.Cancelled ? DataErrorKind.StreamCancelled : mapped.Kind,
            };
        }

        return new DataError(
            DataErrorKind.StreamProtocolError,
            DataErrorMessage.FromException(exception, "gRPC stream failed."),
            Exception: exception);
    }
}

public interface IGrpcClientStream<in TRequest, TResponse> : IAsyncDisposable
{
    ValueTask WriteAsync(TRequest message, CancellationToken cancellationToken = default);

    ValueTask<DataResult<TResponse>> CompleteAsync(CancellationToken cancellationToken = default);
}

public interface IGrpcDuplexStream<in TRequest, TResponse> : IAsyncDisposable
{
    IDataStream<TResponse> Responses { get; }

    ValueTask WriteAsync(TRequest message, CancellationToken cancellationToken = default);

    ValueTask CompleteRequestAsync(CancellationToken cancellationToken = default);
}

internal sealed class GrpcClientStream<TRequest, TResponse>(
    AsyncClientStreamingCall<TRequest, TResponse> call) : IGrpcClientStream<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly object _completionSyncRoot = new();
    private readonly object _disposeSyncRoot = new();
    private Task<DataResult<TResponse>>? _completion;
    private Task? _disposeTask;
    private bool _requestCompleted;
    private int _disposed;

    public async ValueTask WriteAsync(TRequest message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_requestCompleted)
            {
                throw new InvalidOperationException("The gRPC request stream is already completed.");
            }

            await call.RequestStream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    public ValueTask<DataResult<TResponse>> CompleteAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Task<DataResult<TResponse>> completion;
        TaskCompletionSource<DataResult<TResponse>>? transaction = null;
        lock (_completionSyncRoot)
        {
            if (_completion is null)
            {
                transaction = new TaskCompletionSource<DataResult<TResponse>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _completion = transaction.Task;
            }

            completion = _completion;
        }

        if (transaction is not null)
        {
            _ = CompleteCoreAsync(transaction);
        }

        return new ValueTask<DataResult<TResponse>>(completion.WaitAsync(cancellationToken));
    }

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

        return new ValueTask(disposeTask);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        await _writer.WaitAsync().ConfigureAwait(false);
        try
        {
            call.Dispose();
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            _writer.Release();
        }
    }

    private async Task CompleteCoreAsync(TaskCompletionSource<DataResult<TResponse>> transaction)
    {
        try
        {
            await _writer.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_requestCompleted)
                {
                    _requestCompleted = true;
                    await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _writer.Release();
            }

            var response = await call.ResponseAsync.ConfigureAwait(false);
            transaction.TrySetResult(DataResult<TResponse>.Success(response));
        }
        catch (RpcException exception)
        {
            var error = GrpcRpcExceptionMapper.Map(exception);
            transaction.TrySetResult(error.Kind == DataErrorKind.Cancelled
                ? DataResult<TResponse>.Cancelled(error.Message)
                : DataResult<TResponse>.Failed(error));
        }
        catch (OperationCanceledException)
        {
            transaction.TrySetResult(DataResult<TResponse>.Cancelled());
        }
        catch (Exception exception)
        {
            transaction.TrySetResult(DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.TransportError,
                DataErrorMessage.FromException(exception, "Native gRPC client stream failed."),
                Exception: exception)));
        }
    }
}

internal sealed class GrpcDuplexStream<TRequest, TResponse> : IGrpcDuplexStream<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly AsyncDuplexStreamingCall<TRequest, TResponse> _call;
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly object _disposeSyncRoot = new();
    private Task? _disposeTask;
    private bool _requestCompleted;
    private int _disposed;

    public GrpcDuplexStream(
        AsyncDuplexStreamingCall<TRequest, TResponse> call,
        DataStreamOptions options,
        IDataDiagnostics? diagnostics,
        CancellationToken cancellationToken)
    {
        _call = call;
        Responses = DataStream<TResponse>.CreateOwned(
            ReadResponses(call.ResponseStream, cancellationToken),
            options,
            diagnostics,
            exception => exception is RpcException rpc
                ? MapDuplexError(rpc)
                : new DataError(DataErrorKind.StreamProtocolError, exception.Message, Exception: exception),
            () => ValueTask.CompletedTask);
    }

    public IDataStream<TResponse> Responses { get; }

    public async ValueTask WriteAsync(TRequest message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_requestCompleted)
            {
                throw new InvalidOperationException("The gRPC request stream is already completed.");
            }

            await _call.RequestStream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async ValueTask CompleteRequestAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_requestCompleted)
            {
                _requestCompleted = true;
                await _call.RequestStream.CompleteAsync().ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            _writer.Release();
        }
    }

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

        return new ValueTask(disposeTask);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        var failures = new List<Exception>();
        await _writer.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                await Responses.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                _call.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        finally
        {
            _writer.Release();
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
            completion.TrySetException(new AggregateException("gRPC duplex stream disposal failed.", failures));
        }
    }

    private static async IAsyncEnumerable<TResponse> ReadResponses(
        IAsyncStreamReader<TResponse> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            yield return reader.Current;
        }
    }

    private static DataError MapDuplexError(RpcException exception)
    {
        var mapped = GrpcRpcExceptionMapper.Map(exception);
        return mapped with
        {
            Kind = mapped.Kind == DataErrorKind.Cancelled ? DataErrorKind.StreamCancelled : mapped.Kind,
        };
    }
}

internal static class GrpcRpcExceptionMapper
{
    public static DataError Map(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var detail = string.IsNullOrWhiteSpace(exception.Status.Detail)
            ? null
            : exception.Status.Detail;
        return DataErrorMapper.FromGrpcStatus(
            (GrpcStatusCode)(int)exception.StatusCode,
            detail) with
        {
            Exception = exception,
        };
    }
}
