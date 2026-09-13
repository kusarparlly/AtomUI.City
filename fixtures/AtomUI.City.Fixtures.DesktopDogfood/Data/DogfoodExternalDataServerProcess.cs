using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using AtomUI.City.Fixtures.StressCli.DataIntegration;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodExternalDataServerProcess : IAsyncDisposable
{
    internal const string ChildModeArgument = "--data-server-child";

    private const string HttpPortArgument = "--http-port";
    private const string GrpcPortArgument = "--grpc-port";
    private const string ReadyMessage = "DESKTOP_DOGFOOD_DATA_SERVER_CHILD_READY";

    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private Process? _process;
    private int _restartCount;
    private int _disposed;

    private DogfoodExternalDataServerProcess(int httpPort, int grpcPort)
    {
        Endpoints = new StressDataEndpoints(
            new Uri($"http://127.0.0.1:{httpPort}"),
            new Uri($"http://127.0.0.1:{grpcPort}"));
    }

    public StressDataEndpoints Endpoints { get; }

    public int RestartCount => Volatile.Read(ref _restartCount);

    public bool IsRunning => _process is { HasExited: false };

    public bool IsReleased => _process is null;

    public static bool IsChildMode(IReadOnlyList<string> args) =>
        args.Any(static argument => string.Equals(argument, ChildModeArgument, StringComparison.Ordinal));

    public static async Task<DogfoodExternalDataServerProcess> StartAsync(
        CancellationToken cancellationToken = default)
    {
        var httpPort = ReservePort();
        var grpcPort = ReservePort();
        while (grpcPort == httpPort)
        {
            grpcPort = ReservePort();
        }

        var controller = new DogfoodExternalDataServerProcess(httpPort, grpcPort);
        try
        {
            await controller.StartCoreAsync(cancellationToken).ConfigureAwait(false);
            return controller;
        }
        catch
        {
            await controller.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static async Task<int> RunChildAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        var httpPort = ReadPort(args, HttpPortArgument);
        var grpcPort = ReadPort(args, GrpcPortArgument);
        await using var server = await StressDataServer.StartAsync(
            new StressDataServerOptions(httpPort, grpcPort),
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(ReadyMessage);
        await Console.Out.FlushAsync(cancellationToken).ConfigureAwait(false);

        var command = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return command is null || string.Equals(command, "stop", StringComparison.Ordinal)
            ? 0
            : 2;
    }

    public async Task CrashAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var process = _process ?? throw new InvalidOperationException("The external Data server is not running.");
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            process.Dispose();
            _process = null;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is not null)
            {
                throw new InvalidOperationException("The external Data server must be stopped before restart.");
            }

            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _restartCount);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
            _lifecycle.Dispose();
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add(typeof(DogfoodExternalDataServerProcess).Assembly.Location);
        process.StartInfo.ArgumentList.Add(ChildModeArgument);
        process.StartInfo.ArgumentList.Add(HttpPortArgument);
        process.StartInfo.ArgumentList.Add(Endpoints.Http.Port.ToString(CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add(GrpcPortArgument);
        process.StartInfo.ArgumentList.Add(Endpoints.Grpc.Port.ToString(CultureInfo.InvariantCulture));

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("The external Data server process could not be started.");
        }

        try
        {
            var ready = await process.StandardOutput
                .ReadLineAsync(cancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(ready, ReadyMessage, StringComparison.Ordinal))
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"The external Data server did not report readiness. stdout='{ready}', stderr='{error}'.");
            }

            _process = process;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            process.Dispose();
            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync("stop").ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                try
                {
                    await process.WaitForExitAsync()
                        .WaitAsync(TimeSpan.FromSeconds(10))
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static int ReadPort(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal) &&
                int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) &&
                port is > 0 and <= 65_535)
            {
                return port;
            }
        }

        throw new ArgumentException($"A valid '{name}' value is required for Data server child mode.", nameof(args));
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
