using AtomUI.City.Security;

namespace AtomUI.City.Data;

/// <summary>
/// Represents access token credential provider.
/// </summary>
public sealed class AccessTokenCredentialProvider : IDataCredentialProvider
{
    private readonly IAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <c>AccessTokenCredentialProvider</c> type.
    /// </summary>
    public AccessTokenCredentialProvider(IAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        _accessTokenProvider = accessTokenProvider;
    }

    /// <summary>
    /// Executes the get credential async operation.
    /// </summary>
    public async ValueTask<DataCredentialResult> GetCredentialAsync(
        DataAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Authentication.Mode == DataAuthenticationMode.Anonymous)
        {
            return DataCredentialResult.None();
        }

        AccessTokenResult token;
        try
        {
            token = await _accessTokenProvider
                .GetTokenAsync(
                    new AccessTokenRequest(
                        context.ClientId,
                        context.Authentication.Scheme,
                        context.OperationName),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return DataCredentialResult.Unavailable(
                DataErrorMessage.FromException(exception, "Access token provider failed."));
        }

        if (token is null)
        {
            return DataCredentialResult.Unavailable("Access token provider returned a null result.");
        }

        return token.Status switch
        {
            AccessTokenResultStatus.None => DataCredentialResult.None(),
            AccessTokenResultStatus.Success => DataCredentialResult.Success(
                new DataCredential(token.Scheme!, token.Token!)),
            AccessTokenResultStatus.Required => DataCredentialResult.Required(token.Message),
            AccessTokenResultStatus.Expired => DataCredentialResult.Expired(token.Message),
            AccessTokenResultStatus.Failed => DataCredentialResult.Unavailable(token.Message),
            AccessTokenResultStatus.Unavailable => DataCredentialResult.Unavailable(token.Message),
            AccessTokenResultStatus.Cancelled => DataCredentialResult.Cancelled(token.Message),
            _ => DataCredentialResult.Unavailable(token.Message),
        };
    }
}
