namespace AtomUI.City.Routing;

using AtomUI.City.Core.Diagnostics;

/// <summary>
/// Represents route registry.
/// </summary>
public sealed class RouteRegistry : IRouteRegistry, IRouteContributionServiceResolver
{
    private readonly object _syncRoot = new();
    private readonly IHostDiagnostics? _diagnostics;
    private readonly Dictionary<string, Func<Type, object?>> _serviceResolvers = new(StringComparer.Ordinal);
    private RouteGraphSnapshot _currentSnapshot;

    /// <summary>
    /// Initializes a new instance of the <c>RouteRegistry</c> type.
    /// </summary>
    public RouteRegistry()
        : this(RouteGraphSnapshot.Create([]), diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>RouteRegistry</c> type.
    /// </summary>
    public RouteRegistry(
        RouteGraphSnapshot initialSnapshot,
        IHostDiagnostics? diagnostics = null)
    {
        _currentSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets current snapshot.
    /// </summary>
    public RouteGraphSnapshot CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

    /// <summary>
    /// Executes the add contribution operation.
    /// </summary>
    public RouteContributionLease AddContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes) =>
        AddContribution(new RouteContribution(contributionId, routes));

    /// <summary>
    /// Executes the add contribution operation.
    /// </summary>
    public RouteContributionLease AddContribution(RouteContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        var contributionId = contribution.ContributionId;
        var routes = contribution.Routes;
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(routes);

        RouteGraphSnapshot next;
        try
        {
            lock (_syncRoot)
            {
                if (_currentSnapshot.GetContributionRoutes(contributionId).Count > 0)
                {
                    throw new RouteGraphException(
                        RouteGraphError.InvalidContribution,
                        $"Route contribution '{contributionId}' is already active.");
                }

                next = _currentSnapshot.WithContribution(contributionId, routes);
                if (contribution.ServiceResolver is not null)
                {
                    _serviceResolvers.Add(contributionId, contribution.ServiceResolver);
                }
                Volatile.Write(ref _currentSnapshot, next);
            }
        }
        catch (RouteGraphException exception)
        {
            WriteGraphRejected(contributionId, "add", exception);
            throw;
        }

        WriteGraphChanged(contributionId, "added", next);

        return new RouteContributionLease(contributionId, ReleaseContribution);
    }

    /// <summary>
    /// Executes the remove contribution operation.
    /// </summary>
    public bool RemoveContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        RouteGraphSnapshot? next = null;
        try
        {
            lock (_syncRoot)
            {
                if (_currentSnapshot.GetContributionRoutes(contributionId).Count == 0)
                {
                    return false;
                }

                next = _currentSnapshot.WithoutContribution(contributionId);
                _serviceResolvers.Remove(contributionId);
                Volatile.Write(ref _currentSnapshot, next);
            }
        }
        catch (RouteGraphException exception)
        {
            WriteGraphRejected(contributionId, "remove", exception);
            throw;
        }


        WriteGraphChanged(contributionId, "removed", next);
        return true;
    }

    private void ReleaseContribution(string contributionId)
    {
        RemoveContribution(contributionId);
    }

    Func<Type, object?>? IRouteContributionServiceResolver.GetServiceResolver(RouteDescriptor route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.ContributionId is null)
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _serviceResolvers.TryGetValue(route.ContributionId, out var resolver)
                ? resolver
                : null;
        }
    }

    private void WriteGraphChanged(
        string contributionId,
        string operation,
        RouteGraphSnapshot snapshot)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            _diagnostics.Write(new HostDiagnosticRecord(
                RoutingDiagnosticIds.RouteGraphChanged,
                $"Route contribution '{contributionId}' was {operation}.",
                HostDiagnosticSeverity.Info)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["contributionId"] = contributionId,
                    ["operation"] = operation,
                    ["graphVersion"] = snapshot.Version.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["routeCount"] = snapshot.Routes.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                },
            });
        }
        catch
        {
            // Diagnostics are observational and must never roll back a published graph.
        }
    }

    private void WriteGraphRejected(
        string contributionId,
        string operation,
        RouteGraphException exception)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            _diagnostics.Write(new HostDiagnosticRecord(
                RoutingDiagnosticIds.RouteGraphRejected,
                exception.Message,
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["contributionId"] = contributionId,
                    ["operation"] = operation,
                    ["graphVersion"] = CurrentSnapshot.Version.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["graphError"] = exception.Error.ToString(),
                },
            });
        }
        catch
        {
            // Diagnostics are observational and cannot replace the graph failure.
        }
    }
}
