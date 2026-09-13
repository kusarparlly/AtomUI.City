using System.Net.Http.Headers;

namespace AtomUI.City.Data;

public sealed class HttpDataTransport : IRequestResponseTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpDataTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public DataTransportKind Kind => DataTransportKind.Http;

    public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request is not HttpDataRequest<TResponse> httpRequest)
        {
            return DataResult<TResponse>.Failed(
                new DataError(
                    DataErrorKind.PolicyRejected,
                    "HTTP transport requires an HTTP data request."));
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
            var client = _httpClientFactory.CreateClient(httpRequest.ClientName);
            using var message = httpRequest.RequestFactory(context);
            if (message is null)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.TransportError,
                        "HTTP request factory returned a null request message."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled();
            }

            AttachCredential(message, context.Credential);

            using var response = await client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled();
            }

            if (!response.IsSuccessStatusCode)
            {
                return DataResult<TResponse>.Failed(DataErrorMapper.FromHttpStatusCode(response.StatusCode));
            }

            TResponse mappedResponse;
            try
            {
                mappedResponse = await httpRequest
                    .ResponseMapperWithCancellation(response, cancellationToken)
                    .ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return DataResult<TResponse>.Cancelled();
                }
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled();
            }
            catch (HttpRequestException exception)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.TransportError,
                        DataErrorMessage.FromException(exception, "HTTP response body read failed."),
                        Exception: exception));
            }
            catch (IOException exception)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.TransportError,
                        DataErrorMessage.FromException(exception, "HTTP response body read failed."),
                        Exception: exception));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        DataErrorMessage.FromException(exception, "HTTP response mapping failed."),
                        Exception: exception));
            }

            return DataResult<TResponse>.Success(mappedResponse);
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
                    DataErrorMessage.FromException(exception, "HTTP request timed out."),
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
                    DataErrorMessage.FromException(exception, "HTTP transport failed."),
                    Exception: exception));
        }
    }

    private static void AttachCredential(HttpRequestMessage message, DataCredential? credential)
    {
        if (credential is null)
        {
            return;
        }

        message.Headers.Authorization = new AuthenticationHeaderValue(
            credential.Scheme,
            credential.Parameter);
    }
}
