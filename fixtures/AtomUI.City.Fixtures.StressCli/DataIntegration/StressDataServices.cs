using System.Collections.Concurrent;
using System.Net.Http.Json;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Data;
using AtomUI.City.Security;
using Grpc.Net.Client;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

public interface IStressAccessTokenSession
{
    StressPrincipalSnapshot Current { get; }

    string CurrentToken { get; }

    (StressPrincipalSnapshot Previous, StressPrincipalSnapshot Current) Switch(string principal);
}

public sealed class StressAccessTokenSession : IStressAccessTokenSession, IAccessTokenProvider
{
    private readonly object _syncRoot = new();
    private string _principal = "user-a";
    private long _revision = 1;

    public StressPrincipalSnapshot Current
    {
        get
        {
            lock (_syncRoot)
            {
                return CreateSnapshot();
            }
        }
    }

    public string CurrentToken
    {
        get
        {
            lock (_syncRoot)
            {
                return CreateToken();
            }
        }
    }

    public (StressPrincipalSnapshot Previous, StressPrincipalSnapshot Current) Switch(string principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        lock (_syncRoot)
        {
            var previous = CreateSnapshot();
            _principal = principal;
            _revision++;
            return (previous, CreateSnapshot());
        }
    }

    public ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return ValueTask.FromResult(AccessTokenResult.Success(
                CreateToken(),
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(10)));
        }
    }

    private string CreateToken() => $"stress/{_principal}/{_revision}";

    private StressPrincipalSnapshot CreateSnapshot() => new(_principal, $"r{_revision}");
}

public interface IStressDataRequestProbe
{
    int InvocationCount { get; }

    IReadOnlyCollection<Guid> OperationIds { get; }
}

public sealed class StressDataRequestHandler : IDataRequestHandler, IStressDataRequestProbe
{
    private readonly ConcurrentDictionary<Guid, byte> _operationIds = new();
    private int _invocationCount;

    public int Order => -1_000;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public IReadOnlyCollection<Guid> OperationIds => _operationIds.Keys.ToArray();

    public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _invocationCount);
        _operationIds.TryAdd(context.OperationId, 0);
        context.Items["stress-correlation"] = context.OperationId.ToString("N");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

public interface IStressRemoteOperations
{
    ValueTask<DataResult<StressProductSnapshot>> GetProductAsync(
        string sku,
        LifecycleScope? parentScope = null,
        DataResilienceOptions? resilience = null,
        CancellationToken cancellationToken = default);

    ValueTask<DataResult<StressOrderReceipt>> SubmitOrderAsync(
        StressSubmitOrderRequest command,
        IDataOptimisticUpdate? optimisticUpdate = null,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default);

    ValueTask<DataResult<string>> SearchAsync(
        StressSearchRequest request,
        DataConcurrencyPolicy policy,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default);

    ValueTask<DataResult<StressPrincipalSnapshot>> GetPrincipalAsync(
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default);

    ValueTask<DataResult<string>> DelayAsync(
        int milliseconds,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default);
}

public sealed class StressRemoteOperations(
    IDataRequestPipeline pipeline,
    IStressAccessTokenSession tokenSession) : IStressRemoteOperations
{
    public const string ClientId = "stress-operations";
    public const string ClientName = "stress-backend";

    public ValueTask<DataResult<StressProductSnapshot>> GetProductAsync(
        string sku,
        LifecycleScope? parentScope = null,
        DataResilienceOptions? resilience = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        var principal = tokenSession.Current;
        var request = new HttpDataRequest<StressProductSnapshot>(
            ClientId,
            "get-product",
            ClientName,
            context => CreateGet(context, $"/api/products/{Uri.EscapeDataString(sku)}"),
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<StressProductSnapshot>(cancellationToken: token)
                    .ConfigureAwait(false))!,
            DataAccessMode.Query)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            Cache = DataCacheOptions.Enabled(
                sku,
                principal.Revision,
                clientVersion: "1",
                policyVersion: "stress-v1",
                timeToLive: TimeSpan.FromMinutes(1)),
            Resilience = resilience ?? DataResilienceOptions.None,
            ParentScope = parentScope,
        };
        return pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<StressOrderReceipt>> SubmitOrderAsync(
        StressSubmitOrderRequest command,
        IDataOptimisticUpdate? optimisticUpdate = null,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = new HttpDataRequest<StressOrderReceipt>(
            ClientId,
            "submit-order",
            ClientName,
            context =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
                {
                    Content = JsonContent.Create(command),
                };
                message.Headers.TryAddWithoutValidation("x-request-id", command.RequestId);
                AddCorrelation(message, context);
                return message;
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
                OptimisticUpdate = optimisticUpdate,
                InvalidationsOnSuccess =
                [
                    DataCacheInvalidation.ForOperation(
                        ClientId,
                        "get-product",
                        DataCacheInvalidationReason.Mutation),
                ],
            },
            ParentScope = parentScope,
        };
        return pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<string>> SearchAsync(
        StressSearchRequest search,
        DataConcurrencyPolicy policy,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        var request = new HttpDataRequest<string>(
            ClientId,
            "search-orders",
            ClientName,
            context => CreateGet(
                context,
                $"/api/search/{Uri.EscapeDataString(search.Term)}?delay={search.DelayMilliseconds}"),
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            Concurrency = new DataConcurrencyOptions
            {
                Policy = policy,
                OperationKey = "search-orders",
                ResourceKey = "orders",
                MaximumQueueLength = 512,
            },
            ParentScope = parentScope,
        };
        return pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<StressPrincipalSnapshot>> GetPrincipalAsync(
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<StressPrincipalSnapshot>(
            ClientId,
            "get-principal",
            ClientName,
            context => CreateGet(context, "/api/principal"),
            static async (response, token) =>
                (await response.Content.ReadFromJsonAsync<StressPrincipalSnapshot>(cancellationToken: token)
                    .ConfigureAwait(false))!)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            ParentScope = parentScope,
        };
        return pipeline.SendAsync(request, cancellationToken);
    }

    public ValueTask<DataResult<string>> DelayAsync(
        int milliseconds,
        LifecycleScope? parentScope = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpDataRequest<string>(
            ClientId,
            "delay",
            ClientName,
            context => CreateGet(context, $"/api/delay/{milliseconds}"),
            static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
        {
            ParentScope = parentScope,
        };
        return pipeline.SendAsync(request, cancellationToken);
    }

    private static HttpRequestMessage CreateGet(DataRequestContext context, string uri)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, uri);
        AddCorrelation(message, context);
        return message;
    }

    private static void AddCorrelation(HttpRequestMessage message, DataRequestContext context)
    {
        if (context.Items.TryGetValue("stress-correlation", out var correlation) && correlation is string value)
        {
            message.Headers.TryAddWithoutValidation("x-correlation-id", value);
        }
    }
}

public interface IStressDataConnectionFactory
{
    GrpcChannelConnection CreateGrpc(StressDataEndpoints endpoints, DataConnectionOwner owner);

    SignalRRealtimeConnection CreateSignalR(StressDataEndpoints endpoints, DataConnectionOwner owner);
}

public sealed class StressDataConnectionFactory(IStressAccessTokenSession tokenSession) : IStressDataConnectionFactory
{
    public GrpcChannelConnection CreateGrpc(StressDataEndpoints endpoints, DataConnectionOwner owner)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var channel = GrpcChannel.ForAddress(endpoints.Grpc, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { UseProxy = false },
        });
        return new GrpcChannelConnection($"stress-grpc-{Guid.NewGuid():N}", owner, channel);
    }

    public SignalRRealtimeConnection CreateSignalR(StressDataEndpoints endpoints, DataConnectionOwner owner)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return SignalRRealtimeConnection.Create(new SignalRConnectionOptions
        {
            ConnectionId = $"stress-signalr-{Guid.NewGuid():N}",
            Endpoint = new Uri(endpoints.Http, "/stress-hub"),
            Owner = owner,
            JsonContract = StressSignalRJson.Contract,
            AccessTokenProvider = () => ValueTask.FromResult<string?>(tokenSession.CurrentToken),
            ReconnectDelays = [TimeSpan.Zero, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100)],
        });
    }
}
