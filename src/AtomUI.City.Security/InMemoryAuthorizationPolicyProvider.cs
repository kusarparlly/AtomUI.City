using System.Diagnostics.CodeAnalysis;

namespace AtomUI.City.Security;

/// <summary>
/// Represents in memory authorization policy provider.
/// </summary>
public sealed class InMemoryAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly Dictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revokedContributions = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private long _revision;

    /// <summary>
    /// Represents the revision value.
    /// </summary>
    public long Revision
    {
        get
        {
            lock (_syncRoot)
            {
                return _revision;
            }
        }
    }

    /// <summary>
    /// Represents the policies value.
    /// </summary>
    public IReadOnlyCollection<AuthorizationPolicy> Policies
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_policies.Values.ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public bool Add(AuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(policy.ContributionId)
                && _revokedContributions.Contains(policy.ContributionId))
            {
                return false;
            }

            if (_policies.ContainsKey(policy.Name))
            {
                return false;
            }

            _policies.Add(policy.Name, policy);
            _revision++;

            return true;
        }
    }

    /// <summary>
    /// Executes the remove operation.
    /// </summary>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            if (!_policies.Remove(name))
            {
                return false;
            }

            _revision++;

            return true;
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
            var names = _policies
                .Where(pair => string.Equals(pair.Value.ContributionId, contributionId, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var name in names)
            {
                _policies.Remove(name);
            }

            if (names.Length > 0)
            {
                _revision++;
            }

            return names.Length;
        }
    }

    /// <summary>
    /// Executes the contains operation.
    /// </summary>
    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            return _policies.ContainsKey(name);
        }
    }

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    public bool TryGet(
        string name,
        [NotNullWhen(true)] out AuthorizationPolicy? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            return _policies.TryGetValue(name, out policy);
        }
    }

    /// <summary>
    /// Executes the get policy async operation.
    /// </summary>
    public ValueTask<AuthorizationPolicy?> GetPolicyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _policies.TryGetValue(name, out var policy);

            return ValueTask.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
