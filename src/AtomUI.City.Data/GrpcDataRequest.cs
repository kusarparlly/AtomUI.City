namespace AtomUI.City.Data;

/// <summary>
/// Represents grpc data request&lt;tresponse&gt;.
/// </summary>
public class GrpcDataRequest<TResponse> : DataRequest<TResponse>
{
    /// <summary>
    /// Executes the grpc data request operation.
    /// </summary>
    public GrpcDataRequest(
        string clientId,
        string operationName,
        Func<GrpcRequestContext, CancellationToken, ValueTask<GrpcCallResult<TResponse>>> invoker,
        DataAccessMode accessMode = DataAccessMode.Query)
        : base(clientId, operationName, DataTransportKind.Grpc, accessMode)
    {
        Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <summary>
    /// Gets invoker.
    /// </summary>
    public Func<GrpcRequestContext, CancellationToken, ValueTask<GrpcCallResult<TResponse>>> Invoker { get; }
}

/// <summary>
/// Represents grpc request context.
/// </summary>
public sealed class GrpcRequestContext
{
    /// <summary>
    /// Initializes a new instance of the <c>GrpcRequestContext</c> type.
    /// </summary>
    public GrpcRequestContext(DataRequestContext request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    /// <summary>
    /// Gets request.
    /// </summary>
    public DataRequestContext Request { get; }

    /// <summary>
    /// Gets credential.
    /// </summary>
    public DataCredential? Credential => Request.Credential;
}
