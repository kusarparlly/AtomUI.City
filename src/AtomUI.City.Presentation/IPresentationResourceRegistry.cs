namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation resource registry.
/// </summary>
public interface IPresentationResourceRegistry
{
    /// <summary>
    /// Gets contributions.
    /// </summary>
    IReadOnlyList<PresentationResourceContribution> Contributions { get; }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    IPresentationResourceLease Register(PresentationResourceContribution contribution);

    /// <summary>
    /// Executes the revoke plugin operation.
    /// </summary>
    int RevokePlugin(string pluginId);

    /// <summary>
    /// Executes the revoke contribution operation.
    /// </summary>
    int RevokeContribution(string contributionId);
}
