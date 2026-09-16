using AtomUI.City.Routing;

namespace AtomUI.City.Security;

/// <summary>
/// Represents in memory route authorization policy provider.
/// </summary>
public sealed class InMemoryRouteAuthorizationPolicyProvider : IRouteAuthorizationPolicyProvider
{
    private readonly Dictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revokedContributions = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public bool Add(string routeId, AuthorizationPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentNullException.ThrowIfNull(policy);

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(policy.ContributionId)
                && _revokedContributions.Contains(policy.ContributionId))
            {
                return false;
            }

            if (_policies.ContainsKey(routeId))
            {
                return false;
            }

            _policies.Add(routeId, policy);

            return true;
        }
    }

    /// <summary>
    /// Executes the remove operation.
    /// </summary>
    public bool Remove(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);

        lock (_syncRoot)
        {
            return _policies.Remove(routeId);
        }
    }

    /// <summary>
    /// Executes the remove by contribution operation.
    /// </summary>
    public int RemoveByContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        lock (_syncRoot)
        {
            _revokedContributions.Add(contributionId);
            var routeIds = _policies
                .Where(pair => string.Equals(
                    pair.Value.ContributionId,
                    contributionId,
                    StringComparison.Ordinal))
                .Select(static pair => pair.Key)
                .ToArray();

            foreach (var routeId in routeIds)
            {
                _policies.Remove(routeId);
            }

            return routeIds.Length;
        }
    }

    /// <summary>
    /// Executes the get policy async operation.
    /// </summary>
    public ValueTask<AuthorizationPolicy?> GetPolicyAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _policies.TryGetValue(context.Route.RouteId, out var policy);

            return ValueTask.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
