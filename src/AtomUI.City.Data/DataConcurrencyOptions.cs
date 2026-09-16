namespace AtomUI.City.Data;

/// <summary>
/// Represents data concurrency options.
/// </summary>
public sealed class DataConcurrencyOptions
{
    private string? _operationKey;
    private string? _resourceKey;
    private int _maximumQueueLength = 256;
    private DataConcurrencyPolicy _policy;

    /// <summary>
    /// Represents the policy value.
    /// </summary>
    public DataConcurrencyPolicy Policy
    {
        get => _policy;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Policy), value, "Data concurrency policy is not supported.");
            }

            _policy = value;
        }
    }

    /// <summary>
    /// Represents the operation key value.
    /// </summary>
    public string? OperationKey
    {
        get => _operationKey;
        init => _operationKey = ValidateOptional(value, nameof(OperationKey));
    }

    /// <summary>
    /// Represents the resource key value.
    /// </summary>
    public string? ResourceKey
    {
        get => _resourceKey;
        init => _resourceKey = ValidateOptional(value, nameof(ResourceKey));
    }

    /// <summary>
    /// Represents the maximum queue length value.
    /// </summary>
    public int MaximumQueueLength
    {
        get => _maximumQueueLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(MaximumQueueLength));
            _maximumQueueLength = value;
        }
    }

    /// <summary>
    /// Gets allow concurrent.
    /// </summary>
    public static DataConcurrencyOptions AllowConcurrent { get; } = new();

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }
}

/// <summary>
/// Represents the operation used to data operation delegate&lt;tresponse&gt;.
/// </summary>
public delegate ValueTask<DataResult<TResponse>> DataOperationDelegate<TResponse>(
    CancellationToken cancellationToken);

/// <summary>
/// Defines the contract for idata operation scheduler.
/// </summary>
public interface IDataOperationScheduler
{
    /// <summary>
    /// Executes the execute async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataResult<TResponse>> ExecuteAsync<TResponse>(
        DataRequest<TResponse> request,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken = default);
}
