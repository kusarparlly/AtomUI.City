using System.Globalization;
using System.Runtime.CompilerServices;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Routing;

namespace AtomUI.City.Presentation;

/// <summary>
/// Provides the default Routing-Presentation technical coordinator.
/// </summary>
public sealed class RoutePresentationCoordinator : IRoutePresentationCoordinator
{
    private readonly ConditionalWeakTable<IRouteOutlet, SemaphoreSlim> _outletGates = new();
    private readonly IHostDiagnostics? _diagnostics;
    private long _operationSequence;

    /// <summary>
    /// Initializes a new coordinator without diagnostics.
    /// </summary>
    public RoutePresentationCoordinator()
    {
    }

    /// <summary>
    /// Initializes a new coordinator with diagnostics.
    /// </summary>
    public RoutePresentationCoordinator(IHostDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Executes one serialized Routing-Presentation operation for an outlet.
    /// </summary>
    public async ValueTask<RoutePresentationResult> NavigateAsync(
        IRouter router,
        IRouteOutlet outlet,
        RoutePresentationNavigation navigation,
        IRoutePresentationPlanFactory planFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(outlet);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(planFactory);

        var operationId = Interlocked.Increment(ref _operationSequence);
        var outletGate = _outletGates.GetValue(outlet, static _ => new SemaphoreSlim(1, 1));
        try
        {
            await outletGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(
                operationId,
                outlet,
                new RoutePresentationResult(
                    RoutePresentationResultStatus.Cancelled,
                    navigation: null,
                    presentation: null,
                    restore: null,
                    "Coordination was cancelled before Router commit.",
                    exception: null));
        }

        try
        {
            WriteDiagnostic(
                PresentationDiagnosticIds.RoutePresentationCoordinationStarted,
                HostDiagnosticSeverity.Info,
                operationId,
                outlet,
                result: null);

            NavigationResult routingResult;
            try
            {
                routingResult = await navigation(cancellationToken).ConfigureAwait(false) ??
                    throw new InvalidOperationException("The navigation delegate returned a null result.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Complete(
                    operationId,
                    outlet,
                    new RoutePresentationResult(
                        RoutePresentationResultStatus.Cancelled,
                        navigation: null,
                        presentation: null,
                        restore: null,
                        "Coordination was cancelled before Router commit.",
                        exception: null));
            }
            catch (Exception exception)
            {
                return Complete(
                    operationId,
                    outlet,
                    new RoutePresentationResult(
                        RoutePresentationResultStatus.StructuralFailure,
                        navigation: null,
                        presentation: null,
                        restore: null,
                        exception.Message,
                        exception));
            }

            if (!IsCommitted(routingResult))
            {
                return Complete(
                    operationId,
                    outlet,
                    new RoutePresentationResult(
                        RoutePresentationResultStatus.RoutingNotCommitted,
                        routingResult,
                        presentation: null,
                        restore: null,
                        routingResult.Error?.Message,
                        routingResult.Error?.Exception));
            }

            var receipt = routingResult.CommitReceipt;
            if (receipt is null)
            {
                return Complete(
                    operationId,
                    outlet,
                    new RoutePresentationResult(
                        RoutePresentationResultStatus.StructuralFailure,
                        routingResult,
                        presentation: null,
                        restore: null,
                        "Router committed without returning a NavigationCommitReceipt.",
                        exception: null));
            }

            RouteOutletCommitResult? presentationResult = null;
            Exception? presentationException = null;
            try
            {
                var plan = await planFactory.CreateAsync(
                    new RoutePresentationPlanContext(routingResult, receipt, outlet.Name),
                    cancellationToken).ConfigureAwait(false) ??
                    throw new InvalidOperationException("The route presentation plan factory returned null.");
                presentationResult = await outlet.CommitAsync(plan, cancellationToken).ConfigureAwait(false);
                if (presentationResult.Succeeded)
                {
                    return Complete(
                        operationId,
                        outlet,
                        new RoutePresentationResult(
                            RoutePresentationResultStatus.Succeeded,
                            routingResult,
                            presentationResult,
                            restore: null,
                            message: null,
                            exception: null));
                }
            }
            catch (Exception exception)
            {
                presentationException = exception;
            }

            return Complete(
                operationId,
                outlet,
                await RecoverAsync(
                    router,
                    receipt,
                    routingResult,
                    presentationResult,
                    presentationException).ConfigureAwait(false));
        }
        finally
        {
            outletGate.Release();
        }
    }

    private static bool IsCommitted(NavigationResult result) =>
        result.Status is NavigationResultStatus.Success or NavigationResultStatus.Redirected &&
        result.ActiveRoute is not null;

    private static async ValueTask<RoutePresentationResult> RecoverAsync(
        IRouter router,
        NavigationCommitReceipt receipt,
        NavigationResult navigation,
        RouteOutletCommitResult? presentation,
        Exception? exception)
    {
        NavigationRestoreResult restore;
        try
        {
            restore = await router.TryRestoreAsync(receipt, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception restoreException)
        {
            var failures = exception is null
                ? new[] { restoreException }
                : new[] { exception, restoreException };
            return new RoutePresentationResult(
                RoutePresentationResultStatus.StructuralFailure,
                navigation,
                presentation,
                restore: null,
                restoreException.Message,
                new AggregateException(
                    "Presentation failed and conditional Router restore threw.",
                    failures));
        }

        var status = restore.Status switch
        {
            NavigationRestoreResultStatus.Restored =>
                RoutePresentationResultStatus.PresentationFailedRouteRestored,
            NavigationRestoreResultStatus.Rejected when string.Equals(
                restore.Code,
                "CITY-NAVIGATION-RESTORE-SUPERSEDED",
                StringComparison.Ordinal) =>
                RoutePresentationResultStatus.RestoreRejectedByNewerNavigation,
            NavigationRestoreResultStatus.Rejected =>
                RoutePresentationResultStatus.PresentationFailedRouteCommitted,
            _ => RoutePresentationResultStatus.StructuralFailure,
        };

        return new RoutePresentationResult(
            status,
            navigation,
            presentation,
            restore,
            presentation?.Message ?? exception?.Message ?? restore.Message,
            exception ?? restore.Exception);
    }

    private RoutePresentationResult Complete(
        long operationId,
        IRouteOutlet outlet,
        RoutePresentationResult result)
    {
        var diagnosticId = result.Status switch
        {
            RoutePresentationResultStatus.Succeeded =>
                PresentationDiagnosticIds.RoutePresentationCoordinationSucceeded,
            RoutePresentationResultStatus.RestoreRejectedByNewerNavigation =>
                PresentationDiagnosticIds.RoutePresentationRestoreRejected,
            _ => PresentationDiagnosticIds.RoutePresentationCoordinationFailed,
        };
        var severity = result.Status switch
        {
            RoutePresentationResultStatus.Succeeded => HostDiagnosticSeverity.Info,
            RoutePresentationResultStatus.RoutingNotCommitted or
                RoutePresentationResultStatus.Cancelled => HostDiagnosticSeverity.Warning,
            _ => HostDiagnosticSeverity.Error,
        };
        WriteDiagnostic(diagnosticId, severity, operationId, outlet, result);
        return result;
    }

    private void WriteDiagnostic(
        string diagnosticId,
        HostDiagnosticSeverity severity,
        long operationId,
        IRouteOutlet outlet,
        RoutePresentationResult? result)
    {
        try
        {
            var message = result is null
                ? $"Routing-Presentation coordination '{operationId}' started."
                : result.Message ?? $"Routing-Presentation coordination '{operationId}' completed with status '{result.Status}'.";
            _diagnostics?.Write(new HostDiagnosticRecord(
                diagnosticId,
                message,
                severity)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["operationId"] = operationId.ToString(CultureInfo.InvariantCulture),
                    ["outletName"] = outlet.Name,
                    ["status"] = result?.Status.ToString(),
                    ["navigationId"] = result?.Navigation?.NavigationId.ToString("D"),
                    ["generation"] = result?.Navigation?.CommitReceipt?.Generation.ToString(
                        CultureInfo.InvariantCulture),
                    ["presentationOperationId"] = result?.Presentation?.OperationId.ToString(
                        CultureInfo.InvariantCulture),
                    ["restoreStatus"] = result?.Restore?.Status.ToString(),
                    ["error"] = result?.Exception?.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics cannot change coordination state or its structured result.
        }
    }
}
