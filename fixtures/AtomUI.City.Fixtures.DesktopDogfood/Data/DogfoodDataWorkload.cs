using System.Collections.Concurrent;
using System.Net.Http.Json;
using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.DataIntegration.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodDataRequestProbe : IDataRequestHandler
{
    private readonly ConcurrentDictionary<Guid, byte> _operationIds = new();
    private int _invocations;

    public int Order => -1_000;

    public int Invocations => Volatile.Read(ref _invocations);

    public int UniqueOperations => _operationIds.Count;

    public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _invocations);
        _operationIds.TryAdd(context.OperationId, 0);
        request.Items["dogfood-correlation"] = context.OperationId.ToString("N");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DogfoodRemoteOperations
{
    public const string ClientId = "dogfood-operations";
    public const string ClientName = "dogfood-loopback";

    private readonly IDataRequestPipeline _pipeline;

    public DogfoodRemoteOperations(IDataRequestPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public ValueTask<DataResult<StressProductSnapshot>> GetProductAsync(
        string sku,
        DataResilienceOptions? resilience = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<StressProductSnapshot>(
            ClientId,
            "get-product",
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Get, $"/api/products/{Uri.EscapeDataString(sku)}"),
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<StressProductSnapshot>(cancellationToken: token)
                    .ConfigureAwait(false))!)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            Cache = DataCacheOptions.Enabled(
                sku,
                principalRevision: "r10",
                permissionRevision: "64",
                clientVersion: "1",
                policyVersion: "dogfood-v1",
                timeToLive: TimeSpan.FromMinutes(1)),
            Resilience = resilience ?? DataResilienceOptions.None,
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<StressOrderReceipt>> SubmitOrderAsync(
        StressSubmitOrderRequest command,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<StressOrderReceipt>(
            ClientId,
            "submit-order",
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(command),
            },
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<StressOrderReceipt>(cancellationToken: token)
                    .ConfigureAwait(false))!,
            DataAccessMode.Mutation)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            IdempotencyKey = command.RequestId,
            Concurrency = new DataConcurrencyOptions
            {
                Policy = DataConcurrencyPolicy.KeyedSerial,
                OperationKey = "submit-order",
                ResourceKey = command.Sku,
            },
            Consistency = new DataConsistencyOptions
            {
                InvalidationsOnSuccess =
                [
                    DataCacheInvalidation.ForOperation(
                        ClientId,
                        "get-product",
                        DataCacheInvalidationReason.Mutation),
                ],
            },
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<string>> SearchAsync(
        string term,
        int delayMilliseconds,
        DataConcurrencyPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<string>(
            ClientId,
            "search-orders",
            ClientName,
            _ => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/search/{Uri.EscapeDataString(term)}?delay={delayMilliseconds}"),
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            Concurrency = new DataConcurrencyOptions
            {
                Policy = policy,
                OperationKey = "search-orders",
                ResourceKey = "orders",
                MaximumQueueLength = 32,
            },
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<string>> DelayAsync(
        int milliseconds,
        DataRequestOrigin? origin = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<string>(
            ClientId,
            "delay",
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Get, $"/api/delay/{milliseconds}"),
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
        {
            Origin = origin ?? DataRequestOrigin.Host,
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<string>> DelayWithResilienceAsync(
        int milliseconds,
        DataResilienceOptions resilience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resilience);
        var request = new HttpDataRequest<string>(
            ClientId,
            "delay-resilience",
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Get, $"/api/delay/{milliseconds}"),
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
        {
            Resilience = resilience,
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }
}

internal sealed class DogfoodDataWorkload
{
    private const string GrpcServiceName = "stressdata.StressDataProbe";

    private static readonly Method<InventoryRequest, InventoryReply> InventoryMethod = CreateMethod(
        MethodType.Unary,
        "Inventory",
        InventoryRequest.Parser,
        InventoryReply.Parser);

    private static readonly Method<PriceStreamRequest, PriceUpdate> PriceStreamMethod = CreateMethod(
        MethodType.ServerStreaming,
        "PriceStream",
        PriceStreamRequest.Parser,
        PriceUpdate.Parser);

    private static readonly Method<InventoryDelta, InventoryReply> InventoryDuplexMethod = CreateMethod(
        MethodType.DuplexStreaming,
        "InventoryDuplex",
        InventoryDelta.Parser,
        InventoryReply.Parser);

    private readonly StressDataServer _server;
    private readonly DogfoodRemoteOperations _operations;
    private readonly DogfoodDataRequestProbe _probe;
    private readonly DataConnectionManager _connections;
    private readonly DataContributionRegistry _contributions;
    private readonly IDataDiagnostics _diagnostics;

    public DogfoodDataWorkload(
        StressDataServer server,
        DogfoodRemoteOperations operations,
        DogfoodDataRequestProbe probe,
        DataConnectionManager connections,
        DataContributionRegistry contributions,
        IDataDiagnostics diagnostics)
    {
        _server = server;
        _operations = operations;
        _probe = probe;
        _connections = connections;
        _contributions = contributions;
        _diagnostics = diagnostics;
    }

    public async Task InitializeAsync(Action<string> applyStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applyStatus);
        await VerifyHttpPipelineAsync(cancellationToken);
        var (grpcMessages, signalRMessages) = await VerifyRealtimeTransportsAsync(cancellationToken);
        await VerifyContributionAsync(cancellationToken);

        if (_probe.Invocations == 0 ||
            _probe.UniqueOperations == 0 ||
            _probe.UniqueOperations > _probe.Invocations)
        {
            throw new InvalidOperationException(
                $"Data request handler correlation failed: calls={_probe.Invocations}, ids={_probe.UniqueOperations}.");
        }

        var diagnosticText = string.Join('|', _diagnostics.Records.Select(static record => record.Message));
        if (diagnosticText.Contains("stress/admin/r10", StringComparison.Ordinal) ||
            diagnosticText.Contains("stress/alice/r8", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Data diagnostics leaked a bearer token.");
        }

        applyStatus(
            $"HTTP pipeline, native gRPC and SignalR passed; {grpcMessages} gRPC and {signalRMessages} realtime messages.");
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_DATA httpHandlers={_probe.Invocations} operationIds={_probe.UniqueOperations} grpcMessages={grpcMessages} signalRMessages={signalRMessages} diagnostics={_diagnostics.Records.Count}");
    }

    private async Task VerifyHttpPipelineAsync(CancellationToken cancellationToken)
    {
        _server.Backend.ResetCounters();
        var first = await _operations.GetProductAsync("SKU-0001", cancellationToken: cancellationToken);
        var cached = await _operations.GetProductAsync("SKU-0001", cancellationToken: cancellationToken);
        if (!first.Succeeded || !cached.Succeeded || _server.Backend.CountOf("get-product") != 1)
        {
            throw new InvalidOperationException("HTTP query cache did not short-circuit the second transport call.");
        }

        var order = await _operations.SubmitOrderAsync(
            new StressSubmitOrderRequest("SKU-0001", 2, Guid.NewGuid().ToString("N")),
            cancellationToken);
        var refreshed = await _operations.GetProductAsync("SKU-0001", cancellationToken: cancellationToken);
        if (!order.Succeeded || !refreshed.Succeeded || _server.Backend.CountOf("get-product") != 2)
        {
            throw new InvalidOperationException("HTTP mutation did not invalidate the product cache.");
        }

        _server.Backend.FailNext("get-product", 2);
        var retried = await _operations.GetProductAsync(
            "SKU-RETRY",
            new DataResilienceOptions
            {
                MaxRetryAttempts = 2,
                RetryDelay = TimeSpan.FromMilliseconds(5),
                PolicyName = "dogfood-transient",
            },
            cancellationToken);
        if (!retried.Succeeded)
        {
            throw new InvalidOperationException($"HTTP retry policy failed: {retried.Error?.Kind}.");
        }

        var obsolete = _operations.SearchAsync(
            "obsolete",
            250,
            DataConcurrencyPolicy.LatestWins,
            cancellationToken).AsTask();
        await Task.Delay(20, cancellationToken);
        var latest = _operations.SearchAsync(
            "latest",
            1,
            DataConcurrencyPolicy.LatestWins,
            cancellationToken).AsTask();
        var searches = await Task.WhenAll(obsolete, latest);
        if (searches[0].Status is not (DataResultStatus.Cancelled or DataResultStatus.StaleSuppressed) ||
            !searches[1].Succeeded ||
            !string.Equals(searches[1].Value, "admin:latest", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"LatestWins failed: old={searches[0].Status}, latest={searches[1].Status}:{searches[1].Value}.");
        }
    }

    private async Task<(int GrpcMessages, int SignalRMessages)> VerifyRealtimeTransportsAsync(
        CancellationToken cancellationToken)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-realtime");
        var grpc = new GrpcChannelConnection(
            "dogfood-grpc",
            owner,
            GrpcChannel.ForAddress(_server.Endpoints.Grpc, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { UseProxy = false },
            }));
        var signalR = SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = "dogfood-signalr",
                Endpoint = new Uri(_server.Endpoints.Http, "/stress-hub"),
                Owner = owner,
                AccessTokenProvider = () => ValueTask.FromResult<string?>("stress/admin/r10"),
                ReconnectDelays = [TimeSpan.Zero, TimeSpan.FromMilliseconds(20)],
            },
            _diagnostics);

        var grpcRegistration = _connections.Register(grpc);
        var signalRRegistration = _connections.Register(signalR);
        if (!grpcRegistration.Succeeded || !signalRRegistration.Succeeded)
        {
            throw new InvalidOperationException("DataConnectionManager rejected a real transport connection.");
        }

        try
        {
            await _connections.StartOwnerAsync(owner, cancellationToken);
            if (grpc.State != DataConnectionState.Connected || signalR.State != DataConnectionState.Connected)
            {
                throw new InvalidOperationException("gRPC or SignalR did not enter Connected state.");
            }

            var grpcClient = new NativeGrpcClient(grpc, _diagnostics);
            var unary = await grpcClient.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = "SKU-0001" },
                cancellationToken: cancellationToken);
            if (!unary.Succeeded || unary.Value!.Quantity != _server.Backend.Quantity)
            {
                throw new InvalidOperationException("Native gRPC unary result did not match server state.");
            }

            var grpcMessages = 1;
            await using (var stream = grpcClient.ServerStreaming(
                PriceStreamMethod,
                new PriceStreamRequest { Sku = "SKU-0001", Count = 32 },
                new GrpcCallOptions
                {
                    Stream = new DataStreamOptions
                    {
                        Capacity = 4,
                        BackpressurePolicy = DataBackpressurePolicy.BlockProducer,
                    },
                },
                cancellationToken: cancellationToken))
            {
                await foreach (var result in stream.WithCancellation(cancellationToken))
                {
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(result.Error?.Message ?? "gRPC stream failed.");
                    }

                    grpcMessages++;
                }
            }

            await using (var duplex = grpcClient.DuplexStreaming(
                InventoryDuplexMethod,
                new GrpcCallOptions
                {
                    Stream = new DataStreamOptions
                    {
                        Capacity = 4,
                        BackpressurePolicy = DataBackpressurePolicy.Buffer,
                    },
                },
                cancellationToken: cancellationToken))
            {
                var received = new List<InventoryReply>();
                var receive = Task.Run(async () =>
                {
                    await foreach (var result in duplex.Responses.WithCancellation(cancellationToken))
                    {
                        if (result.Succeeded)
                        {
                            received.Add(result.Value!);
                        }
                    }
                }, cancellationToken);

                for (var index = 1; index <= 3; index++)
                {
                    await duplex.WriteAsync(
                        new InventoryDelta { Sku = "SKU-0001", Delta = index, Sequence = index },
                        cancellationToken);
                }

                await duplex.CompleteRequestAsync(cancellationToken);
                await receive;
                if (received.Count != 3 || !received.Select(static item => item.Sequence).SequenceEqual([1L, 2L, 3L]))
                {
                    throw new InvalidOperationException("Native gRPC duplex stream did not preserve message order.");
                }

                grpcMessages += received.Count;
            }

            var pushed = new TaskCompletionSource<StressInventoryPush>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var subscription = signalR.Subscribe<StressInventoryPush>(
                "InventoryChanged",
                (message, _) =>
                {
                    pushed.TrySetResult(message);
                    return ValueTask.CompletedTask;
                },
                new DataSubscriptionOptions
                {
                    Capacity = 8,
                    BackpressurePolicy = DataBackpressurePolicy.DropOldest,
                });
            var echo = await signalR.InvokeAsync<int>("Echo", [41], cancellationToken);
            var payload = new StressInventoryPush("SKU-0001", 4321, 10);
            var publication = await signalR.InvokeAsync<long>("PublishInventory", [payload], cancellationToken);
            var observed = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (!echo.Succeeded || echo.Value != 42 || !publication.Succeeded || publication.Value != 10 || observed != payload)
            {
                throw new InvalidOperationException("SignalR invoke or typed subscription failed.");
            }

            await signalR.SwitchPrincipalAsync("r11", cancellationToken);
            await subscription.RevokeAsync();
            return (grpcMessages, 1);
        }
        finally
        {
            await _connections.StopOwnerAsync(owner, CancellationToken.None);
            if (grpcRegistration.Value is not null)
            {
                await grpcRegistration.Value.DisposeAsync();
            }

            if (signalRRegistration.Value is not null)
            {
                await signalRRegistration.Value.DisposeAsync();
            }
        }
    }

    private async Task VerifyContributionAsync(CancellationToken cancellationToken)
    {
        var result = _contributions.BeginContribution(
            "dogfood.synthetic.plugin",
            $"dogfood.data.{Guid.NewGuid():N}",
            DataCapability.UseDataClient | DataCapability.UseHttpClient);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error?.Message ?? "Data contribution creation failed.");
        }

        var contribution = result.Value!;
        var before = await _operations.DelayAsync(1, contribution.Origin, cancellationToken);
        await contribution.RevokeAsync();
        var after = await _operations.DelayAsync(1, contribution.Origin, cancellationToken);
        if (!before.Succeeded || after.Error?.Kind != DataErrorKind.PluginUnavailable)
        {
            throw new InvalidOperationException(
                $"Data contribution revoke failed: before={before.Status}, after={after.Error?.Kind}.");
        }
    }

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>(
        MethodType methodType,
        string methodName,
        MessageParser<TRequest> requestParser,
        MessageParser<TResponse> responseParser)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse>
    {
        return new Method<TRequest, TResponse>(
            methodType,
            GrpcServiceName,
            methodName,
            Marshallers.Create(
                static message => message.ToByteArray(),
                payload => requestParser.ParseFrom(payload)),
            Marshallers.Create(
                static message => message.ToByteArray(),
                payload => responseParser.ParseFrom(payload)));
    }
}
