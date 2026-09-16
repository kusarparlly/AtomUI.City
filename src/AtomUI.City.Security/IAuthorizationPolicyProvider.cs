using System.Diagnostics.CodeAnalysis;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iauthorization policy provider.
/// </summary>
public interface IAuthorizationPolicyProvider
{
    /// <summary>
    /// Gets revision.
    /// </summary>
    long Revision { get; }

    /// <summary>
    /// Gets policies.
    /// </summary>
    IReadOnlyCollection<AuthorizationPolicy> Policies { get; }

    /// <summary>
    /// Executes the contains operation.
    /// </summary>
    bool Contains(string name);

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    bool TryGet(
        string name,
        [NotNullWhen(true)] out AuthorizationPolicy? policy);

    /// <summary>
    /// Executes the get policy async operation.
    /// </summary>
    ValueTask<AuthorizationPolicy?> GetPolicyAsync(
        string name,
        CancellationToken cancellationToken = default);
}
