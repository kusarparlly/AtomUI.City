namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iview registry.
/// </summary>
public interface IViewRegistry : IViewLocator
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(ViewDescriptor descriptor);

    /// <summary>
    /// Executes the register manifest operation.
    /// </summary>
    void RegisterManifest(IEnumerable<ViewDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        foreach (var descriptor in descriptors)
        {
            Register(descriptor);
        }
    }

    /// <summary>
    /// Executes the revoke plugin operation.
    /// </summary>
    int RevokePlugin(string pluginId);

    /// <summary>
    /// Executes the revoke contribution operation.
    /// </summary>
    int RevokeContribution(string contributionId);
}
