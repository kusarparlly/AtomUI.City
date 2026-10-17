namespace AtomUI.City.Data.Tests;

public sealed class DataPluginLifecycleTests
{
    [Fact]
    public void ContributionRejectsCapabilitiesThatWereNotGranted()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient);
        var descriptor = Descriptor(DataTransportKind.Grpc);

        Assert.Throws<UnauthorizedAccessException>(() => lease.RegisterClientDescriptor(descriptor));
    }

    [Fact]
    public async Task RevocationRemovesClientsDescriptorsAndPluginCache()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        lease.RegisterClientDescriptor(Descriptor(DataTransportKind.Http));
        lease.RegisterClient(new PluginClient());
        var cacheKey = PluginCacheKey(lease.ContributionId);
        await fixture.Cache.SetAsync(cacheKey, "cached");

        await lease.RevokeAsync();

        Assert.False(fixture.Descriptors.TryGet("plugin-client", out _));
        Assert.Throws<KeyNotFoundException>(() => fixture.Clients.GetRequiredClient<PluginClient>());
        Assert.False((await fixture.Cache.TryGetAsync<string>(cacheKey)).IsHit);
    }

    [Fact]
    public async Task PluginResilienceStateIsIsolatedByContribution()
    {
        var fixture = new ContributionFixture();
        var firstLease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var secondLease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var transport = new OriginAwareTransport(firstLease.ContributionId);
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var resilience = OpenOnFirstFailure();

        var first = await pipeline.SendAsync(PluginRequest(firstLease, resilience));
        var second = await pipeline.SendAsync(PluginRequest(secondLease, resilience));

        Assert.Equal(DataErrorKind.ServiceUnavailable, first.Error?.Kind);
        Assert.True(second.Succeeded);
        Assert.Equal(2, transport.Attempts);
        await firstLease.RevokeAsync();
        await secondLease.RevokeAsync();
    }

    [Fact]
    public async Task RevokingContributionClearsItsResilienceState()
    {
        var fixture = new ContributionFixture();
        const string contributionId = "reusable-contribution";
        var firstLease = fixture.Registry.BeginContribution(
            "plugin-a",
            contributionId,
            DataCapability.UseDataClient | DataCapability.UseHttpClient).Value!;
        var transport = new FailOnceTransport();
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var resilience = OpenOnFirstFailure();

        var first = await pipeline.SendAsync(PluginRequest(firstLease, resilience));
        await firstLease.RevokeAsync();
        var replacement = fixture.Registry.BeginContribution(
            "plugin-a",
            contributionId,
            DataCapability.UseDataClient | DataCapability.UseHttpClient).Value!;
        var second = await pipeline.SendAsync(PluginRequest(replacement, resilience));

        Assert.Equal(DataErrorKind.ServiceUnavailable, first.Error?.Kind);
        Assert.True(second.Succeeded);
        Assert.Equal(2, transport.Attempts);
        await replacement.RevokeAsync();
    }

    [Fact]
    public async Task RevocationCancelsInflightPluginRequestAndRejectsStaleOrigin()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new BlockingTransport(entered);
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
        };

        var inflight = pipeline.SendAsync(request).AsTask();
        await entered.Task;
        await lease.RevokeAsync();
        var result = await inflight;
        var staleResult = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.PluginUnavailable, result.Error?.Kind);
        Assert.Equal(DataErrorKind.PluginUnavailable, staleResult.Error?.Kind);
    }

    [Fact]
    public async Task DirectPipelineRejectsPluginTransportCapabilityThatWasNotGranted()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient);
        var transport = new CountingTransport();
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.PluginUnavailable, result.Error?.Kind);
        Assert.Equal(0, transport.Attempts);
        await lease.RevokeAsync();
    }

    [Fact]
    public async Task StandalonePipelineRejectsPluginOriginWithoutContributionAuthorizer()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var transport = new CountingTransport();
        using var pipeline = new DataRequestPipeline(transport);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.PluginUnavailable, result.Error?.Kind);
        Assert.Equal(0, transport.Attempts);
        await lease.RevokeAsync();
    }

    [Fact]
    public async Task RevokedOriginCannotBypassAdmissionThroughExistingCacheEntry()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var transport = new CountingTransport();
        using var pipeline = new DataRequestPipeline(
            transport,
            cache: fixture.Cache,
            handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
            Cache = DataCacheOptions.Enabled("plugin-cache"),
        };

        var first = await pipeline.SendAsync(request);
        var cacheKey = DataCacheKey.Create(request, DataAuthenticationMode.Anonymous.ToString());
        Assert.Equal(lease.ContributionId, cacheKey.PluginContributionId);
        Assert.True((await fixture.Cache.TryGetAsync<string>(cacheKey)).IsHit);

        await lease.RevokeAsync();
        var stale = await pipeline.SendAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(DataErrorKind.PluginUnavailable, stale.Error?.Kind);
        Assert.False((await fixture.Cache.TryGetAsync<string>(cacheKey)).IsHit);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task PluginRequestRejectsMismatchedExplicitCacheContribution()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var transport = new CountingTransport();
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
            Cache = DataCacheOptions.Enabled(
                "plugin-cache",
                pluginContributionId: "another-contribution"),
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.Equal(0, transport.Attempts);
        await lease.RevokeAsync();
    }

    [Fact]
    public async Task RevocationStopsOwnedConnection()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient);
        var connection = new RecordingConnection(
            "plugin-connection",
            new DataConnectionOwner(DataConnectionOwnerKind.Plugin, lease.PluginId));
        var registration = await lease.RegisterConnectionAsync(connection);
        Assert.True(registration.Succeeded);

        await lease.RevokeAsync();

        Assert.Equal(1, connection.StopCount);
        Assert.Equal(DataConnectionState.Stopped, connection.State);
    }

    [Fact]
    public async Task RevocationWaitsForCancellationIgnoringInflightOperation()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var transport = new ManuallyReleasedTransport();
        using var pipeline = new DataRequestPipeline(transport, handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
        };

        var inflight = pipeline.SendAsync(request).AsTask();
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var revocation = lease.RevokeAsync().AsTask();

        await Task.Delay(50);
        Assert.False(revocation.IsCompleted);

        transport.Release.TrySetResult();
        var result = await inflight.WaitAsync(TimeSpan.FromSeconds(5));
        await revocation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DataErrorKind.PluginUnavailable, result.Error?.Kind);
    }

    [Fact]
    public async Task HandlerCanRequestItsOwnContributionRevocationWithoutDeadlock()
    {
        var fixture = new ContributionFixture();
        var lease = fixture.Begin(DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var handler = new SelfRevokingHandler(lease);
        lease.RegisterHandler(handler);
        using var pipeline = new DataRequestPipeline(new CountingTransport(), handlerSource: fixture.Registry);
        var request = new DataRequest<string>("plugin-client", "load", DataTransportKind.Http)
        {
            Origin = lease.Origin,
        };

        var result = await pipeline.SendAsync(request).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await lease.RevokeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(handler.ContinuedAfterRevoke);
        Assert.Equal(DataErrorKind.PluginUnavailable, result.Error?.Kind);
        Assert.False(lease.IsActive);
    }

    private static DataClientDescriptor Descriptor(DataTransportKind kind) =>
        new(
            "plugin-client",
            typeof(PluginClient),
            kind,
            "1",
            [new DataOperationDescriptor("load", typeof(object), typeof(string), DataAccessMode.Query)]);

    private static DataCacheKey PluginCacheKey(string contributionId) =>
        new(
            "plugin-client",
            "load",
            DataTransportKind.Http,
            DataAccessMode.Query,
            "fingerprint",
            "Anonymous",
            "principal:1",
            "permission:1",
            contributionId,
            "1",
            "1");

    private static DataResilienceOptions OpenOnFirstFailure() => new()
    {
        CircuitBreaker = new DataCircuitBreakerOptions
        {
            IsEnabled = true,
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMinutes(1),
        },
    };

    private static DataRequest<string> PluginRequest(
        DataContributionLease lease,
        DataResilienceOptions resilience) => new(
        "plugin-client",
        "load",
        DataTransportKind.Http)
    {
        Origin = lease.Origin,
        Resilience = resilience,
    };

    private sealed class ContributionFixture
    {
        public DataClientDescriptorCatalog Descriptors { get; } = new();

        public DataClientRegistry Clients { get; } = new();

        public DataConnectionManager Connections { get; } = new();

        public InMemoryDataRequestCache Cache { get; } = new();

        public DataContributionRegistry Registry { get; }

        public ContributionFixture()
        {
            Registry = new DataContributionRegistry(Descriptors, Clients, Connections, Cache);
        }

        public DataContributionLease Begin(DataCapability capabilities) =>
            Registry.BeginContribution("plugin-a", $"contribution-{Guid.NewGuid():N}", capabilities).Value!;
    }

    private sealed class PluginClient : IDataClient
    {
        public string ClientId => "plugin-client";
    }

    private sealed class BlockingTransport(TaskCompletionSource entered) : IRequestResponseTransport
    {
        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return DataResult<TResponse>.Success(default!);
        }
    }

    private sealed class CountingTransport : IRequestResponseTransport
    {
        public DataTransportKind Kind => DataTransportKind.Http;

        public int Attempts { get; private set; }

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            return ValueTask.FromResult(DataResult<TResponse>.Success(default!));
        }
    }

    private sealed class OriginAwareTransport(string failingContributionId) : IRequestResponseTransport
    {
        public DataTransportKind Kind => DataTransportKind.Http;

        public int Attempts { get; private set; }

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            var result = string.Equals(
                request.Origin.ContributionId,
                failingContributionId,
                StringComparison.Ordinal)
                ? DataResult<string>.Failed(new DataError(DataErrorKind.ServiceUnavailable, "offline"))
                : DataResult<string>.Success("ok");
            return ValueTask.FromResult(result.Cast<TResponse>());
        }
    }

    private sealed class FailOnceTransport : IRequestResponseTransport
    {
        public DataTransportKind Kind => DataTransportKind.Http;

        public int Attempts { get; private set; }

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            var result = Attempts == 1
                ? DataResult<string>.Failed(new DataError(DataErrorKind.ServiceUnavailable, "offline"))
                : DataResult<string>.Success("ok");
            return ValueTask.FromResult(result.Cast<TResponse>());
        }
    }

    private sealed class ManuallyReleasedTransport : IRequestResponseTransport
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return DataResult<TResponse>.Success(default!);
        }
    }

    private sealed class SelfRevokingHandler(DataContributionLease lease) : IDataRequestHandler
    {
        public int Order => 0;

        public bool ContinuedAfterRevoke { get; private set; }

        public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            await lease.RevokeAsync();
            ContinuedAfterRevoke = true;
            return await next(cancellationToken);
        }
    }

    private sealed class RecordingConnection(string connectionId, DataConnectionOwner owner) : IDataConnection
    {
        public string ConnectionId { get; } = connectionId;

        public DataConnectionOwner Owner { get; } = owner;

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StopCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }
}
