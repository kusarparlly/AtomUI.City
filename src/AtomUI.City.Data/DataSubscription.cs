using System.Threading.Channels;

namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data subscription error policy values.
/// </summary>
public enum DataSubscriptionErrorPolicy
{
    /// <summary>
    /// Represents the continue value.
    /// </summary>
    Continue,
    /// <summary>
    /// Represents the stop value.
    /// </summary>
    Stop,
}

/// <summary>
/// Represents data subscription options.
/// </summary>
public sealed class DataSubscriptionOptions
{
    private int _capacity = 64;
    private DataBackpressurePolicy _backpressurePolicy = DataBackpressurePolicy.DropOldest;
    private DataSubscriptionErrorPolicy _errorPolicy = DataSubscriptionErrorPolicy.Continue;

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
    /// Represents the error policy value.
    /// </summary>
    public DataSubscriptionErrorPolicy ErrorPolicy
    {
        get => _errorPolicy;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(ErrorPolicy), value, "Subscription error policy is not supported.");
            }

            _errorPolicy = value;
        }
    }

    /// <summary>
    /// Gets default.
    /// </summary>
    public static DataSubscriptionOptions Default { get; } = new();
}

/// <summary>
/// Defines the contract for idata subscription.
/// </summary>
public interface IDataSubscription : IAsyncDisposable
{
    /// <summary>
    /// Gets subscription id.
    /// </summary>
    Guid SubscriptionId { get; }

    /// <summary>
    /// Gets owner.
    /// </summary>
    DataConnectionOwner Owner { get; }

    /// <summary>
    /// Gets completion.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Executes the revoke async operation.
    /// </summary>
    ValueTask RevokeAsync();
}

internal sealed class DataSubscription<T> : IDataSubscription
{
    private static readonly AsyncLocal<Guid?> CurrentSubscription = new();
    private readonly Channel<T> _channel;
    private readonly Func<T, CancellationToken, ValueTask> _handler;
    private readonly DataSubscriptionOptions _options;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationToken _cancellationToken;
    private readonly object _revocationSyncRoot = new();
    private IDisposable? _transportRegistration;
    private Task? _revokeTask;
    private int _revocationRequested;

    public DataSubscription(
        DataConnectionOwner owner,
        Func<T, CancellationToken, ValueTask> handler,
        DataSubscriptionOptions options,
        IDataDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.BackpressurePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.BackpressurePolicy, "Backpressure policy is not supported.");
        }

        if (!Enum.IsDefined(options.ErrorPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ErrorPolicy, "Subscription error policy is not supported.");
        }

        Owner = owner;
        _handler = handler;
        _options = options;
        _diagnostics = diagnostics;
        _cancellationToken = _cancellation.Token;
        var channelCapacity = options.BackpressurePolicy == DataBackpressurePolicy.LatestOnly
            ? 1
            : options.Capacity;
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        Completion = DispatchAsync();
    }

    public Guid SubscriptionId { get; } = Guid.NewGuid();

    public DataConnectionOwner Owner { get; }

    public Task Completion { get; }

    public void Attach(IDisposable registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (Interlocked.CompareExchange(ref _transportRegistration, registration, null) is not null)
        {
            registration.Dispose();
            throw new InvalidOperationException("A transport registration is already attached to this subscription.");
        }

        if (Volatile.Read(ref _revocationRequested) != 0)
        {
            Interlocked.Exchange(ref _transportRegistration, null)?.Dispose();
        }
    }

    public async ValueTask PublishAsync(T item, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _revocationRequested) != 0)
        {
            return;
        }

        if (_options.BackpressurePolicy is DataBackpressurePolicy.Buffer or DataBackpressurePolicy.BlockProducer)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationToken);
            await _channel.Writer.WriteAsync(item, linked.Token).ConfigureAwait(false);
            return;
        }

        if (_channel.Writer.TryWrite(item))
        {
            return;
        }

        if (_options.BackpressurePolicy is DataBackpressurePolicy.DropOldest or DataBackpressurePolicy.LatestOnly)
        {
            _channel.Reader.TryRead(out _);
            _channel.Writer.TryWrite(item);
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.BackpressureDropped,
            $"Data subscription '{SubscriptionId}' dropped an item using '{_options.BackpressurePolicy}'.",
            DataDiagnosticSeverity.Warning));
    }

    public ValueTask RevokeAsync()
    {
        TaskCompletionSource? completion = null;
        Task revokeTask;
        lock (_revocationSyncRoot)
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
            Interlocked.Exchange(ref _revocationRequested, 1);
            _ = RevokeCoreAsync(completion);
        }

        if (CurrentSubscription.Value == SubscriptionId)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(revokeTask);
    }

    public ValueTask DisposeAsync() => RevokeAsync();

    private async Task DispatchAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var previousSubscription = CurrentSubscription.Value;
                    CurrentSubscription.Value = SubscriptionId;
                    try
                    {
                        await _handler(item, _cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        CurrentSubscription.Value = previousSubscription;
                    }

                    _cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                        DataDiagnosticIds.HandlerFailed,
                        $"Data subscription '{SubscriptionId}' handler failed: {exception.Message}",
                        DataDiagnosticSeverity.Warning,
                        ErrorKind: DataErrorKind.StreamProtocolError));
                    if (_options.ErrorPolicy == DataSubscriptionErrorPolicy.Stop)
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _revocationRequested, 1);
            try
            {
                Interlocked.Exchange(ref _transportRegistration, null)?.Dispose();
            }
            catch (Exception exception)
            {
                DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                    DataDiagnosticIds.HandlerFailed,
                    $"Data subscription '{SubscriptionId}' transport registration cleanup failed: {exception.Message}",
                    DataDiagnosticSeverity.Warning,
                    ErrorKind: DataErrorKind.StreamProtocolError));
            }

            _channel.Writer.TryComplete();
            _cancellation.Dispose();
        }
    }

    private async Task RevokeCoreAsync(TaskCompletionSource completion)
    {
        var failures = new List<Exception>();
        try
        {
            Interlocked.Exchange(ref _transportRegistration, null)?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            _cancellation.Cancel(throwOnFirstException: false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        _channel.Writer.TryComplete();
        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failures.Add(exception);
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
            completion.TrySetException(new AggregateException(failures));
        }
    }
}
