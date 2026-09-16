using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents active plugin view registry.
/// </summary>
public sealed class ActivePluginViewRegistry : IActivePluginViewRegistry
{
    private readonly object _gate = new();
    private readonly List<ActivePluginViewLease> _leases = [];
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>ActivePluginViewRegistry</c> type.
    /// </summary>
    public ActivePluginViewRegistry()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>ActivePluginViewRegistry</c> type.
    /// </summary>
    public ActivePluginViewRegistry(IHostDiagnostics? diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Represents the active views value.
    /// </summary>
    public IReadOnlyList<ActivePluginView> ActiveViews
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(
                    _leases
                        .Where(static lease => !lease.IsDisposed)
                        .Select(static lease => lease.View)
                        .ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the track operation.
    /// </summary>
    public IActivePluginViewLease Track(ActivePluginView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var lease = new ActivePluginViewLease(this, view);

        lock (_gate)
        {
            _leases.Add(lease);
        }

        WriteTrackedDiagnostic(view);

        return lease;
    }

    /// <summary>
    /// Executes the close plugin views async operation.
    /// </summary>
    public ValueTask<int> ClosePluginViewsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return CloseAsync(
            view => string.Equals(view.PluginId, pluginId, StringComparison.Ordinal),
            cancellationToken);
    }

    /// <summary>
    /// Executes the close contribution views async operation.
    /// </summary>
    public ValueTask<int> CloseContributionViewsAsync(
        string contributionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        return CloseAsync(
            view => string.Equals(view.ContributionId, contributionId, StringComparison.Ordinal),
            cancellationToken);
    }

    private async ValueTask<int> CloseAsync(
        Func<ActivePluginView, bool> predicate,
        CancellationToken cancellationToken)
    {
        ActivePluginViewLease[] leases;

        lock (_gate)
        {
            leases = _leases
                .Where(lease => !lease.IsDisposed && predicate(lease.View))
                .ToArray();
        }

        var closed = 0;

        foreach (var lease in leases)
        {
            if (await CloseAsync(lease, cancellationToken).ConfigureAwait(false))
            {
                closed++;
            }
        }

        return closed;
    }

    private async ValueTask<bool> CloseAsync(
        ActivePluginViewLease lease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var view = lease.View;
        if (!ReferenceEquals(view.Outlet.CurrentContent, view.Handle.View))
        {
            Untrack(lease);

            return false;
        }

        try
        {
            var result = await view.Outlet
                .CommitAsync(RouteOutletCommitPlan.Clear(view.Outlet.Name), cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                WriteCloseFailedDiagnostic(
                    view,
                    result.Message ?? result.Error?.ToString() ?? "Unknown outlet commit failure.");

                return false;
            }

            Untrack(lease);
            WriteClosedDiagnostic(view);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteCloseFailedDiagnostic(view, exception.Message);

            return false;
        }
    }

    private void Untrack(ActivePluginViewLease lease)
    {
        if (!lease.MarkDisposed())
        {
            return;
        }

        lock (_gate)
        {
            _leases.Remove(lease);
        }
    }

    private void WriteTrackedDiagnostic(ActivePluginView view)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.PluginViewTracked,
            $"Presentation plugin view tracked plugin '{view.PluginId}' contribution '{Normalize(view.ContributionId)}' outlet '{view.Outlet.Name}' view '{view.Handle.View.GetType().FullName}' view model '{view.Handle.ViewModel.GetType().FullName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(view, error: null),
        });
    }

    private void WriteClosedDiagnostic(ActivePluginView view)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.PluginViewClosed,
            $"Presentation plugin view closed plugin '{view.PluginId}' contribution '{Normalize(view.ContributionId)}' outlet '{view.Outlet.Name}' view '{view.Handle.View.GetType().FullName}' view model '{view.Handle.ViewModel.GetType().FullName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(view, error: null),
        });
    }

    private void WriteCloseFailedDiagnostic(
        ActivePluginView view,
        string message)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.PluginViewCloseFailed,
            $"Presentation plugin view failed to close plugin '{view.PluginId}' contribution '{Normalize(view.ContributionId)}' outlet '{view.Outlet.Name}' view '{view.Handle.View.GetType().FullName}' view model '{view.Handle.ViewModel.GetType().FullName}': {message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(view, message),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        ActivePluginView view,
        string? error)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["pluginId"] = view.PluginId,
            ["contributionId"] = view.ContributionId,
            ["outletName"] = view.Outlet.Name,
            ["viewType"] = view.Handle.View.GetType().FullName,
            ["viewModelType"] = view.Handle.ViewModel.GetType().FullName,
            ["error"] = error,
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    }

    private sealed class ActivePluginViewLease : IActivePluginViewLease
    {
        private readonly ActivePluginViewRegistry _registry;
        private int _isDisposed;

        public ActivePluginViewLease(
            ActivePluginViewRegistry registry,
            ActivePluginView view)
        {
            _registry = registry;
            View = view;
        }

        public ActivePluginView View { get; }

        public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

        public void Dispose()
        {
            _registry.Untrack(this);
        }

        public bool MarkDisposed()
        {
            return Interlocked.Exchange(ref _isDisposed, 1) == 0;
        }
    }
}
