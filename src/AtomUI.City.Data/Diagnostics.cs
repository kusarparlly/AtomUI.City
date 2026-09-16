namespace AtomUI.City.Data;

/// <summary>
/// Represents data diagnostic ids.
/// </summary>
public static class DataDiagnosticIds
{
    /// <summary>
    /// Represents the request retry value.
    /// </summary>
    public const string RequestRetry = "AUCDATA001";
    /// <summary>
    /// Represents the connection registered value.
    /// </summary>
    public const string ConnectionRegistered = "AUCDATA002";
    /// <summary>
    /// Represents the connection stopped value.
    /// </summary>
    public const string ConnectionStopped = "AUCDATA003";
    /// <summary>
    /// Represents the request completed value.
    /// </summary>
    public const string RequestCompleted = "AUCDATA004";
    /// <summary>
    /// Represents the request failed value.
    /// </summary>
    public const string RequestFailed = "AUCDATA005";
    /// <summary>
    /// Represents the cache read failed value.
    /// </summary>
    public const string CacheReadFailed = "AUCDATA006";
    /// <summary>
    /// Represents the cache write failed value.
    /// </summary>
    public const string CacheWriteFailed = "AUCDATA007";
    /// <summary>
    /// Represents the cache hit value.
    /// </summary>
    public const string CacheHit = "AUCDATA008";
    /// <summary>
    /// Represents the cache miss value.
    /// </summary>
    public const string CacheMiss = "AUCDATA009";
    /// <summary>
    /// Represents the cache invalidated value.
    /// </summary>
    public const string CacheInvalidated = "AUCDATA010";
    /// <summary>
    /// Represents the client missing value.
    /// </summary>
    public const string ClientMissing = "AUCDATA011";
    /// <summary>
    /// Represents the connection stop failed value.
    /// </summary>
    public const string ConnectionStopFailed = "AUCDATA012";
    /// <summary>
    /// Represents the connection start failed value.
    /// </summary>
    public const string ConnectionStartFailed = "AUCDATA013";
    /// <summary>
    /// Represents the connection started value.
    /// </summary>
    public const string ConnectionStarted = "AUCDATA014";
    /// <summary>
    /// Represents the connection registration rejected value.
    /// </summary>
    public const string ConnectionRegistrationRejected = "AUCDATA015";
    /// <summary>
    /// Represents the client registered value.
    /// </summary>
    public const string ClientRegistered = "AUCDATA016";
    /// <summary>
    /// Represents the client unregistered value.
    /// </summary>
    public const string ClientUnregistered = "AUCDATA017";
    /// <summary>
    /// Represents the client unregistration missing value.
    /// </summary>
    public const string ClientUnregistrationMissing = "AUCDATA018";
    /// <summary>
    /// Represents the request stale suppressed value.
    /// </summary>
    public const string RequestStaleSuppressed = "AUCDATA019";
    /// <summary>
    /// Represents the request cancelled value.
    /// </summary>
    public const string RequestCancelled = "AUCDATA020";
    /// <summary>
    /// Represents the circuit opened value.
    /// </summary>
    public const string CircuitOpened = "AUCDATA021";
    /// <summary>
    /// Represents the circuit rejected value.
    /// </summary>
    public const string CircuitRejected = "AUCDATA022";
    /// <summary>
    /// Represents the rate limit rejected value.
    /// </summary>
    public const string RateLimitRejected = "AUCDATA023";
    /// <summary>
    /// Represents the fallback applied value.
    /// </summary>
    public const string FallbackApplied = "AUCDATA024";
    /// <summary>
    /// Represents the fallback failed value.
    /// </summary>
    public const string FallbackFailed = "AUCDATA025";
    /// <summary>
    /// Represents the backpressure dropped value.
    /// </summary>
    public const string BackpressureDropped = "AUCDATA026";
    /// <summary>
    /// Represents the stream completed value.
    /// </summary>
    public const string StreamCompleted = "AUCDATA027";
    /// <summary>
    /// Represents the stream failed value.
    /// </summary>
    public const string StreamFailed = "AUCDATA028";
    /// <summary>
    /// Represents the contribution registered value.
    /// </summary>
    public const string ContributionRegistered = "AUCDATA029";
    /// <summary>
    /// Represents the contribution revoked value.
    /// </summary>
    public const string ContributionRevoked = "AUCDATA030";
    /// <summary>
    /// Represents the contribution rejected value.
    /// </summary>
    public const string ContributionRejected = "AUCDATA031";
    /// <summary>
    /// Represents the handler failed value.
    /// </summary>
    public const string HandlerFailed = "AUCDATA032";
    /// <summary>
    /// Represents the transfer progress failed value.
    /// </summary>
    public const string TransferProgressFailed = "AUCDATA033";
    /// <summary>
    /// Represents the transfer completed value.
    /// </summary>
    public const string TransferCompleted = "AUCDATA034";
    /// <summary>
    /// Represents the cache invalidation unsupported value.
    /// </summary>
    public const string CacheInvalidationUnsupported = "AUCDATA035";
}

/// <summary>
/// Represents data diagnostic record.
/// </summary>
public sealed record DataDiagnosticRecord(
    string Code,
    string Message,
    DataDiagnosticSeverity Severity,
    Guid? OperationId = null,
    string? ClientId = null,
    string? OperationName = null,
    DataTransportKind? TransportKind = null,
    int? Attempt = null,
    DataErrorKind? ErrorKind = null)
{
    private Guid? _operationId = ValidateOperationId(OperationId);
    private string? _clientId = ValidateOptionalText(ClientId, nameof(ClientId));
    private string? _operationName = ValidateOptionalText(OperationName, nameof(OperationName));

    /// <summary>
    /// Gets or sets code.
    /// </summary>
    public string Code { get; init; } = Require(Code, nameof(Code));

    /// <summary>
    /// Gets or sets message.
    /// </summary>
    public string Message { get; init; } = Require(Message, nameof(Message));

    /// <summary>
    /// Gets or sets severity.
    /// </summary>
    public DataDiagnosticSeverity Severity { get; init; } = Validate(Severity, nameof(Severity));

    /// <summary>
    /// Represents the operation id value.
    /// </summary>
    public Guid? OperationId
    {
        get => _operationId;
        init => _operationId = ValidateOperationId(value);
    }

    /// <summary>
    /// Represents the client id value.
    /// </summary>
    public string? ClientId
    {
        get => _clientId;
        init => _clientId = ValidateOptionalText(value, nameof(ClientId));
    }

    /// <summary>
    /// Represents the operation name value.
    /// </summary>
    public string? OperationName
    {
        get => _operationName;
        init => _operationName = ValidateOptionalText(value, nameof(OperationName));
    }

    /// <summary>
    /// Gets or sets transport kind.
    /// </summary>
    public DataTransportKind? TransportKind { get; init; } = ValidateOptional(TransportKind, nameof(TransportKind));

    /// <summary>
    /// Gets or sets attempt.
    /// </summary>
    public int? Attempt { get; init; } = Attempt is null or >= 0
        ? Attempt
        : throw new ArgumentOutOfRangeException(nameof(Attempt), Attempt, "Diagnostic attempt cannot be negative.");

    /// <summary>
    /// Gets or sets error kind.
    /// </summary>
    public DataErrorKind? ErrorKind { get; init; } = ValidateOptional(ErrorKind, nameof(ErrorKind));

    private static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static string? ValidateOptionalText(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }

    private static Guid? ValidateOperationId(Guid? operationId)
    {
        return operationId is null || operationId != Guid.Empty
            ? operationId
            : throw new ArgumentException("Diagnostic operation id cannot be empty.", nameof(OperationId));
    }

    private static TEnum Validate<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Diagnostic enum value is not supported.");
    }

    private static TEnum? ValidateOptional<TEnum>(TEnum? value, string parameterName)
        where TEnum : struct, Enum
    {
        return value is null ? null : Validate(value.Value, parameterName);
    }
}

/// <summary>
/// Defines the supported data diagnostic severity values.
/// </summary>
public enum DataDiagnosticSeverity
{
    /// <summary>
    /// Represents the trace value.
    /// </summary>
    Trace,
    /// <summary>
    /// Represents the info value.
    /// </summary>
    Info,
    /// <summary>
    /// Represents the warning value.
    /// </summary>
    Warning,
    /// <summary>
    /// Represents the error value.
    /// </summary>
    Error,
}

/// <summary>
/// Defines the contract for idata diagnostics.
/// </summary>
public interface IDataDiagnostics
{
    /// <summary>
    /// Gets records.
    /// </summary>
    IReadOnlyList<DataDiagnosticRecord> Records { get; }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    void Write(DataDiagnosticRecord record);
}

/// <summary>
/// Represents in memory data diagnostics.
/// </summary>
public sealed class InMemoryDataDiagnostics : IDataDiagnostics
{
    /// <summary>
    /// Represents the default capacity value.
    /// </summary>
    public const int DefaultCapacity = 4096;

    private readonly Queue<DataDiagnosticRecord> _records = [];
    private readonly object _syncRoot = new();
    private long _droppedCount;

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryDataDiagnostics</c> type.
    /// </summary>
    public InMemoryDataDiagnostics()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryDataDiagnostics</c> type.
    /// </summary>
    public InMemoryDataDiagnostics(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>
    /// Gets capacity.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Represents the dropped count value.
    /// </summary>
    public long DroppedCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _droppedCount;
            }
        }
    }

    /// <summary>
    /// Represents the records value.
    /// </summary>
    public IReadOnlyList<DataDiagnosticRecord> Records
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    public void Write(DataDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_syncRoot)
        {
            if (_records.Count == Capacity)
            {
                _records.Dequeue();
                _droppedCount++;
            }

            _records.Enqueue(record);
        }
    }
}

internal static class DataDiagnosticWriter
{
    public static void TryWrite(IDataDiagnostics? diagnostics, DataDiagnosticRecord record)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(record);
        }
        catch (Exception)
        {
            // Diagnostics are observational and must not change the data operation outcome.
        }
    }
}
