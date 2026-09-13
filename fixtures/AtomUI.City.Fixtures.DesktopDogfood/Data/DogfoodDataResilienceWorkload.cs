using System.Collections.Concurrent;
using System.Net.Http.Json;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.DataIntegration.Grpc;
using AtomUI.City.State;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodExternalRemoteOperations
{
    public const string ClientId = DogfoodRemoteOperations.ClientId;
    public const string ClientName = "dogfood-external-loopback";

    private readonly IDataRequestPipeline _pipeline;

    public DogfoodExternalRemoteOperations(IDataRequestPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public ValueTask<DataResult<string>> HealthAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "external-health",
            "/api/fault/health",
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false),
            timeout is null ? DataResilienceOptions.None : new DataResilienceOptions { Timeout = timeout },
            cancellationToken);

    public ValueTask<DataResult<int>> InventoryAsync(CancellationToken cancellationToken = default) =>
        SendAsync(
            "external-inventory",
            "/api/fault/inventory",
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<int>(cancellationToken: token).ConfigureAwait(false))!,
            DataResilienceOptions.None,
            cancellationToken);

    public ValueTask<DataResult<string>> AbortResponseAsync(CancellationToken cancellationToken = default) =>
        SendAsync(
            "external-abort-response",
            "/api/fault/http/abort",
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false),
            DataResilienceOptions.None,
            cancellationToken);

    public ValueTask<DataResult<int>> SequenceAsync(
        int sequence,
        int delayMilliseconds,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "external-sequence",
            $"/api/fault/http/sequence/{sequence}?delay={delayMilliseconds}",
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<int>(cancellationToken: token).ConfigureAwait(false))!,
            DataResilienceOptions.None,
            cancellationToken);

    public ValueTask<DataResult<StressOrderReceipt>> SubmitAmbiguousAsync(
        StressSubmitOrderRequest command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = new HttpDataRequest<StressOrderReceipt>(
            ClientId,
            "external-ambiguous-order",
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Post, "/api/fault/orders/ambiguous")
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
            Resilience = new DataResilienceOptions
            {
                MaxRetryAttempts = 1,
                RetryDelay = TimeSpan.FromMilliseconds(20),
                PolicyName = "dogfood-ambiguous-mutation",
            },
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }

    private ValueTask<DataResult<T>> SendAsync<T>(
        string operationName,
        string relativeUri,
        Func<HttpResponseMessage, CancellationToken, ValueTask<T>> mapper,
        DataResilienceOptions resilience,
        CancellationToken cancellationToken)
    {
        var request = new HttpDataRequest<T>(
            ClientId,
            operationName,
            ClientName,
            _ => new HttpRequestMessage(HttpMethod.Get, relativeUri),
            mapper)
        {
            Resilience = resilience,
        };
        return _pipeline.SendAsync(request, cancellationToken);
    }
}

internal sealed class DogfoodDataResilienceWorkload
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

    private readonly StressDataServer _inProcessServer;
    private readonly DogfoodExternalDataServerProcess _externalServer;
    private readonly DogfoodRemoteOperations _internalOperations;
    private readonly DogfoodExternalRemoteOperations _externalOperations;
    private readonly DataConnectionManager _connections;
    private readonly IDataDiagnostics _diagnostics;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly IEventBus _eventBus;
    private readonly DogfoodRunLedger _ledger;

    public DogfoodDataResilienceWorkload(
        StressDataServer inProcessServer,
        DogfoodExternalDataServerProcess externalServer,
        DogfoodRemoteOperations internalOperations,
        DogfoodExternalRemoteOperations externalOperations,
        DataConnectionManager connections,
        IDataDiagnostics diagnostics,
        IApplicationStateWriter stateWriter,
        IEventBus eventBus,
        DogfoodRunLedger ledger)
    {
        _inProcessServer = inProcessServer;
        _externalServer = externalServer;
        _internalOperations = internalOperations;
        _externalOperations = externalOperations;
        _connections = connections;
        _diagnostics = diagnostics;
        _stateWriter = stateWriter;
        _eventBus = eventBus;
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await VerifyHttpAsync(cancellationToken).ConfigureAwait(false);
        await VerifyGrpcFaultsAsync(cancellationToken).ConfigureAwait(false);
        await VerifySignalRFaultsAsync(cancellationToken).ConfigureAwait(false);
        await VerifyExternalProcessRecoveryAsync(cancellationToken).ConfigureAwait(false);

        _stateWriter.Set(DogfoodStateWorkload.NetworkReachability, "Online after transport restart");
        _stateWriter.Set(DogfoodStateWorkload.HttpHealth, "Ready");
        _stateWriter.Set(DogfoodStateWorkload.GrpcHealth, "Ready");
        _stateWriter.Set(DogfoodStateWorkload.SignalRHealth, "Ready");
        _stateWriter.Set(DogfoodStateWorkload.RealtimeConnectionState, "Connected");
        var publication = await _eventBus.PublishAsync(
            new NetworkReachabilityChanged(Guid.NewGuid(), "data-transport-recovered", 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!publication.Succeeded)
        {
            throw new InvalidOperationException("The Data recovery EventBus publication did not complete successfully.");
        }

        if (_ledger.Covered("data-resilience") != 13 || !_externalServer.IsReleased)
        {
            throw new InvalidOperationException(
                $"Data resilience coverage did not close: coverage={_ledger.Covered("data-resilience")}, " +
                $"childReleased={_externalServer.IsReleased}.");
        }

        Console.WriteLine(
            $"DESKTOP_DOGFOOD_DATA_RESILIENCE faults=13 expectedFailures={_ledger.ExpectedFailureCount} " +
            $"restarts={_externalServer.RestartCount} http=ready grpc=ready signalR=ready " +
            $"childReleased={_externalServer.IsReleased.ToString().ToLowerInvariant()}");
    }

    private async Task VerifyHttpAsync(CancellationToken cancellationToken)
    {
        _inProcessServer.Backend.ResetCounters();
        _inProcessServer.Backend.FailNext("get-product", 2);
        var retried = await _internalOperations.GetProductAsync(
            "SKU-DTR-01",
            new DataResilienceOptions
            {
                MaxRetryAttempts = 2,
                RetryDelay = TimeSpan.FromMilliseconds(10),
                PolicyName = "dtr-01",
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            retried.Succeeded && _inProcessServer.Backend.CountOf("get-product") == 3,
            "DTR-01 HTTP retry did not execute exactly three attempts.");
        Record("DTR-01");

        var timedOut = await _internalOperations.DelayWithResilienceAsync(
            1_000,
            new DataResilienceOptions
            {
                Timeout = TimeSpan.FromMilliseconds(80),
                PolicyName = "dtr-02",
            },
            cancellationToken).ConfigureAwait(false);
        Require(!timedOut.Succeeded, "DTR-02 delayed HTTP request unexpectedly succeeded.");
        Record("DTR-02", expectedFailure: true);

        var aborted = await _externalOperations.AbortResponseAsync(cancellationToken).ConfigureAwait(false);
        Require(!aborted.Succeeded, "DTR-03 accepted an incomplete HTTP response body as success.");
        Record("DTR-03", expectedFailure: true);

        var slow = _externalOperations.SequenceAsync(1, 180, cancellationToken).AsTask();
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        var fast = _externalOperations.SequenceAsync(2, 5, cancellationToken).AsTask();
        Require(ReferenceEquals(await Task.WhenAny(slow, fast).ConfigureAwait(false), fast),
            "DTR-04 responses did not complete in the injected reverse order.");
        var sequenceResults = await Task.WhenAll(slow, fast).ConfigureAwait(false);
        Require(
            sequenceResults[0].Succeeded && sequenceResults[0].Value == 1 &&
            sequenceResults[1].Succeeded && sequenceResults[1].Value == 2,
            "DTR-04 response values no longer matched their originating requests.");
        Record("DTR-04");

        var inventoryBefore = await _externalOperations.InventoryAsync(cancellationToken).ConfigureAwait(false);
        var command = new StressSubmitOrderRequest("SKU-DTR-05", 7, "dtr-05-idempotency");
        var committed = await _externalOperations.SubmitAmbiguousAsync(command, cancellationToken).ConfigureAwait(false);
        var replayed = await _externalOperations.SubmitAmbiguousAsync(command, cancellationToken).ConfigureAwait(false);
        var inventoryAfter = await _externalOperations.InventoryAsync(cancellationToken).ConfigureAwait(false);
        Require(
            inventoryBefore.Succeeded && committed.Succeeded && replayed.Succeeded && inventoryAfter.Succeeded &&
            committed.Value == replayed.Value && inventoryBefore.Value - inventoryAfter.Value == command.Quantity,
            $"DTR-05 ambiguous mutation was not deduplicated after retry. " +
            $"before={Describe(inventoryBefore)}, committed={Describe(committed)}, " +
            $"replayed={Describe(replayed)}, after={Describe(inventoryAfter)}.");
        Record("DTR-05");
    }

    private async Task VerifyGrpcFaultsAsync(CancellationToken cancellationToken)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-dtr-grpc");
        var connection = CreateGrpcConnection("dogfood-dtr-grpc", owner, _inProcessServer.Endpoints.Grpc);
        var registration = _connections.Register(connection);
        Require(registration.Succeeded, "The Data connection manager rejected the DTR gRPC connection.");
        try
        {
            await _connections.StartOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
            var client = new NativeGrpcClient(connection, _diagnostics);
            var unavailable = await client.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = StressDataFaults.GrpcUnavailableSku },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Require(
                unavailable.Error?.Kind == DataErrorKind.ServiceUnavailable,
                $"DTR-06 mapped gRPC Unavailable as '{unavailable.Error?.Kind}'.");
            var healthy = await client.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = "SKU-DTR-06" },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Require(healthy.Succeeded, "DTR-06 poisoned the next healthy unary call.");
            Record("DTR-06", expectedFailure: true);

            var deadline = await client.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = StressDataFaults.GrpcDeadlineSku },
                new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddMilliseconds(120) },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Require(
                deadline.Error?.Kind == DataErrorKind.DeadlineExceeded,
                $"DTR-07 mapped gRPC deadline as '{deadline.Error?.Kind}'.");
            Record("DTR-07", expectedFailure: true);

            await using (var stream = client.ServerStreaming(
                PriceStreamMethod,
                new PriceStreamRequest { Sku = StressDataFaults.GrpcServerStreamAbortSku, Count = 8 },
                cancellationToken: cancellationToken))
            {
                var results = new List<DataResult<PriceUpdate>>();
                await foreach (var result in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(result);
                }

                Require(
                    results.Count == 4 &&
                    results.Take(3).All(static result => result.Succeeded) &&
                    results.Take(3).Select(static result => result.Value!.Sequence).SequenceEqual([1L, 2L, 3L]) &&
                    results[^1].Error?.Kind == DataErrorKind.ServiceUnavailable,
                    "DTR-08 did not expose three ordered items followed by a terminal stream failure.");
            }
            Record("DTR-08", expectedFailure: true);

            await using (var duplex = client.DuplexStreaming(
                InventoryDuplexMethod,
                cancellationToken: cancellationToken))
            {
                var results = new List<DataResult<InventoryReply>>();
                var receive = Task.Run(async () =>
                {
                    await foreach (var result in duplex.Responses.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        results.Add(result);
                    }
                }, cancellationToken);
                for (var index = 1; index <= 2; index++)
                {
                    await duplex.WriteAsync(
                        new InventoryDelta
                        {
                            Sku = StressDataFaults.GrpcDuplexAbortSku,
                            Delta = index,
                            Sequence = index,
                        },
                        cancellationToken).ConfigureAwait(false);
                }

                await duplex.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
                await receive.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                Require(
                    results.Count == 3 &&
                    results.Take(2).All(static result => result.Succeeded) &&
                    results.Take(2).Select(static result => result.Value!.Sequence).SequenceEqual([1L, 2L]) &&
                    results[^1].Error?.Kind == DataErrorKind.ServiceUnavailable,
                    "DTR-09 did not expose two ordered duplex replies followed by a terminal failure.");
            }
            Record("DTR-09", expectedFailure: true);
        }
        finally
        {
            await _connections.StopOwnerAsync(owner, CancellationToken.None).ConfigureAwait(false);
            if (registration.Value is not null)
            {
                await registration.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task VerifySignalRFaultsAsync(CancellationToken cancellationToken)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-dtr-signalr");
        var connection = CreateSignalRConnection(
            "dogfood-dtr-signalr",
            owner,
            _inProcessServer.Endpoints.Http,
            [TimeSpan.Zero, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)]);
        var states = new ConcurrentQueue<DataConnectionState>();
        connection.StateChanged += (_, args) => states.Enqueue(args.CurrentState);
        var registration = _connections.Register(connection);
        Require(registration.Succeeded, "The Data connection manager rejected the DTR SignalR connection.");
        try
        {
            await _connections.StartOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
            _ = await connection.InvokeAsync<object?>("DropConnection", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await WaitForAsync(
                () => states.Contains(DataConnectionState.Reconnecting) &&
                      connection.State == DataConnectionState.Connected,
                TimeSpan.FromSeconds(8),
                "DTR-10 SignalR did not reconnect after a server-side disconnect.",
                cancellationToken).ConfigureAwait(false);
            var echo = await connection.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
            Require(echo.Succeeded && echo.Value == 42, "DTR-10 SignalR invoke failed after reconnect.");
            Record("DTR-10", expectedFailure: true);

            var messages = new ConcurrentQueue<StressInventoryPush>();
            await using var subscription = connection.Subscribe<StressInventoryPush>(
                "InventoryChanged",
                (message, _) =>
                {
                    messages.Enqueue(message);
                    return ValueTask.CompletedTask;
                },
                new DataSubscriptionOptions
                {
                    Capacity = 8,
                    BackpressurePolicy = DataBackpressurePolicy.Buffer,
                });
            StressInventoryPush[] sent =
            [
                new("SKU-DTR-11", 10, 2),
                new("SKU-DTR-11", 10, 1),
                new("SKU-DTR-11", 10, 2),
                new("SKU-DTR-11", 10, 3),
            ];
            var published = await connection.InvokeAsync<int>(
                "PublishInventorySequence",
                [sent],
                cancellationToken).ConfigureAwait(false);
            await WaitForAsync(
                () => messages.Count == sent.Length,
                TimeSpan.FromSeconds(5),
                "DTR-11 SignalR sequence was not delivered.",
                cancellationToken).ConfigureAwait(false);
            Require(
                published.Succeeded && published.Value == sent.Length &&
                messages.Select(static message => message.Sequence).SequenceEqual([2L, 1L, 2L, 3L]),
                "DTR-11 SignalR transport hid or reordered duplicate sequence values.");
            Record("DTR-11");
        }
        finally
        {
            await _connections.StopOwnerAsync(owner, CancellationToken.None).ConfigureAwait(false);
            if (registration.Value is not null)
            {
                await registration.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task VerifyExternalProcessRecoveryAsync(CancellationToken cancellationToken)
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-dtr-process");
        var grpc = CreateGrpcConnection("dogfood-dtr-process-grpc", owner, _externalServer.Endpoints.Grpc);
        var signalR = CreateSignalRConnection(
            "dogfood-dtr-process-signalr",
            owner,
            _externalServer.Endpoints.Http,
            [
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
            ]);
        var states = new ConcurrentQueue<DataConnectionState>();
        signalR.StateChanged += (_, args) => states.Enqueue(args.CurrentState);
        var grpcRegistration = _connections.Register(grpc);
        var signalRRegistration = _connections.Register(signalR);
        Require(
            grpcRegistration.Succeeded && signalRRegistration.Succeeded,
            "The Data connection manager rejected external-process connections.");

        try
        {
            await _connections.StartOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
            var grpcClient = new NativeGrpcClient(grpc, _diagnostics);
            var beforeHttp = await _externalOperations.HealthAsync(
                TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var beforeGrpc = await grpcClient.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = "SKU-DTR-PREFLIGHT" },
                new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddSeconds(2) },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var beforeSignalR = await signalR.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
            Require(
                beforeHttp.Succeeded && beforeGrpc.Succeeded && beforeSignalR.Succeeded,
                "External Data server preflight failed before crash injection.");

            await _externalServer.CrashAsync(cancellationToken).ConfigureAwait(false);
            var failedHttp = await _externalOperations.HealthAsync(
                TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            var failedGrpc = await grpcClient.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = "SKU-DTR-CRASH" },
                new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddSeconds(1) },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await WaitForAsync(
                () => states.Contains(DataConnectionState.Reconnecting) ||
                      states.Contains(DataConnectionState.Faulted) ||
                      states.Contains(DataConnectionState.Stopped),
                TimeSpan.FromSeconds(5),
                "SignalR did not expose a disconnected state after the child process crash.",
                cancellationToken).ConfigureAwait(false);
            Require(
                !failedHttp.Succeeded && !failedGrpc.Succeeded && !_externalServer.IsRunning,
                "DTR-12 did not fail all request/response transports after process termination.");
            Record("DTR-12", expectedFailure: true);

            await _externalServer.RestartAsync(cancellationToken).ConfigureAwait(false);
            await WaitForAsync(
                () => signalR.State == DataConnectionState.Connected,
                TimeSpan.FromSeconds(10),
                "SignalR did not reconnect to the restarted child process.",
                cancellationToken).ConfigureAwait(false);
            var afterHttp = await _externalOperations.HealthAsync(
                TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var afterGrpc = await grpcClient.UnaryAsync(
                InventoryMethod,
                new InventoryRequest { Sku = "SKU-DTR-RECOVERED" },
                new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddSeconds(2) },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var afterSignalR = await signalR.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
            Require(
                afterHttp.Succeeded && afterGrpc.Succeeded && afterSignalR.Succeeded && afterSignalR.Value == 42 &&
                _externalServer.RestartCount == 1,
                "DTR-13 transports did not recover against the restarted endpoint.");
            Record("DTR-13");
        }
        finally
        {
            await _connections.StopOwnerAsync(owner, CancellationToken.None).ConfigureAwait(false);
            if (grpcRegistration.Value is not null)
            {
                await grpcRegistration.Value.DisposeAsync().ConfigureAwait(false);
            }

            if (signalRRegistration.Value is not null)
            {
                await signalRRegistration.Value.DisposeAsync().ConfigureAwait(false);
            }

            await _externalServer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static GrpcChannelConnection CreateGrpcConnection(
        string connectionId,
        DataConnectionOwner owner,
        Uri endpoint) =>
        new(
            connectionId,
            owner,
            GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { UseProxy = false },
            }));

    private SignalRRealtimeConnection CreateSignalRConnection(
        string connectionId,
        DataConnectionOwner owner,
        Uri endpoint,
        IReadOnlyList<TimeSpan> reconnectDelays) =>
        SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = connectionId,
                Endpoint = new Uri(endpoint, "/stress-hub"),
                Owner = owner,
                AccessTokenProvider = () => ValueTask.FromResult<string?>("stress/admin/r10"),
                ReconnectDelays = reconnectDelays,
            },
            _diagnostics);

    private void Record(string id, bool expectedFailure = false) =>
        _ledger.Record(
            "data-resilience",
            id,
            expectedFailure ? "expected-failure" : "completed");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string Describe<T>(DataResult<T> result) =>
        result.Succeeded
            ? $"success:{result.Value}"
            : $"{result.Status}:{result.Error?.Kind}:{result.Error?.Exception?.GetType().Name}";

    private static async Task WaitForAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        string message,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate())
            {
                return;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(message);
    }

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>(
        MethodType methodType,
        string methodName,
        MessageParser<TRequest> requestParser,
        MessageParser<TResponse> responseParser)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse> =>
        new(
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
