using System.Collections.Concurrent;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data request context.
/// </summary>
public sealed class DataRequestContext
{
    private readonly object _request;

    private DataRequestContext(
        object request,
        Guid operationId,
        string clientId,
        string operationName,
        DataTransportKind transportKind,
        DataAccessMode accessMode,
        CancellationToken cancellationToken)
    {
        _request = request;
        OperationId = operationId;
        CorrelationId = operationId.ToString("D");
        ClientId = clientId;
        OperationName = operationName;
        TransportKind = transportKind;
        AccessMode = accessMode;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets operation id.
    /// </summary>
    public Guid OperationId { get; }

    /// <summary>
    /// Gets correlation id.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets transport kind.
    /// </summary>
    public DataTransportKind TransportKind { get; }

    /// <summary>
    /// Gets access mode.
    /// </summary>
    public DataAccessMode AccessMode { get; }

    /// <summary>
    /// Gets or sets attempt.
    /// </summary>
    public int Attempt { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets or sets credential.
    /// </summary>
    public DataCredential? Credential { get; private set; }

    /// <summary>
    /// Gets items.
    /// </summary>
    public IDictionary<string, object?> Items { get; } =
        new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Executes the set credential operation.
    /// </summary>
    public void SetCredential(DataCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        Credential = credential;
    }

    internal bool BelongsTo<TResponse>(DataRequest<TResponse> request)
    {
        return ReferenceEquals(_request, request);
    }

    /// <summary>
    /// Executes the create&lt;tresponse&gt; operation.
    /// </summary>
    public static DataRequestContext Create<TResponse>(
        DataRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new DataRequestContext(
            request,
            Guid.NewGuid(),
            request.ClientId,
            request.OperationName,
            request.TransportKind,
            request.AccessMode,
            cancellationToken);
    }
}
