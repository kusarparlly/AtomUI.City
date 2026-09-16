using System.Collections.Concurrent;

namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data resilience policy scope values.
/// </summary>
public enum DataResiliencePolicyScope
{
    /// <summary>
    /// Represents the operation value.
    /// </summary>
    Operation,
    /// <summary>
    /// Represents the client value.
    /// </summary>
    Client,
    /// <summary>
    /// Represents the global value.
    /// </summary>
    Global,
}

/// <summary>
/// Represents data circuit breaker options.
/// </summary>
public sealed class DataCircuitBreakerOptions
{
    private int _failureThreshold = 5;
    private TimeSpan _breakDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Represents the failure threshold value.
    /// </summary>
    public int FailureThreshold
    {
        get => _failureThreshold;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(FailureThreshold));
            _failureThreshold = value;
        }
    }

    /// <summary>
    /// Represents the break duration value.
    /// </summary>
    public TimeSpan BreakDuration
    {
        get => _breakDuration;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(BreakDuration), value, "Circuit break duration must be greater than zero.");
            }

            _breakDuration = value;
        }
    }

    /// <summary>
    /// Gets disabled.
    /// </summary>
    public static DataCircuitBreakerOptions Disabled { get; } = new();
}

/// <summary>
/// Represents data rate limit options.
/// </summary>
public sealed class DataRateLimitOptions
{
    private int _permitLimit = 100;
    private TimeSpan _window = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Represents the permit limit value.
    /// </summary>
    public int PermitLimit
    {
        get => _permitLimit;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(PermitLimit));
            _permitLimit = value;
        }
    }

    /// <summary>
    /// Represents the window value.
    /// </summary>
    public TimeSpan Window
    {
        get => _window;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Window), value, "Rate-limit window must be greater than zero.");
            }

            _window = value;
        }
    }

    /// <summary>
    /// Gets disabled.
    /// </summary>
    public static DataRateLimitOptions Disabled { get; } = new();
}

/// <summary>
/// Defines the contract for idata resilience policy provider.
/// </summary>
public interface IDataResiliencePolicyProvider
{
    /// <summary>
    /// Executes the get policy operation.
    /// </summary>
    DataResilienceOptions GetPolicy(string? policyName, DataResilienceOptions requestPolicy);
}

/// <summary>
/// Represents default data resilience policy provider.
/// </summary>
public sealed class DefaultDataResiliencePolicyProvider : IDataResiliencePolicyProvider
{
    /// <summary>
    /// Executes the get policy operation.
    /// </summary>
    public DataResilienceOptions GetPolicy(string? policyName, DataResilienceOptions requestPolicy)
    {
        ArgumentNullException.ThrowIfNull(requestPolicy);
        return requestPolicy;
    }
}

/// <summary>
/// Represents data fallback result&lt;tresponse&gt;.
/// </summary>
public sealed class DataFallbackResult<TResponse>
{
    private DataFallbackResult(bool hasFallback, DataResult<TResponse>? result)
    {
        HasFallback = hasFallback;
        Result = result;
    }

    /// <summary>
    /// Gets a value indicating whether has fallback.
    /// </summary>
    public bool HasFallback { get; }

    /// <summary>
    /// Gets result.
    /// </summary>
    public DataResult<TResponse>? Result { get; }

    /// <summary>
    /// Gets none.
    /// </summary>
    public static DataFallbackResult<TResponse> None() => new(false, null);

    /// <summary>
    /// Executes the from result operation.
    /// </summary>
    public static DataFallbackResult<TResponse> FromResult(DataResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DataFallbackResult<TResponse>(true, result);
    }
}

/// <summary>
/// Defines the contract for idata fallback provider.
/// </summary>
public interface IDataFallbackProvider
{
    /// <summary>
    /// Executes the try get fallback async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataFallbackResult<TResponse>> TryGetFallbackAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataError error,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents no data fallback provider.
/// </summary>
public sealed class NoDataFallbackProvider : IDataFallbackProvider
{
    /// <summary>
    /// Executes the try get fallback async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask<DataFallbackResult<TResponse>> TryGetFallbackAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataError error,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DataFallbackResult<TResponse>.None());
    }
}

internal sealed class DataResilienceCoordinator
{
    private readonly ConcurrentDictionary<PolicyIdentity, CircuitState> _circuits = new();
    private readonly ConcurrentDictionary<PolicyIdentity, RateLimitState> _rateLimits = new();
    private readonly TimeProvider _timeProvider;
    private readonly IDataDiagnostics? _diagnostics;

    public DataResilienceCoordinator(IDataDiagnostics? diagnostics = null, TimeProvider? timeProvider = null)
    {
        _diagnostics = diagnostics;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DataResult<TResponse>? TryEnter<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataResilienceOptions options,
        out Admission admission)
    {
        admission = Admission.None;
        var key = CreatePolicyIdentity(request, options);
        var now = _timeProvider.GetUtcNow();

        if (options.RateLimit.IsEnabled)
        {
            var limiter = _rateLimits.GetOrAdd(key, _ => new RateLimitState(now));
            if (!limiter.TryAcquire(now, options.RateLimit))
            {
                Write(DataDiagnosticIds.RateLimitRejected, "Data operation was rejected by its rate limit.", context);
                return DataResult<TResponse>.Failed(
                    new DataError(DataErrorKind.PolicyRejected, "Data operation rate limit exceeded."));
            }
        }

        if (options.CircuitBreaker.IsEnabled)
        {
            var circuit = _circuits.GetOrAdd(key, static _ => new CircuitState());
            if (!circuit.TryEnter(now, out var generation))
            {
                Write(DataDiagnosticIds.CircuitRejected, "Data operation was rejected because its circuit is open.", context);
                return DataResult<TResponse>.Failed(
                    new DataError(DataErrorKind.ServiceUnavailable, "Data operation circuit is open."));
            }

            admission = new Admission(circuit, generation);
        }

        return null;
    }

    public void RecordResult<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataResilienceOptions options,
        Admission admission,
        DataResult<TResponse> result)
    {
        if (!options.CircuitBreaker.IsEnabled)
        {
            return;
        }

        if (result.Succeeded || !IsTransient(result.Error?.Kind))
        {
            admission.RecordSuccess();
            return;
        }

        if (admission.RecordFailure(
                _timeProvider.GetUtcNow(),
                options.CircuitBreaker.FailureThreshold,
                options.CircuitBreaker.BreakDuration))
        {
            Write(DataDiagnosticIds.CircuitOpened, "Data operation circuit opened after repeated failures.", context);
        }
    }

    private static bool IsTransient(DataErrorKind? kind) => kind is
        DataErrorKind.NetworkUnavailable or
        DataErrorKind.ServiceUnavailable or
        DataErrorKind.Timeout or
        DataErrorKind.TransportError or
        DataErrorKind.ServerError or
        DataErrorKind.DeadlineExceeded or
        DataErrorKind.Unavailable or
        DataErrorKind.ConnectionFailed or
        DataErrorKind.ReconnectFailed;

    public void InvalidateContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        foreach (var entry in _circuits)
        {
            if (string.Equals(entry.Key.ContributionId, contributionId, StringComparison.Ordinal))
            {
                _circuits.TryRemove(new KeyValuePair<PolicyIdentity, CircuitState>(entry.Key, entry.Value));
            }
        }

        foreach (var entry in _rateLimits)
        {
            if (string.Equals(entry.Key.ContributionId, contributionId, StringComparison.Ordinal))
            {
                _rateLimits.TryRemove(new KeyValuePair<PolicyIdentity, RateLimitState>(entry.Key, entry.Value));
            }
        }
    }

    private static PolicyIdentity CreatePolicyIdentity<TResponse>(
        DataRequest<TResponse> request,
        DataResilienceOptions options) => options.Scope switch
        {
            DataResiliencePolicyScope.Global => new PolicyIdentity(
                options.PolicyName ?? request.Resilience.PolicyName,
                request.Origin.Kind,
                request.Origin.ContributionId,
                null,
                null),
            DataResiliencePolicyScope.Client => new PolicyIdentity(
                options.PolicyName ?? request.Resilience.PolicyName,
                request.Origin.Kind,
                request.Origin.ContributionId,
                request.ClientId,
                null),
            DataResiliencePolicyScope.Operation => new PolicyIdentity(
                options.PolicyName ?? request.Resilience.PolicyName,
                request.Origin.Kind,
                request.Origin.ContributionId,
                request.ClientId,
                request.OperationName),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Scope), options.Scope, "Resilience policy scope is not supported."),
        };

    private void Write(string code, string message, DataRequestContext context)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            code,
            message,
            DataDiagnosticSeverity.Warning,
            context.OperationId,
            context.ClientId,
            context.OperationName,
            context.TransportKind,
            context.Attempt));
    }

    internal sealed class CircuitState
    {
        private readonly object _syncRoot = new();
        private int _failureCount;
        private DateTimeOffset? _openUntil;
        private bool _halfOpenProbe;
        private long _generation;

        public bool TryEnter(DateTimeOffset now, out long generation)
        {
            lock (_syncRoot)
            {
                if (_openUntil is null)
                {
                    generation = _generation;
                    return true;
                }

                if (_openUntil > now || _halfOpenProbe)
                {
                    generation = default;
                    return false;
                }

                _halfOpenProbe = true;
                generation = ++_generation;
                return true;
            }
        }

        public void RecordSuccess(long generation)
        {
            lock (_syncRoot)
            {
                if (generation != _generation)
                {
                    return;
                }

                _failureCount = 0;
                _openUntil = null;
                if (_halfOpenProbe)
                {
                    _generation++;
                }

                _halfOpenProbe = false;
            }
        }

        public bool RecordFailure(
            long generation,
            DateTimeOffset now,
            int threshold,
            TimeSpan duration)
        {
            lock (_syncRoot)
            {
                if (generation != _generation)
                {
                    return false;
                }

                if (_halfOpenProbe)
                {
                    _failureCount = 0;
                    _openUntil = now.Add(duration);
                    _halfOpenProbe = false;
                    _generation++;
                    return true;
                }

                _halfOpenProbe = false;
                _failureCount++;
                if (_failureCount < threshold)
                {
                    return false;
                }

                _failureCount = 0;
                _openUntil = now.Add(duration);
                _generation++;
                return true;
            }
        }

        public void Abandon(long generation)
        {
            lock (_syncRoot)
            {
                if (_halfOpenProbe && generation == _generation)
                {
                    _halfOpenProbe = false;
                }
            }
        }
    }

    internal sealed class Admission : IDisposable
    {
        private readonly CircuitState? _circuit;
        private readonly long _generation;
        private int _completed;

        private Admission()
        {
        }

        internal Admission(CircuitState circuit, long generation)
        {
            _circuit = circuit;
            _generation = generation;
        }

        public static Admission None { get; } = new();

        public void RecordSuccess()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _circuit?.RecordSuccess(_generation);
            }
        }

        public bool RecordFailure(DateTimeOffset now, int threshold, TimeSpan duration)
        {
            return Interlocked.Exchange(ref _completed, 1) == 0
                && _circuit is not null
                && _circuit.RecordFailure(_generation, now, threshold, duration);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _circuit?.Abandon(_generation);
            }
        }
    }

    private sealed class RateLimitState(DateTimeOffset windowStart)
    {
        private readonly object _syncRoot = new();
        private DateTimeOffset _windowStart = windowStart;
        private int _permits;

        public bool TryAcquire(DateTimeOffset now, DataRateLimitOptions options)
        {
            lock (_syncRoot)
            {
                if (now - _windowStart >= options.Window)
                {
                    _windowStart = now;
                    _permits = 0;
                }

                if (_permits >= options.PermitLimit)
                {
                    return false;
                }

                _permits++;
                return true;
            }
        }
    }

    private readonly record struct PolicyIdentity(
        string? PolicyName,
        DataRequestOriginKind OriginKind,
        string? ContributionId,
        string? ClientId,
        string? OperationName);
}
