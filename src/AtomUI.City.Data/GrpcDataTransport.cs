namespace AtomUI.City.Data;

/// <summary>
/// Represents grpc data transport.
/// </summary>
public sealed class GrpcDataTransport : IRequestResponseTransport
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    public DataTransportKind Kind => DataTransportKind.Grpc;

    /// <summary>
    /// Executes the send async&lt;tresponse&gt; operation.
    /// </summary>
    public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request is not GrpcDataRequest<TResponse> grpcRequest)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.PolicyRejected,
                    "gRPC transport requires a gRPC data request."));
        }

        var validation = DataTransportRequestValidator.Validate(request, context, Kind);
        if (validation is not null)
        {
            return validation;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return DataResult<TResponse>.Cancelled();
        }

        try
        {
            var callResult = await grpcRequest
                .Invoker(new GrpcRequestContext(context), cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled();
            }

            if (callResult is null)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.TransportError,
                        "gRPC invoker returned a null result."));
            }

            if (callResult.Succeeded)
            {
                return DataResult<TResponse>.Success(callResult.Value!);
            }

            var error = DataErrorMapper.FromGrpcStatus(callResult.StatusCode, callResult.Detail);

            return error.Kind == DataErrorKind.Cancelled
                ? DataResult<TResponse>.Cancelled(error.Message)
                : DataResult<TResponse>.Failed(error);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (TaskCanceledException exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.Timeout,
                    DataErrorMessage.FromException(exception, "gRPC call timed out."),
                    Exception: exception));
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.TransportError,
                    DataErrorMessage.FromException(exception, "gRPC transport failed."),
                    Exception: exception));
        }
    }
}
