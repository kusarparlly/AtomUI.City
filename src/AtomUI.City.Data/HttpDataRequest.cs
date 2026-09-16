namespace AtomUI.City.Data;

/// <summary>
/// Represents http data request&lt;tresponse&gt;.
/// </summary>
public sealed class HttpDataRequest<TResponse> : DataRequest<TResponse>
{
    /// <summary>
    /// Executes the http data request operation.
    /// </summary>
    public HttpDataRequest(
        string clientId,
        string operationName,
        string clientName,
        Func<DataRequestContext, HttpRequestMessage> requestFactory,
        Func<HttpResponseMessage, ValueTask<TResponse>> responseMapper,
        DataAccessMode accessMode = DataAccessMode.Query)
        : this(
            clientId,
            operationName,
            clientName,
            requestFactory,
            (response, _) => responseMapper(response),
            accessMode)
    {
        ResponseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
    }

    /// <summary>
    /// Executes the http data request operation.
    /// </summary>
    public HttpDataRequest(
        string clientId,
        string operationName,
        string clientName,
        Func<DataRequestContext, HttpRequestMessage> requestFactory,
        Func<HttpResponseMessage, CancellationToken, ValueTask<TResponse>> responseMapper,
        DataAccessMode accessMode = DataAccessMode.Query)
        : base(clientId, operationName, DataTransportKind.Http, accessMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        ClientName = clientName;
        RequestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        ResponseMapperWithCancellation = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
        ResponseMapper = response => ResponseMapperWithCancellation(response, CancellationToken.None);
    }

    /// <summary>
    /// Gets client name.
    /// </summary>
    public string ClientName { get; }

    /// <summary>
    /// Gets request factory.
    /// </summary>
    public Func<DataRequestContext, HttpRequestMessage> RequestFactory { get; }

    /// <summary>
    /// Gets response mapper.
    /// </summary>
    public Func<HttpResponseMessage, ValueTask<TResponse>> ResponseMapper { get; }

    /// <summary>
    /// Gets response mapper with cancellation.
    /// </summary>
    public Func<HttpResponseMessage, CancellationToken, ValueTask<TResponse>> ResponseMapperWithCancellation { get; }
}
