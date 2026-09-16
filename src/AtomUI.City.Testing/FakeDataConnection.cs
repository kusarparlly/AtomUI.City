using AtomUI.City.Data;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents fake data connection.
/// </summary>
public sealed class FakeDataConnection : IDataConnection
{
    private readonly Func<CancellationToken, ValueTask>? _start;
    private readonly Func<CancellationToken, ValueTask>? _stop;
    private int _startCount;
    private int _stopCount;

    /// <summary>
    /// Initializes a new instance of the <c>FakeDataConnection</c> type.
    /// </summary>
    public FakeDataConnection(
        string connectionId,
        DataConnectionOwner owner,
        Func<CancellationToken, ValueTask>? start = null,
        Func<CancellationToken, ValueTask>? stop = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        if (owner == DataConnectionOwner.None)
        {
            throw new ArgumentException("A fake data connection requires an owner.", nameof(owner));
        }

        ConnectionId = connectionId;
        Owner = owner;
        _start = start;
        _stop = stop;
    }

    /// <summary>
    /// Gets connection id.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// Gets owner.
    /// </summary>
    public DataConnectionOwner Owner { get; }

    /// <summary>
    /// Gets or sets state.
    /// </summary>
    public DataConnectionState State { get; private set; } = DataConnectionState.Created;

    /// <summary>
    /// Gets start count.
    /// </summary>
    public int StartCount => Volatile.Read(ref _startCount);

    /// <summary>
    /// Gets stop count.
    /// </summary>
    public int StopCount => Volatile.Read(ref _stopCount);

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        State = DataConnectionState.Connecting;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_start is not null)
            {
                await _start(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _startCount);
            State = DataConnectionState.Connected;
        }
        catch
        {
            State = DataConnectionState.Faulted;
            throw;
        }
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (State == DataConnectionState.Stopped)
        {
            return;
        }

        State = DataConnectionState.Disconnecting;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_stop is not null)
            {
                await _stop(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _stopCount);
            State = DataConnectionState.Stopped;
        }
        catch
        {
            State = DataConnectionState.Faulted;
            throw;
        }
    }
}
