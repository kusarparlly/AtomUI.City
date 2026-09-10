using System.Net;
using AtomUI.City.Data;
using AtomUI.City.Data.HeadlessApp.Grpc;
using AtomUI.City.Fixtures;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Data.HeadlessApp;

internal static class Program
{
    private const string GrpcServiceName = "datafixture.DataProbe";

    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Contains("--expected-failure", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("DATA_HEADLESS_EXPECTED_FAILURE");
        }

        ConfigureLoopbackProxyBypass();
        var descriptorCatalog = new DataClientDescriptorCatalog();
        descriptorCatalog.RegisterGenerated<
            global::AtomUI.City.Generated.GeneratedDataClientRegistrar_AtomUI_City_Data_HeadlessApp_247672EF>();
        Ensure(
            descriptorCatalog.TryGet("headless-probe", out var generatedDescriptor)
            && generatedDescriptor is not null
            && generatedDescriptor.Operations.Count == 1,
            "Generated data client descriptor registration failed.");

        var httpPort = ReservePort();
        var grpcPort = ReservePort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, httpPort, listen => listen.Protocols = HttpProtocols.Http1);
            options.Listen(IPAddress.Loopback, grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGrpc();
        builder.Services.AddSignalR();
        var app = builder.Build();
        var payload = Enumerable.Range(0, 256 * 1024).Select(static index => (byte)index).ToArray();
        app.MapGet("/api/echo/{value:int}", (int value) => Results.Text((value + 1).ToString()));
        app.MapGet("/api/payload", () => Results.Bytes(payload));
        app.MapPut("/api/upload", async (HttpRequest request, CancellationToken token) =>
        {
            long bytes = 0;
            var buffer = new byte[8192];
            while (true)
            {
                var read = await request.Body.ReadAsync(buffer, token);
                if (read == 0)
                {
                    break;
                }

                bytes += read;
            }

            return Results.Json(bytes);
        });
        app.MapGrpcService<ProbeService>();
        app.MapHub<ProbeHub>("/probe-hub");

        await app.StartAsync();
        try
        {
            var httpEndpoint = new Uri($"http://127.0.0.1:{httpPort}");
            var grpcEndpoint = new Uri($"http://127.0.0.1:{grpcPort}");
            await VerifyHttpPipelineAsync(httpEndpoint);
            await VerifyLargePayloadAsync(httpEndpoint, payload);
            await VerifyGrpcAsync(grpcEndpoint);
            await VerifySignalRAsync(httpEndpoint);
            Console.WriteLine("DATA_HEADLESS_OK http=100 grpc-unary=100 grpc-streaming=all signalr=100 payload=262144");
            return 0;
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static async Task VerifyHttpPipelineAsync(Uri endpoint)
    {
        using var httpClient = CreateLoopbackHttpClient(endpoint);
        using var pipeline = new DataRequestPipeline(
            new HttpDataTransport(new FixedHttpClientFactory(httpClient)));
        var requests = Enumerable.Range(0, 100).Select(async value =>
        {
            var request = new HttpDataRequest<int>(
                "fixture-http",
                "echo",
                "fixture",
                _ => new HttpRequestMessage(HttpMethod.Get, $"/api/echo/{value}"),
                async (response, token) => int.Parse(await response.Content.ReadAsStringAsync(token)));
            var result = await pipeline.SendAsync(request);
            Ensure(
                result.Succeeded && result.Value == value + 1,
                $"HTTP pipeline returned an invalid result for value {value}: " +
                $"{result.Status}/{result.Error?.Kind}/{result.Error?.Message}/" +
                $"{result.Error?.Exception?.GetType().FullName}.");
        });
        await Task.WhenAll(requests);
    }

    private static async Task VerifyLargePayloadAsync(Uri endpoint, byte[] payload)
    {
        using var httpClient = CreateLoopbackHttpClient(endpoint);
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream();
        using var download = new HttpRequestMessage(HttpMethod.Get, "/api/payload");
        var downloaded = await client.DownloadAsync(
            download,
            destination,
            options: new DataTransferOptions { BufferSize = 4096 });
        Ensure(downloaded.Succeeded && destination.ToArray().SequenceEqual(payload), "Large download failed.");

        using var upload = new HttpRequestMessage(HttpMethod.Put, "/api/upload");
        var uploaded = await client.UploadAsync(
            upload,
            new MemoryStream(payload, writable: false),
            payload.Length,
            options: new DataTransferOptions { BufferSize = 4096 });
        Ensure(uploaded.Succeeded && uploaded.Value!.BytesTransferred == payload.Length, "Large upload failed.");
    }

    private static async Task VerifyGrpcAsync(Uri endpoint)
    {
        using var channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { UseProxy = false },
        });
        using var connection = new GrpcChannelConnection(
            "fixture-grpc",
            new DataConnectionOwner(DataConnectionOwnerKind.Application, "fixture"),
            channel);
        await connection.StartAsync();
        var client = new NativeGrpcClient(connection);
        var methods = CreateGrpcMethods();

        var unaryCalls = Enumerable.Range(0, 100).Select(async value =>
        {
            var result = await client.UnaryAsync(
                methods.Unary,
                new ProbeRequest { Value = value },
                new GrpcCallOptions
                {
                    Metadata = new Dictionary<string, string> { ["x-data-probe"] = "headless" },
                    DeadlineUtc = DateTime.UtcNow.AddSeconds(10),
                });
            Ensure(
                result.Succeeded && result.Value!.Value == value + 1,
                $"gRPC unary returned an invalid result: {result.Status}/{result.Error?.Kind}/{result.Error?.Message}");
        });
        await Task.WhenAll(unaryCalls);

        await using (var stream = client.ServerStreaming(methods.Server, new ProbeRequest { Value = 10 }))
        {
            var values = new List<int>();
            await foreach (var item in stream)
            {
                Ensure(item.Succeeded, "gRPC server stream failed.");
                values.Add(item.Value!.Value);
            }

            Ensure(values.SequenceEqual([10, 11, 12]), "gRPC server stream order was invalid.");
        }

        await using (var stream = client.ClientStreaming(methods.Client))
        {
            await stream.WriteAsync(new ProbeRequest { Value = 5 });
            await stream.WriteAsync(new ProbeRequest { Value = 7 });
            var firstCompletion = stream.CompleteAsync().AsTask();
            var secondCompletion = stream.CompleteAsync().AsTask();
            var completions = await Task.WhenAll(firstCompletion, secondCompletion);
            Ensure(completions.All(result => result.Succeeded && result.Value!.Value == 12),
                "gRPC client stream failed or concurrent completion was not merged.");
            var writeRejected = false;
            try
            {
                await stream.WriteAsync(new ProbeRequest { Value = 11 });
            }
            catch (InvalidOperationException)
            {
                writeRejected = true;
            }

            Ensure(writeRejected, "gRPC client stream accepted a write after completion.");
        }

        await using (var stream = client.DuplexStreaming(methods.Duplex))
        {
            await stream.WriteAsync(new ProbeRequest { Value = 4 });
            await stream.WriteAsync(new ProbeRequest { Value = 9 });
            await stream.CompleteRequestAsync();
            var values = new List<int>();
            await foreach (var item in stream.Responses)
            {
                Ensure(item.Succeeded, "gRPC duplex stream failed.");
                values.Add(item.Value!.Value);
            }

            Ensure(values.SequenceEqual([8, 18]), "gRPC duplex stream order was invalid.");
        }

        await connection.StopAsync();
    }

    private static async Task VerifySignalRAsync(Uri endpoint)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "fixture");
        await using var connection = SignalRRealtimeConnection.Create(new SignalRConnectionOptions
        {
            ConnectionId = "fixture-signalr",
            Endpoint = new Uri(endpoint, "/probe-hub"),
            Owner = owner,
            AccessTokenProvider = () => ValueTask.FromResult<string?>("fixture-token"),
            ReconnectDelays = [TimeSpan.Zero, TimeSpan.FromMilliseconds(20)],
        });
        var manager = new DataConnectionManager();
        var registration = manager.Register(connection);
        Ensure(registration.Succeeded, "SignalR connection registration failed.");
        await manager.StartOwnerAsync(owner);

        var invocations = Enumerable.Range(0, 100).Select(async value =>
        {
            var result = await connection.InvokeAsync<int>("Echo", [value]);
            Ensure(result.Succeeded && result.Value == value + 1, "SignalR invoke returned an invalid result.");
        });
        await Task.WhenAll(invocations);

        var reconnecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.StateChanged += (_, change) =>
        {
            if (change.CurrentState == DataConnectionState.Reconnecting)
            {
                reconnecting.TrySetResult();
            }
            else if (change.PreviousState == DataConnectionState.Reconnecting
                     && change.CurrentState == DataConnectionState.Connected)
            {
                reconnected.TrySetResult();
            }
        };
        _ = await connection.InvokeAsync<object?>("DropConnection");
        await reconnecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var afterReconnect = await connection.InvokeAsync<int>("Echo", [41]);
        Ensure(afterReconnect.Succeeded && afterReconnect.Value == 42, "SignalR reconnect did not restore invocation.");

        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = connection.Subscribe<int>(
            "ProbeMessage",
            (value, _) =>
            {
                received.TrySetResult(value);
                return ValueTask.CompletedTask;
            });
        var publish = await connection.InvokeAsync<int>("Publish", [808]);
        Ensure(publish.Succeeded && await received.Task.WaitAsync(TimeSpan.FromSeconds(5)) == 808,
            "SignalR server push failed.");

        await connection.SwitchPrincipalAsync("principal:2");
        Ensure(connection.PrincipalRevision == "principal:2", "SignalR principal switch failed.");

        var stoppedFromHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stopSubscription = connection.Subscribe<int>(
            "StopMessage",
            async (_, _) =>
            {
                await connection.StopAsync();
                stoppedFromHandler.TrySetResult();
            });
        _ = await connection.InvokeAsync<int>("PublishStop", [1]);
        await stoppedFromHandler.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(connection.State == DataConnectionState.Stopped, "SignalR handler reentrant stop failed.");
        try
        {
            _ = connection.Subscribe<int>("LateMessage", static (_, _) => ValueTask.CompletedTask);
            throw new InvalidOperationException("SignalR accepted a subscription after stop.");
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("no longer accepts subscriptions", StringComparison.Ordinal))
        {
        }

        await manager.StopOwnerAsync(owner);
        Ensure(connection.State == DataConnectionState.Stopped, "SignalR owner shutdown failed.");
        var firstDispose = connection.DisposeAsync().AsTask();
        var secondDispose = connection.DisposeAsync().AsTask();
        Ensure(ReferenceEquals(firstDispose, secondDispose), "SignalR concurrent dispose did not share one transaction.");
        await Task.WhenAll(firstDispose, secondDispose);
    }

    private static GrpcMethods CreateGrpcMethods()
    {
        var requestMarshaller = Marshallers.Create(
            static (ProbeRequest value) => value.ToByteArray(),
            static data => ProbeRequest.Parser.ParseFrom(data));
        var replyMarshaller = Marshallers.Create(
            static (ProbeReply value) => value.ToByteArray(),
            static data => ProbeReply.Parser.ParseFrom(data));
        return new GrpcMethods(
            new Method<ProbeRequest, ProbeReply>(MethodType.Unary, GrpcServiceName, "Unary", requestMarshaller, replyMarshaller),
            new Method<ProbeRequest, ProbeReply>(MethodType.ServerStreaming, GrpcServiceName, "ServerStream", requestMarshaller, replyMarshaller),
            new Method<ProbeRequest, ProbeReply>(MethodType.ClientStreaming, GrpcServiceName, "ClientStream", requestMarshaller, replyMarshaller),
            new Method<ProbeRequest, ProbeReply>(MethodType.DuplexStreaming, GrpcServiceName, "Duplex", requestMarshaller, replyMarshaller));
    }

    private static int ReservePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpClient CreateLoopbackHttpClient(Uri endpoint)
    {
        return new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = endpoint,
        };
    }

    private static void ConfigureLoopbackProxyBypass()
    {
        AddLoopbackProxyBypass("NO_PROXY");
        if (!OperatingSystem.IsWindows())
        {
            AddLoopbackProxyBypass("no_proxy");
        }
    }

    private static void AddLoopbackProxyBypass(string variableName)
    {
        const string loopbackHosts = "localhost,127.0.0.1,::1";
        var current = Environment.GetEnvironmentVariable(variableName);
        var value = string.IsNullOrWhiteSpace(current)
            ? loopbackHosts
            : $"{current},{loopbackHosts}";
        Environment.SetEnvironmentVariable(variableName, value);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record GrpcMethods(
        Method<ProbeRequest, ProbeReply> Unary,
        Method<ProbeRequest, ProbeReply> Server,
        Method<ProbeRequest, ProbeReply> Client,
        Method<ProbeRequest, ProbeReply> Duplex);

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}

[DataClient("headless-probe", DataTransportKind.Http, Version = "1")]
public interface IHeadlessProbeClient
{
    [DataOperation("echo", DataAccessMode.Query, ConcurrencyPolicy = DataConcurrencyPolicy.AllowConcurrent)]
    ValueTask<DataResult<int>> EchoAsync(int value, CancellationToken cancellationToken = default);
}

public sealed class ProbeService : DataProbe.DataProbeBase
{
    public override Task<ProbeReply> Unary(ProbeRequest request, ServerCallContext context)
    {
        if (context.RequestHeaders.GetValue("x-data-probe") != "headless")
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "metadata missing"));
        }

        return Task.FromResult(new ProbeReply { Value = request.Value + 1 });
    }

    public override async Task ServerStream(
        ProbeRequest request,
        IServerStreamWriter<ProbeReply> responseStream,
        ServerCallContext context)
    {
        for (var index = 0; index < 3; index++)
        {
            await responseStream.WriteAsync(new ProbeReply { Value = request.Value + index });
        }
    }

    public override async Task<ProbeReply> ClientStream(
        IAsyncStreamReader<ProbeRequest> requestStream,
        ServerCallContext context)
    {
        var sum = 0;
        await foreach (var item in requestStream.ReadAllAsync(context.CancellationToken))
        {
            sum += item.Value;
        }

        return new ProbeReply { Value = sum };
    }

    public override async Task Duplex(
        IAsyncStreamReader<ProbeRequest> requestStream,
        IServerStreamWriter<ProbeReply> responseStream,
        ServerCallContext context)
    {
        await foreach (var item in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new ProbeReply { Value = item.Value * 2 });
        }
    }
}

public sealed class ProbeHub : Hub
{
    public int Echo(int value) => value + 1;

    public async Task<int> Publish(int value)
    {
        await Clients.Caller.SendAsync("ProbeMessage", value);
        return value;
    }

    public void DropConnection() => Context.GetHttpContext()?.Abort();

    public async Task<int> PublishStop(int value)
    {
        await Clients.Caller.SendAsync("StopMessage", value);
        return value;
    }
}
