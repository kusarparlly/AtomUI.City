namespace AtomUI.City.Data;

/// <summary>
/// Represents signal rdata request&lt;tresponse&gt;.
/// </summary>
public sealed class SignalRDataRequest<TResponse> : DataRequest<TResponse>
{
    /// <summary>
    /// Executes the signal rdata request operation.
    /// </summary>
    public SignalRDataRequest(
        string clientId,
        string operationName,
        string hubName,
        string methodName,
        Func<SignalRInvocationContext, CancellationToken, ValueTask<TResponse>> invoker,
        DataAccessMode accessMode = DataAccessMode.Query)
        : base(clientId, operationName, DataTransportKind.SignalR, accessMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hubName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        HubName = hubName;
        MethodName = methodName;
        Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <summary>
    /// Gets hub name.
    /// </summary>
    public string HubName { get; }

    /// <summary>
    /// Gets method name.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets invoker.
    /// </summary>
    public Func<SignalRInvocationContext, CancellationToken, ValueTask<TResponse>> Invoker { get; }
}

/// <summary>
/// Represents signal rinvocation context.
/// </summary>
public sealed class SignalRInvocationContext
{
    /// <summary>
    /// Initializes a new instance of the <c>SignalRInvocationContext</c> type.
    /// </summary>
    public SignalRInvocationContext(
        string hubName,
        string methodName,
        DataRequestContext request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hubName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        HubName = hubName;
        MethodName = methodName;
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    /// <summary>
    /// Gets hub name.
    /// </summary>
    public string HubName { get; }

    /// <summary>
    /// Gets method name.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets request.
    /// </summary>
    public DataRequestContext Request { get; }

    /// <summary>
    /// Gets credential.
    /// </summary>
    public DataCredential? Credential => Request.Credential;
}
