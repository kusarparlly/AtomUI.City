namespace AtomUI.City.Data;

/// <summary>
/// Represents signal rdata transport.
/// </summary>
public sealed class SignalRDataTransport : IRequestResponseTransport
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    public DataTransportKind Kind => DataTransportKind.SignalR;

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

        if (request is not SignalRDataRequest<TResponse> signalRRequest)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.PolicyRejected,
                    "SignalR transport requires a SignalR data request."));
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
            var response = await signalRRequest
                .Invoker(
                    new SignalRInvocationContext(
                        signalRRequest.HubName,
                        signalRRequest.MethodName,
                        context),
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled();
            }

            return DataResult<TResponse>.Success(response);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return DataResult<TResponse>.Cancelled();
        }
        catch (SignalRConnectionClosedException exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.ConnectionClosed,
                    DataErrorMessage.FromException(exception, "SignalR connection closed."),
                    Exception: exception));
        }
        catch (SignalRReconnectFailedException exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.ReconnectFailed,
                    DataErrorMessage.FromException(exception, "SignalR reconnect failed."),
                    Exception: exception));
        }
        catch (TaskCanceledException exception)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.Timeout,
                    DataErrorMessage.FromException(exception, "SignalR invocation timed out."),
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
                    DataErrorMessage.FromException(exception, "SignalR transport failed."),
                    Exception: exception));
        }
    }
}
