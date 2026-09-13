using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed record DogfoodTcpFaultProfile(
    TimeSpan Latency,
    int MaximumChunkBytes = 16 * 1024,
    TimeSpan ChunkDelay = default,
    long ResetAfterBytes = long.MaxValue,
    bool Blackhole = false)
{
    public static DogfoodTcpFaultProfile Healthy { get; } = new(TimeSpan.Zero);
}

internal sealed class DogfoodTcpFaultProxy : IAsyncDisposable
{
    private readonly IPEndPoint _target;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<long, ProxyConnection> _connections = new();
    private Task? _acceptLoop;
    private DogfoodTcpFaultProfile _profile = DogfoodTcpFaultProfile.Healthy;
    private long _nextConnectionId;

    private DogfoodTcpFaultProxy(IPEndPoint target, int listenPort)
    {
        _target = target;
        _listener = new TcpListener(IPAddress.Loopback, listenPort);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public int ActiveConnectionCount => _connections.Count;

    public static Task<DogfoodTcpFaultProxy> StartAsync(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var address = IPAddress.TryParse(target.Host, out var parsed)
            ? parsed
            : Dns.GetHostAddresses(target.Host)
                .First(static address => address.AddressFamily == AddressFamily.InterNetwork);
        var proxy = new DogfoodTcpFaultProxy(new IPEndPoint(address, target.Port), 0);
        proxy._listener.Start();
        proxy._acceptLoop = proxy.AcceptLoopAsync(proxy._lifetime.Token);
        return Task.FromResult(proxy);
    }

    public void SetProfile(DogfoodTcpFaultProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Volatile.Write(ref _profile, profile);
    }

    public void ResetConnections()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _listener.Stop();
        ResetConnections();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException) when (_lifetime.IsCancellationRequested)
            {
            }
        }

        _lifetime.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var id = Interlocked.Increment(ref _nextConnectionId);
            var connection = new ProxyConnection(client, cancellationToken);
            _connections[id] = connection;
            _ = ForwardAsync(id, connection);
        }
    }

    private async Task ForwardAsync(long id, ProxyConnection connection)
    {
        var client = connection.Client;
        var cancellationToken = connection.Cancellation.Token;
        using (connection)
        using (var upstream = new TcpClient(AddressFamily.InterNetwork))
        {
            try
            {
                var profile = Volatile.Read(ref _profile);
                if (profile.Latency > TimeSpan.Zero)
                {
                    await Task.Delay(profile.Latency, cancellationToken).ConfigureAwait(false);
                }

                if (profile.Blackhole)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await upstream.ConnectAsync(_target.Address, _target.Port, cancellationToken).ConfigureAwait(false);
                using var clientStream = client.GetStream();
                using var upstreamStream = upstream.GetStream();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var outbound = PumpAsync(clientStream, upstreamStream, linked, linked.Token);
                var inbound = PumpAsync(upstreamStream, clientStream, linked, linked.Token);
                await Task.WhenAny(outbound, inbound).ConfigureAwait(false);
                linked.Cancel();
                try
                {
                    await Task.WhenAll(outbound, inbound).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                _connections.TryRemove(id, out _);
            }
        }
    }

    private async Task PumpAsync(
        Stream source,
        Stream destination,
        CancellationTokenSource connectionCancellation,
        CancellationToken cancellationToken)
    {
        var transferred = 0L;
        var buffer = new byte[16 * 1024];
        while (!cancellationToken.IsCancellationRequested)
        {
            var profile = Volatile.Read(ref _profile);
            var count = await source.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, profile.MaximumChunkBytes)),
                cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            transferred += count;
            if (transferred >= profile.ResetAfterBytes)
            {
                connectionCancellation.Cancel();
                return;
            }

            if (profile.Latency > TimeSpan.Zero)
            {
                await Task.Delay(profile.Latency, cancellationToken).ConfigureAwait(false);
            }

            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (profile.ChunkDelay > TimeSpan.Zero)
            {
                await Task.Delay(profile.ChunkDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class ProxyConnection : IDisposable
    {
        public ProxyConnection(TcpClient client, CancellationToken lifetime)
        {
            Client = client;
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        }

        public TcpClient Client { get; }

        public CancellationTokenSource Cancellation { get; }

        public void Cancel()
        {
            Cancellation.Cancel();
            Client.Dispose();
        }

        public void Dispose()
        {
            Client.Dispose();
            Cancellation.Dispose();
        }
    }
}
