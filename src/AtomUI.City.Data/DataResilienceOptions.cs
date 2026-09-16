namespace AtomUI.City.Data;

/// <summary>
/// Represents data resilience options.
/// </summary>
public sealed class DataResilienceOptions
{
    private TimeSpan? _timeout;
    private int _maxRetryAttempts;
    private TimeSpan _retryDelay;
    private string? _policyName;
    private DataCircuitBreakerOptions _circuitBreaker = DataCircuitBreakerOptions.Disabled;
    private DataRateLimitOptions _rateLimit = DataRateLimitOptions.Disabled;
    private DataResiliencePolicyScope _scope;

    /// <summary>
    /// Represents the timeout value.
    /// </summary>
    public TimeSpan? Timeout
    {
        get => _timeout;
        init
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), value, "Timeout must be greater than zero.");
            }

            _timeout = value;
        }
    }

    /// <summary>
    /// Represents the max retry attempts value.
    /// </summary>
    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MaxRetryAttempts));
            _maxRetryAttempts = value;
        }
    }

    /// <summary>
    /// Gets or sets allow mutation retry.
    /// </summary>
    public bool AllowMutationRetry { get; init; }

    /// <summary>
    /// Represents the retry delay value.
    /// </summary>
    public TimeSpan RetryDelay
    {
        get => _retryDelay;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(RetryDelay), value, "Retry delay cannot be negative.");
            }

            _retryDelay = value;
        }
    }

    /// <summary>
    /// Represents the policy name value.
    /// </summary>
    public string? PolicyName
    {
        get => _policyName;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(PolicyName));
            }

            _policyName = value;
        }
    }

    /// <summary>
    /// Represents the scope value.
    /// </summary>
    public DataResiliencePolicyScope Scope
    {
        get => _scope;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Scope), value, "Data resilience policy scope is not supported.");
            }

            _scope = value;
        }
    }

    /// <summary>
    /// Represents the circuit breaker value.
    /// </summary>
    public DataCircuitBreakerOptions CircuitBreaker
    {
        get => _circuitBreaker;
        init => _circuitBreaker = value ?? throw new ArgumentNullException(nameof(CircuitBreaker));
    }

    /// <summary>
    /// Represents the rate limit value.
    /// </summary>
    public DataRateLimitOptions RateLimit
    {
        get => _rateLimit;
        init => _rateLimit = value ?? throw new ArgumentNullException(nameof(RateLimit));
    }

    /// <summary>
    /// Gets or sets enable fallback.
    /// </summary>
    public bool EnableFallback { get; init; }

    /// <summary>
    /// Gets none.
    /// </summary>
    public static DataResilienceOptions None { get; } = new();
}
