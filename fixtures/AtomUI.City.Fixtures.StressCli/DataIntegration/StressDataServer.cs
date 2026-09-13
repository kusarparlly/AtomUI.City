using System.Collections.Concurrent;
using System.Net;
using AtomUI.City.Fixtures.StressCli.DataIntegration.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

public sealed class StressDataBackend
{
    private readonly ConcurrentDictionary<string, int> _requestCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _remainingFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StressOrderReceipt> _ordersByRequestId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguousResponses = new(StringComparer.Ordinal);
    private readonly object _orderSyncRoot = new();
    private int _quantity = 50_000;
    private long _revision = 1;
    private long _orderSequence;

    public int Quantity => Volatile.Read(ref _quantity);

    public long Revision => Volatile.Read(ref _revision);

    public int CountOf(string operation) =>
        _requestCounts.TryGetValue(operation, out var count) ? count : 0;

    public void ResetCounters() => _requestCounts.Clear();

    public void FailNext(string operation, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _remainingFailures[operation] = count;
    }

    public void Record(string operation) =>
        _requestCounts.AddOrUpdate(operation, 1, static (_, current) => current + 1);

    public bool ConsumeFailure(string operation)
    {
        while (_remainingFailures.TryGetValue(operation, out var current) && current > 0)
        {
            if (_remainingFailures.TryUpdate(operation, current - 1, current))
            {
                return true;
            }
        }

        return false;
    }

    public StressProductSnapshot GetProduct(string sku, string principal)
    {
        Record("get-product");
        return new StressProductSnapshot(sku, 20m, Quantity, principal, Revision);
    }

    public StressOrderReceipt Submit(StressSubmitOrderRequest request, string principal)
    {
        Record("submit-order");
        if (request.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Quantity must be positive.");
        }

        lock (_orderSyncRoot)
        {
            if (_ordersByRequestId.TryGetValue(request.RequestId, out var existing))
            {
                Record("submit-order-deduplicated");
                return existing;
            }

            var remaining = Interlocked.Add(ref _quantity, -request.Quantity);
            if (remaining < 0)
            {
                Interlocked.Add(ref _quantity, request.Quantity);
                throw new InvalidOperationException("Insufficient inventory.");
            }

            var revision = Interlocked.Increment(ref _revision);
            var sequence = Interlocked.Increment(ref _orderSequence);
            var receipt = new StressOrderReceipt(
                $"remote-{sequence:D8}",
                request.Sku,
                request.Quantity,
                request.Quantity * 20m,
                principal,
                revision);
            _ordersByRequestId.Add(request.RequestId, receipt);
            Record("submit-order-applied");
            return receipt;
        }
    }

    public bool ShouldAbortAmbiguousResponse(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        lock (_orderSyncRoot)
        {
            return _ambiguousResponses.Add(requestId);
        }
    }

    public int ApplyInventoryDelta(int delta)
    {
        Interlocked.Increment(ref _revision);
        return Interlocked.Add(ref _quantity, delta);
    }
}

public sealed record StressDataServerOptions(int HttpPort = 0, int GrpcPort = 0);

public sealed class StressDataServer : IAsyncDisposable
{
    private readonly WebApplication _application;

    private StressDataServer(WebApplication application, StressDataBackend backend, StressDataEndpoints endpoints)
    {
        _application = application;
        Backend = backend;
        Endpoints = endpoints;
    }

    public StressDataBackend Backend { get; }

    public StressDataEndpoints Endpoints { get; }

    public static Task<StressDataServer> StartAsync(CancellationToken cancellationToken = default) =>
        StartAsync(new StressDataServerOptions(), cancellationToken);

    public static async Task<StressDataServer> StartAsync(
        StressDataServerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePort(options.HttpPort, nameof(options.HttpPort));
        ValidatePort(options.GrpcPort, nameof(options.GrpcPort));
        var httpPort = options.HttpPort == 0 ? ReservePort() : options.HttpPort;
        var grpcPort = options.GrpcPort == 0 ? ReservePort() : options.GrpcPort;
        if (httpPort == grpcPort)
        {
            throw new ArgumentException("HTTP and gRPC endpoints must use different ports.", nameof(options));
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, httpPort, listen => listen.Protocols = HttpProtocols.Http1);
            options.Listen(IPAddress.Loopback, grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddSingleton<StressDataBackend>();
        builder.Services.AddGrpc();
        builder.Services.AddSignalR();

        var application = builder.Build();
        MapHttp(application);
        application.MapGrpcService<StressDataGrpcService>();
        application.MapHub<StressDataHub>("/stress-hub");
        await application.StartAsync(cancellationToken).ConfigureAwait(false);

        return new StressDataServer(
            application,
            application.Services.GetRequiredService<StressDataBackend>(),
            new StressDataEndpoints(
                new Uri($"http://127.0.0.1:{httpPort}"),
                new Uri($"http://127.0.0.1:{grpcPort}")));
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync().ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
    }

    internal static bool TryReadPrincipal(HttpRequest request, out string principal, out string revision)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer stress/";
        if (!authorization.StartsWith(prefix, StringComparison.Ordinal))
        {
            principal = string.Empty;
            revision = string.Empty;
            return false;
        }

        var parts = authorization[prefix.Length..].Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            principal = string.Empty;
            revision = string.Empty;
            return false;
        }

        principal = parts[0];
        revision = parts[1];
        return true;
    }

    private static void MapHttp(WebApplication application)
    {
        application.MapGet("/api/products/{sku}", (string sku, HttpRequest request, StressDataBackend backend) =>
        {
            if (!TryReadPrincipal(request, out var principal, out _))
            {
                return Results.Unauthorized();
            }

            if (backend.ConsumeFailure("get-product"))
            {
                backend.Record("get-product");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Json(backend.GetProduct(sku, principal));
        });

        application.MapPost("/api/orders", async (
            HttpRequest request,
            StressDataBackend backend,
            CancellationToken token) =>
        {
            if (!TryReadPrincipal(request, out var principal, out _))
            {
                return Results.Unauthorized();
            }

            if (backend.ConsumeFailure("submit-order"))
            {
                backend.Record("submit-order");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var command = await request.ReadFromJsonAsync<StressSubmitOrderRequest>(token).ConfigureAwait(false);
            if (command is null)
            {
                return Results.BadRequest();
            }

            try
            {
                return Results.Json(backend.Submit(command, principal));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        });

        application.MapGet("/api/principal", (HttpRequest request, StressDataBackend backend) =>
        {
            backend.Record("get-principal");
            return TryReadPrincipal(request, out var principal, out var revision)
                ? Results.Json(new StressPrincipalSnapshot(principal, revision))
                : Results.Unauthorized();
        });

        application.MapGet("/api/search/{term}", async (
            string term,
            int delay,
            HttpRequest request,
            StressDataBackend backend,
            CancellationToken token) =>
        {
            backend.Record("search-orders");
            if (!TryReadPrincipal(request, out var principal, out _))
            {
                return Results.Unauthorized();
            }

            await Task.Delay(Math.Clamp(delay, 0, 5_000), token).ConfigureAwait(false);
            return Results.Text($"{principal}:{term}");
        });

        application.MapGet("/api/delay/{milliseconds:int}", async (
            int milliseconds,
            StressDataBackend backend,
            CancellationToken token) =>
        {
            backend.Record("delay");
            await Task.Delay(Math.Clamp(milliseconds, 0, 30_000), token).ConfigureAwait(false);
            return Results.Text("completed");
        });

        application.MapGet("/api/fault/health", (StressDataBackend backend) =>
        {
            backend.Record("fault-health");
            return Results.Text("ready");
        });

        application.MapGet("/api/fault/inventory", (StressDataBackend backend) =>
        {
            backend.Record("fault-inventory");
            return Results.Json(backend.Quantity);
        });

        application.MapGet("/api/fault/http/sequence/{sequence:int}", async (
            int sequence,
            int delay,
            StressDataBackend backend,
            CancellationToken token) =>
        {
            backend.Record("fault-http-sequence");
            await Task.Delay(Math.Clamp(delay, 0, 5_000), token).ConfigureAwait(false);
            return Results.Json(sequence);
        });

        application.MapGet("/api/fault/http/abort", async (HttpContext context, StressDataBackend backend) =>
        {
            backend.Record("fault-http-abort");
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = 128;
            await context.Response.WriteAsync("{\"partial\":true", CancellationToken.None).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            context.Abort();
        });

        application.MapPost("/api/fault/orders/ambiguous", async (
            HttpContext context,
            StressDataBackend backend,
            CancellationToken token) =>
        {
            if (!TryReadPrincipal(context.Request, out var principal, out _))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var command = await context.Request
                .ReadFromJsonAsync<StressSubmitOrderRequest>(token)
                .ConfigureAwait(false);
            if (command is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            StressOrderReceipt receipt;
            try
            {
                receipt = backend.Submit(command, principal);
            }
            catch (ArgumentOutOfRangeException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            catch (InvalidOperationException)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            if (backend.ShouldAbortAmbiguousResponse(command.RequestId))
            {
                backend.Record("fault-http-ambiguous-abort");
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = 512;
                await context.Response
                    .WriteAsync("{\"orderId\":\"committed-but-response-lost", CancellationToken.None)
                    .ConfigureAwait(false);
                await context.Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                context.Abort();
                return;
            }

            await context.Response.WriteAsJsonAsync(receipt, token).ConfigureAwait(false);
        });
    }

    private static void ValidatePort(int port, string parameterName)
    {
        if (port is < 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(parameterName, port, "Port must be zero or a valid TCP port.");
        }
    }

    private static int ReservePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public sealed class StressDataGrpcService(StressDataBackend backend) : StressDataProbe.StressDataProbeBase
{
    public override async Task<InventoryReply> Inventory(InventoryRequest request, ServerCallContext context)
    {
        backend.Record("grpc-inventory");
        if (string.Equals(request.Sku, StressDataFaults.GrpcUnavailableSku, StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Injected unary unavailability."));
        }

        if (string.Equals(request.Sku, StressDataFaults.GrpcDeadlineSku, StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken).ConfigureAwait(false);
        }

        return new InventoryReply
        {
            Sku = request.Sku,
            Quantity = backend.Quantity,
            Sequence = backend.Revision,
        };
    }

    public override async Task PriceStream(
        PriceStreamRequest request,
        IServerStreamWriter<PriceUpdate> responseStream,
        ServerCallContext context)
    {
        backend.Record("grpc-price-stream");
        var injectAbort = string.Equals(
            request.Sku,
            StressDataFaults.GrpcServerStreamAbortSku,
            StringComparison.Ordinal);
        for (var index = 1; index <= Math.Clamp(request.Count, 1, 10_000); index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await responseStream.WriteAsync(new PriceUpdate
            {
                Sku = request.Sku,
                Price = 20d + index / 100d,
                Sequence = index,
            }).ConfigureAwait(false);
            if (injectAbort && index == 3)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Injected server stream interruption."));
            }
        }
    }

    public override async Task InventoryDuplex(
        IAsyncStreamReader<InventoryDelta> requestStream,
        IServerStreamWriter<InventoryReply> responseStream,
        ServerCallContext context)
    {
        backend.Record("grpc-inventory-duplex");
        var received = 0;
        await foreach (var delta in requestStream.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
        {
            received++;
            var quantity = backend.ApplyInventoryDelta(delta.Delta);
            await responseStream.WriteAsync(new InventoryReply
            {
                Sku = delta.Sku,
                Quantity = quantity,
                Sequence = delta.Sequence,
            }).ConfigureAwait(false);
            if (string.Equals(delta.Sku, StressDataFaults.GrpcDuplexAbortSku, StringComparison.Ordinal) && received == 2)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Injected duplex stream interruption."));
            }
        }
    }
}

public sealed class StressDataHub : Hub
{
    public int Echo(int value) => value + 1;

    public async Task<long> PublishInventory(StressInventoryPush message)
    {
        await Clients.Caller.SendAsync("InventoryChanged", message).ConfigureAwait(false);
        return message.Sequence;
    }

    public async Task<long> PublishShipment(StressShipmentPush message)
    {
        await Clients.Caller.SendAsync("ShipmentProgressed", message).ConfigureAwait(false);
        return message.Sequence;
    }

    public async Task<int> PublishInventorySequence(StressInventoryPush[] messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            await Clients.Caller.SendAsync("InventoryChanged", message).ConfigureAwait(false);
        }

        return messages.Length;
    }

    public void DropConnection() => Context.GetHttpContext()?.Abort();
}
