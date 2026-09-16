using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data request pipeline.
/// </summary>
public sealed class DataRequestPipeline : IDataRequestPipeline, IDisposable
{
    private readonly IReadOnlyDictionary<DataTransportKind, IRequestResponseTransport> _transports;
    private readonly IDataCredentialProvider? _credentialProvider;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly IDataRequestCache? _cache;
    private readonly IDataOperationScheduler _operationScheduler;
    private readonly bool _ownsOperationScheduler;
    private readonly IDataResiliencePolicyProvider _resiliencePolicyProvider;
    private readonly IDataFallbackProvider _fallbackProvider;
    private readonly DataResilienceCoordinator _resilienceCoordinator;
    private readonly IReadOnlyList<IDataRequestHandler> _handlers;
    private readonly IDataRequestHandlerSource? _handlerSource;
    private readonly IDataCapabilityAuthorizer _capabilityAuthorizer;
    private readonly DataRuntimeGate _runtimeGate;
    private readonly bool _ownsRuntimeGate;
    private readonly IDisposable? _resilienceTracking;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>DataRequestPipeline</c> type.
    /// </summary>
    public DataRequestPipeline(
        IRequestResponseTransport transport,
        IDataCredentialProvider? credentialProvider = null,
        IDataDiagnostics? diagnostics = null,
        IDataRequestCache? cache = null,
        IDataOperationScheduler? operationScheduler = null,
        IDataResiliencePolicyProvider? resiliencePolicyProvider = null,
        IDataFallbackProvider? fallbackProvider = null,
        IEnumerable<IDataRequestHandler>? handlers = null,
        IDataRequestHandlerSource? handlerSource = null,
        IDataCapabilityAuthorizer? capabilityAuthorizer = null)
        : this(
            [transport],
            credentialProvider,
            diagnostics,
            cache,
            operationScheduler,
            resiliencePolicyProvider,
            fallbackProvider,
            handlers,
            handlerSource,
            capabilityAuthorizer)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>DataRequestPipeline</c> type.
    /// </summary>
    public DataRequestPipeline(
        IEnumerable<IRequestResponseTransport> transports,
        IDataCredentialProvider? credentialProvider = null,
        IDataDiagnostics? diagnostics = null,
        IDataRequestCache? cache = null,
        IDataOperationScheduler? operationScheduler = null,
        IDataResiliencePolicyProvider? resiliencePolicyProvider = null,
        IDataFallbackProvider? fallbackProvider = null,
        IEnumerable<IDataRequestHandler>? handlers = null,
        IDataRequestHandlerSource? handlerSource = null,
        IDataCapabilityAuthorizer? capabilityAuthorizer = null)
        : this(
            transports,
            credentialProvider,
            diagnostics,
            cache,
            operationScheduler,
            resiliencePolicyProvider,
            fallbackProvider,
            handlers,
            handlerSource,
            capabilityAuthorizer,
            new DataRuntimeGate(),
            ownsRuntimeGate: true)
    {
    }

    internal DataRequestPipeline(
        IEnumerable<IRequestResponseTransport> transports,
        IDataCredentialProvider? credentialProvider,
        IDataDiagnostics? diagnostics,
        IDataRequestCache? cache,
        IDataOperationScheduler? operationScheduler,
        IDataResiliencePolicyProvider? resiliencePolicyProvider,
        IDataFallbackProvider? fallbackProvider,
        IEnumerable<IDataRequestHandler>? handlers,
        IDataRequestHandlerSource? handlerSource,
        IDataCapabilityAuthorizer? capabilityAuthorizer,
        DataRuntimeGate runtimeGate,
        bool ownsRuntimeGate = false)
    {
        ArgumentNullException.ThrowIfNull(transports);

        _transports = CreateTransportMap(transports);
        _credentialProvider = credentialProvider;
        _diagnostics = diagnostics;
        _cache = cache;
        _operationScheduler = operationScheduler ?? new DataOperationScheduler();
        _ownsOperationScheduler = operationScheduler is null;
        _resiliencePolicyProvider = resiliencePolicyProvider ?? new DefaultDataResiliencePolicyProvider();
        _fallbackProvider = fallbackProvider ?? new NoDataFallbackProvider();
        _resilienceCoordinator = new DataResilienceCoordinator(diagnostics);
        var handlerSnapshot = (handlers ?? []).ToArray();
        if (handlerSnapshot.Any(static handler => handler is null))
        {
            throw new ArgumentException("Data request handlers cannot contain null values.", nameof(handlers));
        }

        _handlers = Array.AsReadOnly(handlerSnapshot.OrderBy(static handler => handler.Order).ToArray());
        _handlerSource = handlerSource;
        _resilienceTracking = (handlerSource as DataContributionRegistry)
            ?.TrackResilienceCoordinator(_resilienceCoordinator);
        _capabilityAuthorizer = capabilityAuthorizer
            ?? handlerSource as IDataCapabilityAuthorizer
            ?? new DefaultDataCapabilityAuthorizer();
        _runtimeGate = runtimeGate ?? throw new ArgumentNullException(nameof(runtimeGate));
        _ownsRuntimeGate = ownsRuntimeGate;
    }

    private static IReadOnlyDictionary<DataTransportKind, IRequestResponseTransport> CreateTransportMap(
        IEnumerable<IRequestResponseTransport> transports)
    {
        var transportMap = new Dictionary<DataTransportKind, IRequestResponseTransport>();
        foreach (var transport in transports)
        {
            ArgumentNullException.ThrowIfNull(transport);
            var kind = transport.Kind;
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transports),
                    kind,
                    "Data transport kind is not supported.");
            }

            transportMap.TryAdd(kind, transport);
        }

        return transportMap;
    }

    /// <summary>
    /// Executes the send async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!_runtimeGate.TryEnter(out var requestLease, out var shutdownToken))
        {
            var context = DataRequestContext.Create(request, cancellationToken);
            var rejected = DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                "The Data runtime is stopping and no longer accepts new requests."));
            WriteRequestResultDiagnostic(context, rejected);
            return ValueTask.FromResult(rejected);
        }

        return ExecuteTrackedAsync(request, requestLease!, shutdownToken, cancellationToken);
    }

    private async ValueTask<DataResult<TResponse>> ExecuteTrackedAsync<TResponse>(
        DataRequest<TResponse> request,
        IDisposable requestLease,
        CancellationToken shutdownToken,
        CancellationToken cancellationToken)
    {
        using (requestLease)
        using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                   cancellationToken,
                   shutdownToken))
        {
            return await _operationScheduler.ExecuteAsync(
                    request,
                    operationToken => SendCoreAsync(request, operationToken),
                    linkedCancellation.Token)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_ownsRuntimeGate)
        {
            _runtimeGate.Dispose();
        }

        if (_ownsOperationScheduler)
        {
            (_operationScheduler as IDisposable)?.Dispose();
        }

        _resilienceTracking?.Dispose();
    }

    private async ValueTask<DataResult<TResponse>> SendCoreAsync<TResponse>(
        DataRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        DataResilienceOptions resilience;
        try
        {
            resilience = _resiliencePolicyProvider.GetPolicy(
                request.Resilience.PolicyName,
                request.Resilience) ?? throw new InvalidOperationException("Data resilience policy provider returned null.");
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                DataErrorMessage.FromException(exception, "Data resilience policy resolution failed."),
                Exception: exception));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            var earlyContext = DataRequestContext.Create(request, cancellationToken);
            var cancelledResult = DataResult<TResponse>.Cancelled();
            WriteRequestResultDiagnostic(earlyContext, cancelledResult);

            return cancelledResult;
        }

        using var timeoutCancellation = CreateTimeoutCancellation(resilience, cancellationToken);
        if (!CanUseParentScope(request.ParentScope))
        {
            var earlyContext = DataRequestContext.Create(request, timeoutCancellation.Token);
            var suppressedResult = DataResult<TResponse>.StaleSuppressed();
            WriteStaleSuppressedDiagnostic(earlyContext, suppressedResult);

            return suppressedResult;
        }

        CancellationTokenSource operationCancellation;
        try
        {
            operationCancellation = request.ParentScope is null
                ? CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCancellation.Token,
                    request.ParentScope.CancellationToken);
        }
        catch (ObjectDisposedException) when (request.ParentScope is not null)
        {
            var earlyContext = DataRequestContext.Create(request, timeoutCancellation.Token);
            var suppressedResult = DataResult<TResponse>.StaleSuppressed();
            WriteStaleSuppressedDiagnostic(earlyContext, suppressedResult);

            return suppressedResult;
        }

        using var operationCancellationLease = operationCancellation;
        var operationToken = operationCancellation.Token;
        var context = DataRequestContext.Create(request, operationToken);

        bool isAuthorized;
        try
        {
            isAuthorized = _capabilityAuthorizer.IsAuthorized(
                request.Origin,
                DataCapabilityRules.RequiredFor(request.TransportKind));
        }
        catch (Exception exception)
        {
            var rejected = DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                DataErrorMessage.FromException(exception, "Data capability authorization failed."),
                Exception: exception));
            WriteDiagnostic(
                DataDiagnosticIds.HandlerFailed,
                $"Data capability authorization failed: {exception.Message}",
                context,
                rejected.Error?.Kind);
            WriteRequestResultDiagnostic(context, rejected);
            return rejected;
        }

        if (!isAuthorized)
        {
            var rejected = DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PluginUnavailable,
                "Data request capability was denied."));
            WriteRequestResultDiagnostic(context, rejected);
            return rejected;
        }

        if (request.Origin.Kind == DataRequestOriginKind.Plugin
            && request.Cache.IsEnabled
            && request.Cache.PluginContributionId is not null
            && !string.Equals(
                request.Cache.PluginContributionId,
                request.Origin.ContributionId,
                StringComparison.Ordinal))
        {
            var rejected = DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                "Plugin request cache identity does not match its active contribution."));
            WriteRequestResultDiagnostic(context, rejected);
            return rejected;
        }

        if (!CanUseParentScope(request.ParentScope))
        {
            var suppressedResult = DataResult<TResponse>.StaleSuppressed();
            WriteStaleSuppressedDiagnostic(context, suppressedResult);

            return suppressedResult;
        }

        var credentialResult = await ResolveCredentialAsync(request, context, operationToken).ConfigureAwait(false);
        if (ShouldSuppress(request))
        {
            var suppressedResult = DataResult<TResponse>.StaleSuppressed();
            WriteStaleSuppressedDiagnostic(context, suppressedResult);

            return suppressedResult;
        }

        if (IsOperationTimeout(timeoutCancellation, cancellationToken))
        {
            var timeoutResult = CreateTimeoutResult<TResponse>();
            WriteRequestResultDiagnostic(context, timeoutResult);

            return timeoutResult;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            var cancelledResult = DataResult<TResponse>.Cancelled();
            WriteRequestResultDiagnostic(context, cancelledResult);

            return cancelledResult;
        }

        if (credentialResult is not null)
        {
            WriteRequestResultDiagnostic(context, credentialResult);

            return credentialResult;
        }

        var cacheKey = CreateCacheKey(request, context);
        long? cacheMutationEpoch = null;
        if (cacheKey is not null)
        {
            var cachedResult = await ReadCacheAsync<TResponse>(cacheKey, context, operationToken).ConfigureAwait(false);
            if (cachedResult is not null)
            {
                if (ShouldSuppress(request))
                {
                    var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                    WriteStaleSuppressedDiagnostic(context, suppressedResult);

                    return suppressedResult;
                }

                if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);

                    return timeoutResult;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelledResult = DataResult<TResponse>.Cancelled();
                    WriteRequestResultDiagnostic(context, cancelledResult);

                    return cancelledResult;
                }

                if (cachedResult.Status == DataResultStatus.Cancelled
                    && IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    cachedResult = CreateTimeoutResult<TResponse>();
                }

                WriteRequestResultDiagnostic(context, cachedResult);

                return cachedResult;
            }

            cacheMutationEpoch = (_cache as IDataCacheMutationGuard)?.CaptureMutationEpoch();
        }

        var policyRejection = _resilienceCoordinator.TryEnter(
            request,
            context,
            resilience,
            out var resilienceAdmission);
        using var resilienceAdmissionLease = resilienceAdmission;
        if (policyRejection is not null)
        {
            policyRejection = await ApplyFallbackAsync(
                    request,
                    context,
                    resilience,
                    policyRejection,
                    operationToken)
                .ConfigureAwait(false);
            if (ShouldSuppress(request))
            {
                var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                WriteStaleSuppressedDiagnostic(context, suppressedResult);
                return suppressedResult;
            }

            if (IsOperationTimeout(timeoutCancellation, cancellationToken))
            {
                var timeoutResult = CreateTimeoutResult<TResponse>();
                WriteRequestResultDiagnostic(context, timeoutResult);
                return timeoutResult;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                var cancelledResult = DataResult<TResponse>.Cancelled();
                WriteRequestResultDiagnostic(context, cancelledResult);
                return cancelledResult;
            }

            WriteRequestResultDiagnostic(context, policyRejection);
            return policyRejection;
        }

        var maxAttempts = GetMaxAttempts(resilience);
        var optimisticApplied = false;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            context.Attempt = attempt;

            if (ShouldSuppress(request))
            {
                await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                WriteStaleSuppressedDiagnostic(context, suppressedResult);

                return suppressedResult;
            }

            if (IsOperationTimeout(timeoutCancellation, cancellationToken))
            {
                await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                var timeoutResult = CreateTimeoutResult<TResponse>();
                WriteRequestResultDiagnostic(context, timeoutResult);

                return timeoutResult;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                var cancelledResult = DataResult<TResponse>.Cancelled();
                WriteRequestResultDiagnostic(context, cancelledResult);

                return cancelledResult;
            }

            try
            {
                if (!_transports.TryGetValue(request.TransportKind, out var transport))
                {
                    var missingTransportResult = DataResult<TResponse>.Failed(
                        new DataError(
                            DataErrorKind.PolicyRejected,
                            $"No data transport is registered for '{request.TransportKind}'."));
                    WriteRequestResultDiagnostic(context, missingTransportResult);

                    return missingTransportResult;
                }

                if (!optimisticApplied && request.Consistency.OptimisticUpdate is not null)
                {
                    optimisticApplied = true;
                    var optimisticFailure = await ApplyOptimisticUpdateAsync(request, context, operationToken).ConfigureAwait(false);
                    if (optimisticFailure is not null)
                    {
                        await RollBackOptimisticUpdateAsync(
                                request,
                                context,
                                optimisticApplied,
                                cancellation: optimisticFailure.Status == DataResultStatus.Cancelled)
                            .ConfigureAwait(false);
                        WriteRequestResultDiagnostic(context, optimisticFailure);
                        return optimisticFailure;
                    }
                }

                var result = await InvokeTransportAsync(
                        request,
                        context,
                        transport,
                        operationToken)
                    .ConfigureAwait(false);
                if (result is null)
                {
                    throw new InvalidOperationException("Data transport returned a null result.");
                }

                if (ShouldSuppress(request))
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                    WriteStaleSuppressedDiagnostic(context, suppressedResult);

                    return suppressedResult;
                }

                if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);

                    return timeoutResult;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var cancelledResult = DataResult<TResponse>.Cancelled();
                    WriteRequestResultDiagnostic(context, cancelledResult);

                    return cancelledResult;
                }

                if (result.Status == DataResultStatus.Cancelled
                    && IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);

                    return timeoutResult;
                }

                if (result.Succeeded || !ShouldRetry(request, resilience, result, attempt, maxAttempts))
                {
                    var transportSucceeded = result.Succeeded;
                    _resilienceCoordinator.RecordResult(
                        request,
                        context,
                        resilience,
                        resilienceAdmission,
                        result);
                    result = await ApplyFallbackAsync(request, context, resilience, result, operationToken).ConfigureAwait(false);
                    await FinalizeConsistencyAsync(
                            request,
                            context,
                            transportSucceeded,
                            optimisticApplied,
                            operationToken)
                        .ConfigureAwait(false);
                    await WriteCacheAsync(
                            cacheKey,
                            result,
                            context,
                            request.Cache.TimeToLive,
                            cacheMutationEpoch,
                            operationToken)
                        .ConfigureAwait(false);

                    if (ShouldSuppress(request))
                    {
                        await RollBackCacheAsync(cacheKey, result, context).ConfigureAwait(false);
                        var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                        WriteStaleSuppressedDiagnostic(context, suppressedResult);

                        return suppressedResult;
                    }

                    if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                    {
                        await RollBackCacheAsync(cacheKey, result, context).ConfigureAwait(false);
                        var timeoutResult = CreateTimeoutResult<TResponse>();
                        WriteRequestResultDiagnostic(context, timeoutResult);

                        return timeoutResult;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        await RollBackCacheAsync(cacheKey, result, context).ConfigureAwait(false);
                        var cancelledResult = DataResult<TResponse>.Cancelled();
                        WriteRequestResultDiagnostic(context, cancelledResult);

                        return cancelledResult;
                    }

                    WriteRequestResultDiagnostic(context, result);

                    return result;
                }

                WriteDiagnostic(
                    DataDiagnosticIds.RequestRetry,
                    $"Data operation '{request.OperationName}' retry attempt {attempt}.",
                    context,
                    result.Error?.Kind);
                await DelayBeforeRetryAsync(resilience, operationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                if (ShouldSuppress(request))
                {
                    var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                    WriteStaleSuppressedDiagnostic(context, suppressedResult);

                    return suppressedResult;
                }

                if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);

                    return timeoutResult;
                }

                var cancelledResult = DataResult<TResponse>.Cancelled();
                WriteRequestResultDiagnostic(context, cancelledResult);

                return cancelledResult;
            }
            catch (Exception exception)
            {
                if (ShouldSuppress(request))
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                    WriteStaleSuppressedDiagnostic(context, suppressedResult);

                    return suppressedResult;
                }

                if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);

                    return timeoutResult;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied, cancellation: true).ConfigureAwait(false);
                    var cancelledResult = DataResult<TResponse>.Cancelled();
                    WriteRequestResultDiagnostic(context, cancelledResult);

                    return cancelledResult;
                }

                var failedResult = DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.TransportError,
                        DataErrorMessage.FromException(exception, "Data transport failed."),
                        Exception: exception));

                if (ShouldRetry(request, resilience, failedResult, attempt, maxAttempts))
                {
                    WriteDiagnostic(
                        DataDiagnosticIds.RequestRetry,
                        $"Data operation '{request.OperationName}' retry attempt {attempt}.",
                        context,
                        failedResult.Error?.Kind);

                    await DelayBeforeRetryAsync(resilience, operationToken).ConfigureAwait(false);

                    continue;
                }

                _resilienceCoordinator.RecordResult(
                    request,
                    context,
                    resilience,
                    resilienceAdmission,
                    failedResult);
                failedResult = await ApplyFallbackAsync(
                        request,
                        context,
                        resilience,
                        failedResult,
                        operationToken)
                    .ConfigureAwait(false);
                await FinalizeConsistencyAsync(
                        request,
                        context,
                        transportSucceeded: false,
                        optimisticApplied,
                        operationToken)
                    .ConfigureAwait(false);

                if (ShouldSuppress(request))
                {
                    var suppressedResult = DataResult<TResponse>.StaleSuppressed();
                    WriteStaleSuppressedDiagnostic(context, suppressedResult);
                    return suppressedResult;
                }

                if (IsOperationTimeout(timeoutCancellation, cancellationToken))
                {
                    var timeoutResult = CreateTimeoutResult<TResponse>();
                    WriteRequestResultDiagnostic(context, timeoutResult);
                    return timeoutResult;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelledResult = DataResult<TResponse>.Cancelled();
                    WriteRequestResultDiagnostic(context, cancelledResult);
                    return cancelledResult;
                }

                WriteRequestResultDiagnostic(context, failedResult);

                return failedResult;
            }
        }

        var emptyResult = DataResult<TResponse>.Failed(
            new DataError(DataErrorKind.Unknown, "Data operation did not produce a result."));
        await RollBackOptimisticUpdateAsync(request, context, optimisticApplied).ConfigureAwait(false);
        WriteRequestResultDiagnostic(context, emptyResult);

        return emptyResult;
    }

    private async ValueTask<DataResult<TResponse>?> ResolveCredentialAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken)
    {
        if (request.Authentication.Mode == DataAuthenticationMode.Anonymous)
        {
            return null;
        }

        if (_credentialProvider is null)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.CredentialUnavailable,
                    "No data credential provider is registered."));
        }

        DataCredentialResult credentialResult;

        try
        {
            credentialResult = await _credentialProvider
                .GetCredentialAsync(
                    new DataAuthenticationContext(
                        request.ClientId,
                        request.OperationName,
                        request.Authentication),
                    cancellationToken)
                .ConfigureAwait(false);
            if (credentialResult is null)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.CredentialUnavailable,
                        "Data credential provider returned a null result."));
            }
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.CredentialUnavailable,
                    DataErrorMessage.FromException(exception, "Data credential provider failed."),
                    Exception: exception));
        }

        switch (credentialResult.Status)
        {
            case DataCredentialResultStatus.None:
                return null;
            case DataCredentialResultStatus.Success:
                context.SetCredential(credentialResult.Credential!);
                return null;
            case DataCredentialResultStatus.Required:
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.AuthenticationRequired,
                        credentialResult.Message ?? "Authentication is required."));
            case DataCredentialResultStatus.Expired:
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.AuthenticationExpired,
                        credentialResult.Message ?? "Authentication has expired."));
            case DataCredentialResultStatus.Cancelled:
                return DataResult<TResponse>.Cancelled();
            default:
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.CredentialUnavailable,
                        credentialResult.Message ?? "Credential is unavailable."));
        }
    }

    private static CancellationTokenSource CreateTimeoutCancellation(
        DataResilienceOptions resilience,
        CancellationToken cancellationToken)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (resilience.Timeout is { } timeout)
        {
            cancellation.CancelAfter(timeout);
        }

        return cancellation;
    }

    private static bool IsOperationTimeout(
        CancellationTokenSource timeoutCancellation,
        CancellationToken cancellationToken)
    {
        return timeoutCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
    }

    private static DataResult<TResponse> CreateTimeoutResult<TResponse>()
    {
        return DataResult<TResponse>.Failed(
            new DataError(DataErrorKind.Timeout, "Data operation timed out."));
    }

    private static bool CanUseParentScope(LifecycleScope? parentScope)
    {
        return parentScope is null || parentScope.State == LifecycleScopeState.Running;
    }

    private static bool ShouldSuppress<TResponse>(DataRequest<TResponse> request)
    {
        return request.ParentScope is not null && request.ParentScope.State != LifecycleScopeState.Running;
    }

    private static int GetMaxAttempts(DataResilienceOptions resilience)
    {
        return resilience.MaxRetryAttempts == int.MaxValue
            ? int.MaxValue
            : resilience.MaxRetryAttempts + 1;
    }

    private static bool ShouldRetry<TResponse>(
        DataRequest<TResponse> request,
        DataResilienceOptions resilience,
        DataResult<TResponse> result,
        int attempt,
        int maxAttempts)
    {
        if (result.Status != DataResultStatus.Failed
            || attempt >= maxAttempts
            || result.Error is null)
        {
            return false;
        }

        if (!IsRetryAllowedForAccessMode(request, resilience))
        {
            return false;
        }

        return result.Error.Kind is
            DataErrorKind.NetworkUnavailable or
            DataErrorKind.ServiceUnavailable or
            DataErrorKind.Timeout or
            DataErrorKind.TransportError or
            DataErrorKind.ServerError or
            DataErrorKind.DeadlineExceeded or
            DataErrorKind.Unavailable;
    }

    private static bool IsRetryAllowedForAccessMode<TResponse>(
        DataRequest<TResponse> request,
        DataResilienceOptions resilience)
    {
        return request.AccessMode != DataAccessMode.Mutation
            || resilience.AllowMutationRetry
            || !string.IsNullOrWhiteSpace(request.IdempotencyKey);
    }

    private async ValueTask<DataResult<TResponse>> InvokeTransportAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        IRequestResponseTransport transport,
        CancellationToken cancellationToken)
    {
        IDataRequestHandler[] handlers;
        try
        {
            var dynamicHandlers = _handlerSource?.GetHandlers(request) ?? [];
            if (dynamicHandlers.Any(static handler => handler is null))
            {
                throw new InvalidOperationException("Data request handler source returned a null handler.");
            }

            handlers = _handlers
                .Concat(dynamicHandlers)
                .OrderBy(static handler => handler.Order)
                .ToArray();
        }
        catch (Exception exception)
        {
            WriteDiagnostic(
                DataDiagnosticIds.HandlerFailed,
                $"Data request handler resolution failed: {exception.Message}",
                context,
                DataErrorKind.PolicyRejected);
            return DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.PolicyRejected,
                DataErrorMessage.FromException(exception, "Data request handler resolution failed."),
                Exception: exception));
        }

        DataResult<TResponse> result;
        try
        {
            result = await DataRequestHandlerPipeline.InvokeAsync(
                    handlers,
                    request,
                    context,
                    async handlerCancellationToken =>
                    {
                        handlerCancellationToken.ThrowIfCancellationRequested();
                        var transportResult = await transport
                            .SendAsync(request, context, handlerCancellationToken)
                            .ConfigureAwait(false);
                        handlerCancellationToken.ThrowIfCancellationRequested();
                        return transportResult;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteDiagnostic(
                DataDiagnosticIds.HandlerFailed,
                $"Data request handler failed: {exception.Message}",
                context,
                DataErrorKind.TransportError);
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static ValueTask DelayBeforeRetryAsync(
        DataResilienceOptions resilience,
        CancellationToken cancellationToken)
    {
        return resilience.RetryDelay <= TimeSpan.Zero
            ? ValueTask.CompletedTask
            : new ValueTask(Task.Delay(resilience.RetryDelay, cancellationToken));
    }

    private async ValueTask<DataResult<TResponse>> ApplyFallbackAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataResilienceOptions resilience,
        DataResult<TResponse> result,
        CancellationToken cancellationToken)
    {
        if (!resilience.EnableFallback
            || result.Status != DataResultStatus.Failed
            || result.Error is null
            || result.Error.Kind is DataErrorKind.AuthenticationRequired
                or DataErrorKind.AuthenticationExpired
                or DataErrorKind.AuthorizationForbidden
                or DataErrorKind.Cancelled)
        {
            return result;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fallback = await _fallbackProvider
                .TryGetFallbackAsync(request, context, result.Error, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (fallback is null || !fallback.HasFallback || fallback.Result is null)
            {
                return result;
            }

            WriteDiagnostic(
                DataDiagnosticIds.FallbackApplied,
                $"Fallback applied for data operation '{request.OperationName}'.",
                context,
                result.Error.Kind);
            return fallback.Result;
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            WriteDiagnostic(
                DataDiagnosticIds.FallbackFailed,
                $"Fallback failed for data operation '{request.OperationName}': {exception.Message}",
                context,
                result.Error.Kind);
            return result;
        }
    }

    private async ValueTask<DataResult<TResponse>?> ApplyOptimisticUpdateAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await request.Consistency.OptimisticUpdate!
                .ApplyAsync(context, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.LocalStorageError,
                DataErrorMessage.FromException(exception, "Optimistic update failed."),
                Exception: exception));
        }
    }

    private async ValueTask FinalizeConsistencyAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        bool transportSucceeded,
        bool optimisticApplied,
        CancellationToken cancellationToken)
    {
        if (optimisticApplied)
        {
            if (transportSucceeded)
            {
                try
                {
                    await request.Consistency.OptimisticUpdate!
                        .ConfirmAsync(context, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    WriteDiagnostic(
                        DataDiagnosticIds.HandlerFailed,
                        $"Optimistic update confirmation failed: {exception.Message}",
                        context,
                        DataErrorKind.LocalStorageError);
                    await RollBackOptimisticUpdateAsync(request, context, optimisticApplied: true).ConfigureAwait(false);
                }
            }
            else if (request.Consistency.RollBackOnCancellation
                     || !cancellationToken.IsCancellationRequested)
            {
                await RollBackOptimisticUpdateAsync(request, context, optimisticApplied: true).ConfigureAwait(false);
            }
        }

        if (!transportSucceeded
            || request.Consistency.InvalidationsOnSuccess.Count == 0
            || _cache is not IDataCacheInvalidator invalidator)
        {
            return;
        }

        foreach (var invalidation in request.Consistency.InvalidationsOnSuccess)
        {
            try
            {
                await invalidator.InvalidateAsync(invalidation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                WriteCacheDiagnostic(
                    DataDiagnosticIds.CacheWriteFailed,
                    $"Data cache invalidation failed: {exception.Message}",
                    context);
            }
        }
    }

    private async ValueTask RollBackOptimisticUpdateAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        bool optimisticApplied,
        bool cancellation = false)
    {
        if (!optimisticApplied || (cancellation && !request.Consistency.RollBackOnCancellation))
        {
            return;
        }

        try
        {
            await request.Consistency.OptimisticUpdate!
                .RollBackAsync(context, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteDiagnostic(
                DataDiagnosticIds.HandlerFailed,
                $"Optimistic update rollback failed: {exception.Message}",
                context,
                DataErrorKind.LocalStorageError);
        }
    }

    private DataCacheKey? CreateCacheKey<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context)
    {
        if (_cache is null || request.AccessMode != DataAccessMode.Query || !request.Cache.IsEnabled)
        {
            return null;
        }

        return DataCacheKey.Create(request, GetAuthenticationScheme(request, context));
    }

    private static string GetAuthenticationScheme<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context)
    {
        return context.Credential?.Scheme ?? request.Authentication.Mode.ToString();
    }

    private async ValueTask<DataResult<TResponse>?> ReadCacheAsync<TResponse>(
        DataCacheKey cacheKey,
        DataRequestContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var lookup = await _cache!
                .TryGetAsync<TResponse>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (!lookup.IsHit)
            {
                WriteCacheDiagnostic(
                    DataDiagnosticIds.CacheMiss,
                    $"Data operation '{context.OperationName}' cache miss.",
                    context);

                return null;
            }

            WriteCacheDiagnostic(
                DataDiagnosticIds.CacheHit,
                $"Data operation '{context.OperationName}' cache hit.",
                context);

            return DataResult<TResponse>.Success(lookup.Value!);
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            WriteCacheDiagnostic(
                DataDiagnosticIds.CacheReadFailed,
                $"Data operation '{context.OperationName}' cache read failed: {exception.Message}",
                context);

            return null;
        }
    }

    private async ValueTask WriteCacheAsync<TResponse>(
        DataCacheKey? cacheKey,
        DataResult<TResponse> result,
        DataRequestContext context,
        TimeSpan? timeToLive,
        long? cacheMutationEpoch,
        CancellationToken cancellationToken)
    {
        if (cacheKey is null || !result.Succeeded)
        {
            return;
        }

        try
        {
            var entryOptions = new DataCacheEntryOptions { TimeToLive = timeToLive };
            if (_cache is IDataCacheMutationGuard mutationGuard
                && cacheMutationEpoch is { } expectedEpoch)
            {
                var written = await mutationGuard
                    .TrySetIfUnchangedAsync(
                        cacheKey,
                        result.Value,
                        entryOptions,
                        expectedEpoch,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!written)
                {
                    WriteCacheDiagnostic(
                        DataDiagnosticIds.CacheInvalidated,
                        $"Data operation '{context.OperationName}' skipped a stale cache write after invalidation.",
                        context);
                }
            }
            else if (_cache is IDataExpiringRequestCache expiringCache)
            {
                await expiringCache
                    .SetAsync(
                        cacheKey,
                        result.Value,
                        entryOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _cache!
                    .SetAsync(cacheKey, result.Value, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException exception)
        {
            WriteCacheDiagnostic(
                DataDiagnosticIds.CacheWriteFailed,
                $"Data operation '{context.OperationName}' cache write was cancelled unexpectedly: {exception.Message}",
                context);
        }
        catch (Exception exception)
        {
            WriteCacheDiagnostic(
                DataDiagnosticIds.CacheWriteFailed,
                $"Data operation '{context.OperationName}' cache write failed: {exception.Message}",
                context);
        }
    }

    private async ValueTask RollBackCacheAsync<TResponse>(
        DataCacheKey? cacheKey,
        DataResult<TResponse> result,
        DataRequestContext context)
    {
        if (cacheKey is null || !result.Succeeded)
        {
            return;
        }

        try
        {
            await _cache!.InvalidateAsync(cacheKey, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteCacheDiagnostic(
                DataDiagnosticIds.CacheWriteFailed,
                $"Data operation '{context.OperationName}' cache rollback failed: {exception.Message}",
                context);
        }
    }

    private void WriteDiagnostic(
        string code,
        string message,
        DataRequestContext context,
        DataErrorKind? errorKind = null)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            code,
            message,
            DataDiagnosticSeverity.Trace,
            context.OperationId,
            context.ClientId,
            context.OperationName,
            context.TransportKind,
            context.Attempt,
            errorKind));
    }

    private void WriteCacheDiagnostic(
        string code,
        string message,
        DataRequestContext context)
    {
        var severity = code switch
        {
            DataDiagnosticIds.CacheHit or DataDiagnosticIds.CacheMiss => DataDiagnosticSeverity.Trace,
            DataDiagnosticIds.CacheInvalidated => DataDiagnosticSeverity.Info,
            _ => DataDiagnosticSeverity.Warning,
        };

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            code,
            message,
            severity,
            context.OperationId,
            context.ClientId,
            context.OperationName,
            context.TransportKind,
            context.Attempt));
    }

    private void WriteRequestResultDiagnostic<TResponse>(
        DataRequestContext context,
        DataResult<TResponse> result)
    {
        var code = result.Status switch
        {
            DataResultStatus.Success => DataDiagnosticIds.RequestCompleted,
            DataResultStatus.Cancelled => DataDiagnosticIds.RequestCancelled,
            DataResultStatus.StaleSuppressed => DataDiagnosticIds.RequestStaleSuppressed,
            _ => DataDiagnosticIds.RequestFailed,
        };
        var severity = result.Status is DataResultStatus.Failed or DataResultStatus.Partial
            ? DataDiagnosticSeverity.Warning
            : DataDiagnosticSeverity.Trace;
        var message = result.Status switch
        {
            DataResultStatus.Success => $"Data operation '{context.OperationName}' completed.",
            DataResultStatus.Partial => $"Data operation '{context.OperationName}' completed partially.",
            DataResultStatus.Cancelled => $"Data operation '{context.OperationName}' was cancelled.",
            DataResultStatus.StaleSuppressed => $"Data operation '{context.OperationName}' result was suppressed.",
            _ => $"Data operation '{context.OperationName}' failed.",
        };

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            code,
            message,
            severity,
            context.OperationId,
            context.ClientId,
            context.OperationName,
            context.TransportKind,
            context.Attempt,
            result.Error?.Kind));
    }

    private void WriteStaleSuppressedDiagnostic<TResponse>(
        DataRequestContext context,
        DataResult<TResponse> result)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.RequestStaleSuppressed,
            $"Data operation '{context.OperationName}' result was suppressed.",
            DataDiagnosticSeverity.Trace,
            context.OperationId,
            context.ClientId,
            context.OperationName,
            context.TransportKind,
            context.Attempt,
            result.Error?.Kind));
    }
}
