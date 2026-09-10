using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public sealed class PresentationResourceDictionaryRevoker : IPresentationResourceDictionaryRevoker
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IReadOnlyList<IPresentationResourceDictionaryTarget> _targets;
    private readonly IHostDiagnostics? _diagnostics;

    public PresentationResourceDictionaryRevoker(
        IUiDispatcher dispatcher,
        IEnumerable<IPresentationResourceDictionaryTarget> targets)
        : this(dispatcher, targets, diagnostics: null)
    {
    }

    public PresentationResourceDictionaryRevoker(
        IUiDispatcher dispatcher,
        IEnumerable<IPresentationResourceDictionaryTarget> targets,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(targets);

        _dispatcher = dispatcher;
        _targets = targets.ToArray();
        _diagnostics = diagnostics;
    }

    public async ValueTask<PresentationResourceDictionaryRevokeResult> RevokeAsync(
        PresentationResourceDictionaryRevocation revocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revocation);

        var failures = new List<Exception>();
        try
        {
            await _dispatcher.PostAsync(
                async dispatcherCancellationToken =>
                {
                    foreach (var target in _targets)
                    {
                        dispatcherCancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            await target
                                .RevokeResourcesAsync(revocation, dispatcherCancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                            when (dispatcherCancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        var result = new PresentationResourceDictionaryRevokeResult(failures);
        WriteDiagnostic(revocation, result);
        return result;
    }

    private void WriteDiagnostic(
        PresentationResourceDictionaryRevocation revocation,
        PresentationResourceDictionaryRevokeResult result)
    {
        var firstError = result.Errors.FirstOrDefault();
        _diagnostics?.Write(new HostDiagnosticRecord(
            result.Succeeded
                ? PresentationDiagnosticIds.ResourceDictionaryRevoked
                : PresentationDiagnosticIds.ResourceDictionaryRevokeFailed,
            result.Succeeded
                ? $"Presentation resource dictionaries were revoked for plugin '{revocation.PluginId}'."
                : $"Presentation resource dictionary revoke failed for plugin '{revocation.PluginId}': {firstError?.Message}",
            result.Succeeded ? HostDiagnosticSeverity.Info : HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["pluginId"] = revocation.PluginId,
                ["contributionId"] = revocation.ContributionId,
                ["targetCount"] = _targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["failureCount"] = result.Errors.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["error"] = firstError?.GetType().FullName,
            },
        });
    }
}
