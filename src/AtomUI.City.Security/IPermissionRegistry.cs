using System.Diagnostics.CodeAnalysis;

namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for ipermission registry.
/// </summary>
public interface IPermissionRegistry
{
    /// <summary>
    /// Occurs when changed.
    /// </summary>
    event EventHandler<PermissionRegistryChangedEventArgs>? Changed;

    /// <summary>
    /// Gets revision.
    /// </summary>
    long Revision { get; }

    /// <summary>
    /// Gets permissions.
    /// </summary>
    IReadOnlyCollection<PermissionDescriptor> Permissions { get; }

    /// <summary>
    /// Executes the contains operation.
    /// </summary>
    bool Contains(string name);

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    bool TryGet(
        string name,
        [NotNullWhen(true)] out PermissionDescriptor? descriptor);
}
