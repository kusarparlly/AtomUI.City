using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

/// <summary>
/// Represents delegate access token provider.
/// </summary>
public sealed class DelegateAccessTokenProvider : IAccessTokenProvider
{
    private readonly Func<AccessTokenRequest, CancellationToken, ValueTask<AccessTokenResult>> _provider;
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>DelegateAccessTokenProvider</c> type.
    /// </summary>
    public DelegateAccessTokenProvider(
        Func<AccessTokenRequest, CancellationToken, ValueTask<AccessTokenResult>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
    }

    /// <summary>
    /// Initializes a new instance of the <c>DelegateAccessTokenProvider</c> type.
    /// </summary>
    public DelegateAccessTokenProvider(
        Func<AccessTokenRequest, CancellationToken, ValueTask<AccessTokenResult>> provider,
        IHostDiagnostics diagnostics)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Executes the get token async operation.
    /// </summary>
    public async ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Complete(request, AccessTokenResult.Cancelled());
        }

        try
        {
            var result = await _provider(request, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
            {
                throw new InvalidOperationException("The access token provider returned null.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(request, AccessTokenResult.Cancelled());
            }

            return Complete(request, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(request, AccessTokenResult.Cancelled());
        }
        catch (Exception exception)
        {
            SecurityDiagnostics.Write(
                _diagnostics,
                SecurityDiagnosticIds.AccessTokenProviderFailed,
                "Access token provider failed.",
                HostDiagnosticSeverity.Error,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["resourceName"] = request.ResourceName,
                    ["scheme"] = request.Scheme,
                    ["operationName"] = request.OperationName,
                    ["status"] = AccessTokenResultStatus.Failed.ToString(),
                    ["exceptionType"] = exception.GetType().FullName,
                });
            return AccessTokenResult.Failed(exception.Message, exception);
        }
    }

    private AccessTokenResult Complete(AccessTokenRequest request, AccessTokenResult result)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.AccessTokenResolved,
            "Access token request completed.",
            result.Status == AccessTokenResultStatus.Failed
                ? HostDiagnosticSeverity.Error
                : HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["resourceName"] = request.ResourceName,
                ["scheme"] = result.Scheme ?? request.Scheme,
                ["operationName"] = request.OperationName,
                ["status"] = result.Status.ToString(),
                ["expiresAt"] = result.ExpiresAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            });
        return result;
    }
}
