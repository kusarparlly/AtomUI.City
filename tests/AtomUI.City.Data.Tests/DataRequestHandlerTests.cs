namespace AtomUI.City.Data.Tests;

public sealed class DataRequestHandlerTests
{
    [Fact]
    public async Task HandlersWrapTransportInDeterministicOrder()
    {
        var calls = new List<string>();
        var transport = new DelegateTransport(() =>
        {
            calls.Add("transport");
            return DataResult<string>.Success("ok");
        });
        using var pipeline = new DataRequestPipeline(
            transport,
            handlers: [new RecordingHandler(20, "second", calls), new RecordingHandler(10, "first", calls)]);

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "handlers",
            "ordered",
            DataTransportKind.Http));

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["first:before", "second:before", "transport", "second:after", "first:after"],
            calls);
    }

    [Fact]
    public async Task HandlerCanRejectBeforeTransport()
    {
        var transport = new DelegateTransport(() => DataResult<string>.Success("unexpected"));
        using var pipeline = new DataRequestPipeline(transport, handlers: [new RejectingHandler()]);

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "handlers",
            "reject",
            DataTransportKind.Http));

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task HandlerSourceFailureIsDiagnosedAndDoesNotReachTransportOrRetry()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var transport = new DelegateTransport(() => DataResult<string>.Success("unexpected"));
        using var pipeline = new DataRequestPipeline(
            transport,
            diagnostics: diagnostics,
            handlerSource: new ThrowingHandlerSource());
        var request = new DataRequest<string>("handlers", "source-failure", DataTransportKind.Http)
        {
            Resilience = new DataResilienceOptions { MaxRetryAttempts = 3 },
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.Equal(0, transport.CallCount);
        Assert.Contains(diagnostics.Records, record => record.Code == DataDiagnosticIds.HandlerFailed);
    }

    [Fact]
    public async Task CapabilityAuthorizerFailureBecomesPolicyResult()
    {
        var transport = new DelegateTransport(() => DataResult<string>.Success("unexpected"));
        using var pipeline = new DataRequestPipeline(
            transport,
            capabilityAuthorizer: new ThrowingAuthorizer());

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "handlers",
            "authorization-failure",
            DataTransportKind.Http));

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.IsType<InvalidOperationException>(result.Error?.Exception);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task HandlerContinuationCanBeInvokedOnlyOnce()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var transport = new DelegateTransport(() => DataResult<string>.Success("ok"));
        using var pipeline = new DataRequestPipeline(
            transport,
            diagnostics: diagnostics,
            handlers: [new DoubleNextHandler()]);

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "handlers",
            "double-next",
            DataTransportKind.Http,
            DataAccessMode.Mutation));

        Assert.Equal(DataErrorKind.TransportError, result.Error?.Kind);
        Assert.Equal(1, transport.CallCount);
        Assert.Contains(diagnostics.Records, record => record.Code == DataDiagnosticIds.HandlerFailed);
    }

    private sealed class RecordingHandler(int order, string name, IList<string> calls) : IDataRequestHandler
    {
        public int Order => order;

        public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            calls.Add($"{name}:before");
            var result = await next(cancellationToken);
            calls.Add($"{name}:after");
            return result;
        }
    }

    private sealed class RejectingHandler : IDataRequestHandler
    {
        public int Order => 0;

        public ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.PolicyRejected, "rejected")));
    }

    private sealed class ThrowingHandlerSource : IDataRequestHandlerSource
    {
        public IReadOnlyList<IDataRequestHandler> GetHandlers<TResponse>(DataRequest<TResponse> request) =>
            throw new InvalidOperationException("handler source failed");
    }

    private sealed class ThrowingAuthorizer : IDataCapabilityAuthorizer
    {
        public bool IsAuthorized(DataRequestOrigin origin, DataCapability capability) =>
            throw new InvalidOperationException("authorizer failed");
    }

    private sealed class DoubleNextHandler : IDataRequestHandler
    {
        public int Order => 0;

        public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            _ = await next(cancellationToken);
            return await next(cancellationToken);
        }
    }

    private sealed class DelegateTransport(Func<DataResult<string>> handler) : IRequestResponseTransport
    {
        public int CallCount { get; private set; }

        public DataTransportKind Kind => DataTransportKind.Http;

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(handler().Cast<TResponse>());
        }
    }
}
