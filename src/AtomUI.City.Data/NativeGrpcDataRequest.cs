using Grpc.Core;

namespace AtomUI.City.Data;

/// <summary>
/// Represents native grpc data request&lt;trequest, tresponse&gt;.
/// </summary>
public sealed class NativeGrpcDataRequest<TRequest, TResponse> : GrpcDataRequest<TResponse>
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// Executes the native grpc data request operation.
    /// </summary>
    public NativeGrpcDataRequest(
        string clientId,
        string operationName,
        NativeGrpcClient client,
        Method<TRequest, TResponse> method,
        TRequest payload,
        GrpcCallOptions? options = null,
        DataAccessMode accessMode = DataAccessMode.Query)
        : base(
            clientId,
            operationName,
            (context, cancellationToken) => InvokeAsync(
                client,
                method,
                payload,
                options,
                context,
                cancellationToken),
            accessMode)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Options = options ?? GrpcCallOptions.Default;
    }

    /// <summary>
    /// Gets client.
    /// </summary>
    public NativeGrpcClient Client { get; }

    /// <summary>
    /// Gets method.
    /// </summary>
    public Method<TRequest, TResponse> Method { get; }

    /// <summary>
    /// Gets payload.
    /// </summary>
    public TRequest Payload { get; }

    /// <summary>
    /// Gets options.
    /// </summary>
    public GrpcCallOptions Options { get; }

    private static async ValueTask<GrpcCallResult<TResponse>> InvokeAsync(
        NativeGrpcClient client,
        Method<TRequest, TResponse> method,
        TRequest payload,
        GrpcCallOptions? options,
        GrpcRequestContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(payload);

        var result = await client
            .UnaryAsync(method, payload, options, context.Credential, cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            return GrpcCallResult<TResponse>.Success(result.Value!);
        }

        var status = result.Error?.Kind switch
        {
            DataErrorKind.AuthenticationRequired => GrpcStatusCode.Unauthenticated,
            DataErrorKind.AuthorizationForbidden => GrpcStatusCode.PermissionDenied,
            DataErrorKind.NotFound => GrpcStatusCode.NotFound,
            DataErrorKind.Conflict => GrpcStatusCode.Aborted,
            DataErrorKind.ValidationFailed => GrpcStatusCode.InvalidArgument,
            DataErrorKind.DeadlineExceeded or DataErrorKind.Timeout => GrpcStatusCode.DeadlineExceeded,
            DataErrorKind.ServiceUnavailable or DataErrorKind.Unavailable => GrpcStatusCode.Unavailable,
            DataErrorKind.Cancelled => GrpcStatusCode.Cancelled,
            _ => GrpcStatusCode.Unknown,
        };
        return GrpcCallResult<TResponse>.Failed(status, result.Error?.Message);
    }
}
