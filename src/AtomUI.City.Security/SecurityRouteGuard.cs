using AtomUI.City.Routing;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

/// <summary>
/// Represents security route guard.
/// </summary>
public sealed class SecurityRouteGuard : IRouteEnterGuard
{
    private readonly IAuthorizationEvaluator _authorizationEvaluator;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly IRouteAuthorizationPolicyProvider _policyProvider;
    private readonly SecurityRouteGuardOptions _options;
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>SecurityRouteGuard</c> type.
    /// </summary>
    public SecurityRouteGuard(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        IRouteAuthorizationPolicyProvider policyProvider)
        : this(
            authorizationEvaluator,
            principalAccessor,
            policyProvider,
            new SecurityRouteGuardOptions(),
            diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>SecurityRouteGuard</c> type.
    /// </summary>
    public SecurityRouteGuard(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        IRouteAuthorizationPolicyProvider policyProvider,
        SecurityRouteGuardOptions options)
        : this(
            authorizationEvaluator,
            principalAccessor,
            policyProvider,
            options,
            diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>SecurityRouteGuard</c> type.
    /// </summary>
    public SecurityRouteGuard(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        IRouteAuthorizationPolicyProvider policyProvider,
        SecurityRouteGuardOptions options,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(authorizationEvaluator);
        ArgumentNullException.ThrowIfNull(principalAccessor);
        ArgumentNullException.ThrowIfNull(policyProvider);
        ArgumentNullException.ThrowIfNull(options);

        if (options.LoginRouteId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.LoginRouteId);
        }

        ArgumentNullException.ThrowIfNull(options.LoginNavigationOptions);

        _authorizationEvaluator = authorizationEvaluator;
        _principalAccessor = principalAccessor;
        _policyProvider = policyProvider;
        _options = options;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the can enter async operation.
    /// </summary>
    public async ValueTask<RouteGuardResult> CanEnterAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
        {
            return Complete(
                context,
                policy: null,
                RouteGuardResult.Cancel("Route authorization was cancelled."));
        }

        AuthorizationResult authorization;
        AuthorizationPolicy? policy = null;

        try
        {
            policy = await _policyProvider.GetPolicyAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(
                    context,
                    policy,
                    RouteGuardResult.Cancel("Route authorization was cancelled."));
            }

            if (policy is null)
            {
                return Complete(context, policy, RouteGuardResult.Allow());
            }

            authorization = await _authorizationEvaluator.EvaluateAsync(
                    new AuthorizationRequest(
                        _principalAccessor.Principal,
                        policy,
                        resourceName: context.Route.RouteId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(
                    context,
                    policy,
                    RouteGuardResult.Cancel("Route authorization was cancelled."));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(
                context,
                policy,
                RouteGuardResult.Cancel("Route authorization was cancelled."));
        }
        catch (Exception exception)
        {
            return Complete(
                context,
                policy,
                RouteGuardResult.Failed(
                    SecurityRouteGuardResultCodes.AuthorizationFailed,
                    exception.Message,
                    exception));
        }

        if (authorization is null)
        {
            return Complete(
                context,
                policy,
                RouteGuardResult.Failed(
                    SecurityRouteGuardResultCodes.AuthorizationFailed,
                    "The authorization evaluator returned null."));
        }

        var result = authorization.Status switch
        {
            AuthorizationResultStatus.Allowed => RouteGuardResult.Allow(),
            AuthorizationResultStatus.Challenge => CreateChallengeResult(authorization),
            AuthorizationResultStatus.Forbidden or AuthorizationResultStatus.Denied => RouteGuardResult.Reject(
                SecurityRouteGuardResultCodes.Forbidden,
                authorization.Message),
            AuthorizationResultStatus.Cancelled => RouteGuardResult.Cancel(authorization.Message),
            AuthorizationResultStatus.Failed => RouteGuardResult.Failed(
                SecurityRouteGuardResultCodes.AuthorizationFailed,
                authorization.Message ?? "Route authorization failed.",
                authorization.Exception),
            _ => RouteGuardResult.Failed(
                SecurityRouteGuardResultCodes.AuthorizationFailed,
                "Route authorization returned an unsupported result."),
        };

        return Complete(context, policy, result);
    }

    private RouteGuardResult CreateChallengeResult(AuthorizationResult authorization)
    {
        if (!string.IsNullOrWhiteSpace(_options.LoginRouteId))
        {
            return RouteGuardResult.Redirect(
                NavigationTarget.FromRouteReference(
                    _options.LoginRouteId,
                    parameters: null,
                    _options.LoginNavigationOptions));
        }

        return RouteGuardResult.Reject(
            SecurityRouteGuardResultCodes.AuthenticationRequired,
            authorization.Message);
    }

    private RouteGuardResult Complete(
        RouteGuardContext context,
        AuthorizationPolicy? policy,
        RouteGuardResult result)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            result.Status == RouteGuardResultStatus.Failed
                ? SecurityDiagnosticIds.RouteAuthorizationFailed
                : SecurityDiagnosticIds.RouteAuthorizationCompleted,
            "Route authorization completed.",
            result.Status switch
            {
                RouteGuardResultStatus.Failed => HostDiagnosticSeverity.Error,
                RouteGuardResultStatus.Reject => HostDiagnosticSeverity.Warning,
                _ => HostDiagnosticSeverity.Info,
            },
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["routeId"] = context.Route.RouteId,
                ["policyName"] = policy?.Name,
                ["resultStatus"] = result.Status.ToString(),
                ["resultCode"] = result.Code,
                ["exceptionType"] = result.Exception?.GetType().FullName,
            });
        return result;
    }
}
