using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data backpressure policy values.
/// </summary>
public enum DataBackpressurePolicy
{
    /// <summary>
    /// Represents the buffer value.
    /// </summary>
    Buffer,
    /// <summary>
    /// Represents the drop oldest value.
    /// </summary>
    DropOldest,
    /// <summary>
    /// Represents the drop newest value.
    /// </summary>
    DropNewest,
    /// <summary>
    /// Represents the latest only value.
    /// </summary>
    LatestOnly,
    /// <summary>
    /// Represents the block producer value.
    /// </summary>
    BlockProducer,
}

/// <summary>
/// Represents data stream options.
/// </summary>
public sealed class DataStreamOptions
{
    private int _capacity = 64;
    private DataBackpressurePolicy _backpressurePolicy = DataBackpressurePolicy.Buffer;

    /// <summary>
    /// Represents the capacity value.
    /// </summary>
    public int Capacity
    {
        get => _capacity;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(Capacity));
            _capacity = value;
        }
    }

    /// <summary>
    /// Represents the backpressure policy value.
    /// </summary>
    public DataBackpressurePolicy BackpressurePolicy
    {
        get => _backpressurePolicy;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(BackpressurePolicy), value, "Backpressure policy is not supported.");
            }

            _backpressurePolicy = value;
        }
    }

    /// <summary>
    /// Gets or sets parent scope.
    /// </summary>
    public LifecycleScope? ParentScope { get; init; }

    /// <summary>
    /// Gets default.
    /// </summary>
    public static DataStreamOptions Default { get; } = new();
}

/// <summary>
/// Defines the contract for idata stream&lt;t&gt;.
/// </summary>
public interface IDataStream<T> : IAsyncEnumerable<DataResult<T>>, IAsyncDisposable
{
    /// <summary>
    /// Gets stream id.
    /// </summary>
    Guid StreamId { get; }

    /// <summary>
    /// Gets completion.
    /// </summary>
    Task Completion { get; }
}

/// <summary>
/// Represents data stream&lt;t&gt;.
/// </summary>
public sealed class DataStream<T> : IDataStream<T>
{
    private readonly Channel<DataResult<T>> _channel;
    private readonly CancellationTokenSource _cancellation;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly Func<Exception, DataError> _errorMapper;
    private readonly Func<ValueTask>? _release;
    private readonly DataStreamOptions _options;
    private readonly object _disposeSyncRoot = new();
    private Task? _disposeTask;
    private int _enumerated;
    private int _disposed;

    private DataStream(
        IAsyncEnumerable<T> source,
        DataStreamOptions options,
        IDataDiagnostics? diagnostics,
        Func<Exception, DataError>? errorMapper,
        Func<ValueTask>? release)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.BackpressurePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.BackpressurePolicy,
                "Backpressure policy is not supported.");
        }

        _options = options;
        _diagnostics = diagnostics;
        _errorMapper = errorMapper ?? DefaultErrorMapper;
        _release = release;
        var channelCapacity = options.BackpressurePolicy == DataBackpressurePolicy.LatestOnly
            ? 1
            : options.Capacity;
        _channel = Channel.CreateBounded<DataResult<T>>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });
        _cancellation = CreateCancellation(options.ParentScope);
        StreamId = Guid.NewGuid();
        Completion = PumpAsync(source);
    }

    /// <summary>
    /// Gets stream id.
    /// </summary>
    public Guid StreamId { get; }

    /// <summary>
    /// Gets completion.
    /// </summary>
    public Task Completion { get; }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static DataStream<T> Create(
        IAsyncEnumerable<T> source,
        DataStreamOptions? options = null,
        IDataDiagnostics? diagnostics = null,
        Func<Exception, DataError>? errorMapper = null) =>
        new(source, options ?? DataStreamOptions.Default, diagnostics, errorMapper, release: null);

    internal static DataStream<T> CreateOwned(
        IAsyncEnumerable<T> source,
        DataStreamOptions options,
        IDataDiagnostics? diagnostics,
        Func<Exception, DataError>? errorMapper,
        Func<ValueTask> release) =>
        new(source, options, diagnostics, errorMapper, release);

    /// <summary>
    /// Executes the get async enumerator operation.
    /// </summary>
    public async IAsyncEnumerator<DataResult<T>> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _enumerated, 1) != 0)
        {
            throw new InvalidOperationException("A data stream can only have one active consumer.");
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _cancellation.Token);
        await foreach (var item in _channel.Reader.ReadAllAsync(linkedCancellation.Token).ConfigureAwait(false))
        {
            yield return item;
        }
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

        return new ValueTask(disposeTask);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        var failures = new List<Exception>();
        try
        {
            try
            {
                _cancellation.Cancel(throwOnFirstException: false);
            }
            catch (AggregateException exception)
            {
                failures.AddRange(exception.InnerExceptions);
            }

            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        finally
        {
            _cancellation.Dispose();
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
            return;
        }

        var failure = failures.Count == 1
            ? failures[0]
            : new AggregateException("One or more data stream disposal operations failed.", failures);
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.StreamFailed,
            $"Data stream '{StreamId}' disposal failed: {failure.Message}",
            DataDiagnosticSeverity.Warning,
            ErrorKind: DataErrorKind.StreamProtocolError));
        completion.TrySetException(failure);
    }

    private async Task PumpAsync(IAsyncEnumerable<T> source)
    {
        try
        {
            await foreach (var item in source.WithCancellation(_cancellation.Token).ConfigureAwait(false))
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                await PublishAsync(DataResult<T>.Success(item), _cancellation.Token).ConfigureAwait(false);
            }

            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.StreamCompleted,
                $"Data stream '{StreamId}' completed.",
                DataDiagnosticSeverity.Trace));
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DataError error;
            try
            {
                error = _errorMapper(exception) ?? throw new InvalidOperationException("Data stream error mapper returned null.");
            }
            catch (Exception mapperException)
            {
                error = new DataError(
                    DataErrorKind.StreamProtocolError,
                    "Data stream failure could not be mapped by the configured error mapper.",
                    Exception: new AggregateException(exception, mapperException));
            }

            PublishTerminal(DataResult<T>.Failed(error));
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.StreamFailed,
                $"Data stream '{StreamId}' failed: {error.Message}",
                DataDiagnosticSeverity.Warning,
                ErrorKind: error.Kind));
        }
        finally
        {
            _channel.Writer.TryComplete();
            if (_release is not null)
            {
                try
                {
                    await _release().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                        DataDiagnosticIds.StreamFailed,
                        $"Data stream '{StreamId}' release failed: {exception.Message}",
                        DataDiagnosticSeverity.Warning,
                        ErrorKind: DataErrorKind.TransportError));
                    throw;
                }
            }
        }
    }

    private async ValueTask PublishAsync(DataResult<T> result, CancellationToken cancellationToken)
    {
        if (_options.BackpressurePolicy is DataBackpressurePolicy.Buffer or DataBackpressurePolicy.BlockProducer)
        {
            await _channel.Writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_channel.Writer.TryWrite(result))
        {
            return;
        }

        if (_options.BackpressurePolicy is DataBackpressurePolicy.DropOldest or DataBackpressurePolicy.LatestOnly)
        {
            _channel.Reader.TryRead(out _);
            _channel.Writer.TryWrite(result);
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.BackpressureDropped,
            $"Data stream '{StreamId}' dropped an item using '{_options.BackpressurePolicy}'.",
            DataDiagnosticSeverity.Warning));
    }

    private void PublishTerminal(DataResult<T> result)
    {
        while (!_channel.Writer.TryWrite(result))
        {
            if (!_channel.Reader.TryRead(out _))
            {
                Thread.Yield();
            }
        }
    }

    private static CancellationTokenSource CreateCancellation(LifecycleScope? parentScope)
    {
        if (parentScope is null)
        {
            return new CancellationTokenSource();
        }

        try
        {
            return CancellationTokenSource.CreateLinkedTokenSource(parentScope.CancellationToken);
        }
        catch (ObjectDisposedException)
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            return cancellation;
        }
    }

    private static DataError DefaultErrorMapper(Exception exception) => new(
        DataErrorKind.StreamProtocolError,
        DataErrorMessage.FromException(exception, "Data stream failed."),
        Exception: exception);
}
