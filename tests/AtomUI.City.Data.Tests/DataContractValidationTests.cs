namespace AtomUI.City.Data.Tests;

public sealed class DataContractValidationTests
{
    [Fact]
    public void RequestHandlerCallbackRequiresExplicitCancellationToken()
    {
        var method = typeof(IDataRequestHandler).GetMethod(nameof(IDataRequestHandler.InvokeAsync));

        Assert.NotNull(method);
        var cancellationToken = Assert.Single(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.False(cancellationToken.IsOptional);
    }

    [Fact]
    public void RequestRejectsUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataRequest<string>("client", "operation", (DataTransportKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataRequest<string>("client", "operation", DataTransportKind.Http, (DataAccessMode)999));
    }

    [Fact]
    public void RequestRejectsNullOptionsAndBlankIdempotencyKey()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DataRequest<string>("client", "operation", DataTransportKind.Http)
            {
                Authentication = null!,
            });
        Assert.Throws<ArgumentNullException>(() =>
            new DataRequest<string>("client", "operation", DataTransportKind.Http)
            {
                Cache = null!,
            });
        Assert.Throws<ArgumentNullException>(() =>
            new DataRequest<string>("client", "operation", DataTransportKind.Http)
            {
                Resilience = null!,
            });
        Assert.Throws<ArgumentException>(() =>
            new DataRequest<string>("client", "operation", DataTransportKind.Http)
            {
                IdempotencyKey = " ",
            });
    }

    [Fact]
    public void RequestAndContextItemsSupportDeclaredNullableValues()
    {
        var request = new DataRequest<string>("client", "operation", DataTransportKind.Http);
        var context = DataRequestContext.Create(request);

        request.Items["nullable"] = null;
        context.Items["nullable"] = null;

        Assert.True(request.Items.ContainsKey("nullable"));
        Assert.True(context.Items.ContainsKey("nullable"));
        Assert.Null(request.Items["nullable"]);
        Assert.Null(context.Items["nullable"]);
    }

    [Fact]
    public void ResilienceOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataResilienceOptions { Timeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataResilienceOptions { MaxRetryAttempts = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataResilienceOptions { Scope = (DataResiliencePolicyScope)999 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataConcurrencyOptions { Policy = (DataConcurrencyPolicy)999 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataStreamOptions { BackpressurePolicy = (DataBackpressurePolicy)999 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataSubscriptionOptions { ErrorPolicy = (DataSubscriptionErrorPolicy)999 });
    }

    [Fact]
    public void CacheOptionsRejectBlankPluginContributionId()
    {
        Assert.Throws<ArgumentException>(() =>
            DataCacheOptions.Enabled("items:v1", pluginContributionId: " "));
    }

    [Fact]
    public void CredentialAndOwnerRejectInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new DataCredential(" ", "token"));
        Assert.Throws<ArgumentException>(() => new DataCredential("Bearer", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataConnectionOwner((DataConnectionOwnerKind)999, "owner"));
        Assert.Throws<ArgumentException>(() => DataCredentialResult.Required(" "));
        Assert.Throws<ArgumentException>(() => DataCredentialResult.Expired(" "));
        Assert.Throws<ArgumentException>(() => DataCredentialResult.Unavailable(" "));
    }

    [Fact]
    public void GrpcFailureRejectsSuccessAndUnknownStatusCodes()
    {
        Assert.Throws<ArgumentException>(() => GrpcCallResult<string>.Failed(GrpcStatusCode.OK));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrpcCallResult<string>.Failed((GrpcStatusCode)999));
        Assert.Throws<ArgumentException>(() =>
            GrpcCallResult<string>.Failed(GrpcStatusCode.Internal, " "));
        Assert.Throws<ArgumentException>(() =>
            DataErrorMapper.FromGrpcStatus(GrpcStatusCode.Internal, " "));
    }

    [Fact]
    public void SignalRRequestCanDeclareMutationAccess()
    {
        var request = new SignalRDataRequest<string>(
            "client",
            "send-message",
            "ChatHub",
            "SendMessage",
            (_, _) => ValueTask.FromResult("sent"),
            DataAccessMode.Mutation);

        Assert.Equal(DataAccessMode.Mutation, request.AccessMode);
    }

    [Fact]
    public void TransferContractsRejectInvalidPublicValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataTransferOptions { RangeUnsupportedPolicy = (DataRangeUnsupportedPolicy)999 });
        Assert.Throws<ArgumentException>(() =>
            new DataTransferReceipt(Guid.Empty, 0, System.Net.HttpStatusCode.OK));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataTransferReceipt(Guid.NewGuid(), -1, System.Net.HttpStatusCode.OK));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataTransferReceipt(Guid.NewGuid(), 0, (System.Net.HttpStatusCode)999));
    }

    [Fact]
    public void GrpcCallOptionsRejectInvalidDeadlineAndStream()
    {
        Assert.Throws<ArgumentException>(() =>
            new GrpcCallOptions { DeadlineUtc = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local) });
        Assert.Throws<ArgumentNullException>(() =>
            new GrpcCallOptions { Stream = null! });
    }

    [Fact]
    public void NativeLongConnectionsRequireOwnerAndAbsoluteEndpoint()
    {
        using var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost");
        Assert.Throws<ArgumentException>(() =>
            new GrpcChannelConnection("grpc", DataConnectionOwner.None, channel));

        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "app");
        Assert.Throws<ArgumentException>(() => SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = "signalr",
                Endpoint = new Uri("relative", UriKind.Relative),
                Owner = owner,
                JsonContract = SignalRJsonContract.Create(SignalRTestJsonContext.Default),
            }));
        Assert.Throws<ArgumentException>(() => SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = "signalr",
                Endpoint = new Uri("https://localhost/hub"),
                Owner = DataConnectionOwner.None,
                JsonContract = SignalRJsonContract.Create(SignalRTestJsonContext.Default),
            }));
    }

    [Fact]
    public void PipelineRejectsTransportWithUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataRequestPipeline(new UnknownKindTransport()));
    }

    [Fact]
    public void ConnectionManagerRejectsUnknownConnectionState()
    {
        var manager = new DataConnectionManager();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.Register(new UnknownStateConnection()));
    }

    [Fact]
    public void DescriptorAndStateEventRejectInconsistentMetadata()
    {
        Assert.Throws<ArgumentException>(() => new DataOperationDescriptor(
            "save",
            typeof(object),
            typeof(string),
            DataAccessMode.Mutation,
            cacheEnabled: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DataConnectionStateChangedEventArgs(
            (DataConnectionState)999,
            DataConnectionState.Connected));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DataConnectionStateChangedEventArgs(
            DataConnectionState.Created,
            (DataConnectionState)999));
    }

    [Fact]
    public void MisleadingCompatibilityContractsAreMarkedObsolete()
    {
        var registrationConstructor = typeof(DataConnectionRegistration)
            .GetConstructor([typeof(IDataConnection)]);
        var streamCompleted = typeof(DataErrorKind).GetField("StreamCompleted");

        Assert.NotNull(registrationConstructor);
        Assert.NotNull(registrationConstructor.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Single());
        Assert.NotNull(streamCompleted);
        Assert.NotNull(streamCompleted.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Single());
    }

    private sealed class UnknownKindTransport : IRequestResponseTransport
    {
        public DataTransportKind Kind => (DataTransportKind)999;

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class UnknownStateConnection : IDataConnection
    {
        public string ConnectionId => "unknown-state";

        public DataConnectionOwner Owner { get; } =
            new(DataConnectionOwnerKind.Manual, "unknown-state-owner");

        public DataConnectionState State => (DataConnectionState)999;

        public ValueTask StartAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
