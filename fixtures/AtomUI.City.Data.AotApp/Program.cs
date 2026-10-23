using System.Text.Json;
using System.Text.Json.Serialization;
using AtomUI.City.Data;
using AtomUI.City.Data.HeadlessApp.Grpc;
using AtomUI.City.Fixtures;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

namespace AtomUI.City.Data.AotApp;

[DataClient("aot-probe", DataTransportKind.Http, Version = "1")]
public interface IAotProbeClient
{
    [DataOperation("load", DataAccessMode.Query, CacheEnabled = true)]
    ValueTask<DataResult<AotReply>> LoadAsync(AotRequest request, CancellationToken cancellationToken = default);
}

public sealed record AotRequest(int Value);

public sealed record AotReply(int Value);

public sealed record AotSignalRRequest(int Value, string CorrelationId);

public sealed record AotSignalRReply(int Value, string CorrelationId);

public sealed record AotSignalRMessage(int Value, string CorrelationId, string Source);

public sealed record DataAotResult(
    bool Success,
    bool Http,
    bool Grpc,
    bool SignalRInvoke,
    bool SignalRPush,
    bool GeneratedDescriptor,
    bool Scheduler,
    bool CacheFingerprint,
    string FrameworkDescription);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AotSignalRRequest))]
[JsonSerializable(typeof(AotSignalRReply))]
[JsonSerializable(typeof(AotSignalRMessage))]
[JsonSerializable(typeof(DataAotResult))]
internal sealed partial class DataAotJsonContext : JsonSerializerContext;

internal static class Program
{
    private const string GrpcServiceName = "datafixture.DataProbe";

    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        ConfigureLoopbackProxyBypass();
        var httpEndpoint = ReadEndpoint(args, "--http-endpoint");
        var grpcEndpoint = ReadEndpoint(args, "--grpc-endpoint");

        var descriptor = VerifyGeneratedDescriptor();
        var scheduler = await VerifySchedulerAsync();
        var fingerprint = VerifyCacheFingerprint();
        var http = await VerifyHttpAsync(httpEndpoint);
        var grpc = await VerifyGrpcAsync(grpcEndpoint);
        var (signalRInvoke, signalRPush) = await VerifySignalRAsync(httpEndpoint);

        var result = new DataAotResult(
            true,
            http,
            grpc,
            signalRInvoke,
            signalRPush,
            descriptor,
            scheduler,
            fingerprint,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Console.WriteLine("DATA_AOT_OK");
        Console.WriteLine(JsonSerializer.Serialize(result, DataAotJsonContext.Default.DataAotResult));
        return 0;
    }

    private static bool VerifyGeneratedDescriptor()
    {
        var catalog = new DataClientDescriptorCatalog();
        catalog.RegisterGenerated<
            global::AtomUI.City.Generated.GeneratedDataClientRegistrar_AtomUI_City_Data_AotApp_5F5EC4E4>();
        Ensure(
            catalog.TryGet("aot-probe", out var descriptor)
            && descriptor is not null
            && descriptor.Operations.Count == 1,
            "Generated Data descriptor registration failed.");
        return true;
    }

    private static async Task<bool> VerifySchedulerAsync()
    {
        using var scheduler = new DataOperationScheduler();
        var request = new DataRequest<int>("aot", "schedule", DataTransportKind.Http);
        var result = await scheduler.ExecuteAsync(
            request,
            _ => ValueTask.FromResult(DataResult<int>.Success(42)));
        Ensure(result.Succeeded && result.Value == 42, "Data scheduler failed.");
        return true;
    }

    private static bool VerifyCacheFingerprint()
    {
        var fingerprint = DataCacheFingerprint.Create("https://localhost/aot", "GET", "{}");
        Ensure(fingerprint.Length == 64, "Data cache fingerprint failed.");
        return true;
    }

    private static async Task<bool> VerifyHttpAsync(Uri endpoint)
    {
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = endpoint,
        };
        using var pipeline = new DataRequestPipeline(
            new HttpDataTransport(new FixedHttpClientFactory(client)));
        var request = new HttpDataRequest<int>(
            "aot-http",
            "echo",
            "aot",
            _ => new HttpRequestMessage(HttpMethod.Get, "/api/echo/41"),
            async (response, token) => int.Parse(await response.Content.ReadAsStringAsync(token)));
        var result = await pipeline.SendAsync(request);
        Ensure(result.Succeeded && result.Value == 42, "NativeAOT HTTP protocol call failed.");
        return true;
    }

    private static async Task<bool> VerifyGrpcAsync(Uri endpoint)
    {
        using var channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { UseProxy = false },
        });
        using var connection = new GrpcChannelConnection(
            "aot-grpc",
            new DataConnectionOwner(DataConnectionOwnerKind.Application, "aot"),
            channel);
        await connection.StartAsync();
        var client = new NativeGrpcClient(connection);
        var requestMarshaller = Marshallers.Create(
            static (ProbeRequest value) => value.ToByteArray(),
            static data => ProbeRequest.Parser.ParseFrom(data));
        var replyMarshaller = Marshallers.Create(
            static (ProbeReply value) => value.ToByteArray(),
            static data => ProbeReply.Parser.ParseFrom(data));
        var method = new Method<ProbeRequest, ProbeReply>(
            MethodType.Unary,
            GrpcServiceName,
            "Unary",
            requestMarshaller,
            replyMarshaller);
        var result = await client.UnaryAsync(
            method,
            new ProbeRequest { Value = 41 },
            new GrpcCallOptions
            {
                Metadata = new Dictionary<string, string> { ["x-data-probe"] = "headless" },
                DeadlineUtc = DateTime.UtcNow.AddSeconds(10),
            });
        await connection.StopAsync();
        Ensure(result.Succeeded && result.Value!.Value == 42, "NativeAOT gRPC protocol call failed.");
        return true;
    }

    private static async Task<(bool Invoke, bool Push)> VerifySignalRAsync(Uri endpoint)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "aot");
        await using var connection = SignalRRealtimeConnection.Create(new SignalRConnectionOptions
        {
            ConnectionId = "aot-signalr",
            Endpoint = new Uri(endpoint, "/probe-hub"),
            Owner = owner,
            JsonContract = SignalRJsonContract.Create(DataAotJsonContext.Default),
        });
        await connection.StartAsync();

        const string correlationId = "aot-correlation-42";
        var request = new AotSignalRRequest(41, correlationId);
        var reply = await connection.InvokeAsync<AotSignalRReply>("AotEcho", [request]);
        Ensure(
            reply.Succeeded
            && reply.Value == new AotSignalRReply(42, correlationId),
            $"NativeAOT SignalR custom DTO invocation failed: {reply.Error?.Message}");

        var pushed = new TaskCompletionSource<AotSignalRMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = connection.Subscribe<AotSignalRMessage>(
            "AotMessage",
            (message, _) =>
            {
                pushed.TrySetResult(message);
                return ValueTask.CompletedTask;
            });
        var publish = await connection.InvokeAsync<AotSignalRReply>("AotPublish", [request]);
        var message = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Ensure(
            publish.Succeeded
            && publish.Value == new AotSignalRReply(42, correlationId)
            && message == new AotSignalRMessage(43, correlationId, "server-push"),
            $"NativeAOT SignalR custom DTO server push failed: {publish.Error?.Message}");

        await connection.StopAsync();
        return (true, true);
    }

    private static Uri ReadEndpoint(IReadOnlyList<string> args, string option)
    {
        var index = -1;
        for (var candidate = 0; candidate < args.Count; candidate++)
        {
            if (string.Equals(args[candidate], option, StringComparison.Ordinal))
            {
                index = candidate;
                break;
            }
        }

        if (index < 0
            || index + 1 >= args.Count
            || !Uri.TryCreate(args[index + 1], UriKind.Absolute, out var endpoint))
        {
            throw new ArgumentException($"{option} requires an absolute endpoint.", nameof(args));
        }

        return endpoint;
    }

    private static void ConfigureLoopbackProxyBypass()
    {
        const string loopbackHosts = "localhost,127.0.0.1,::1";
        Environment.SetEnvironmentVariable("NO_PROXY", loopbackHosts);
        if (!OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable("no_proxy", loopbackHosts);
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
