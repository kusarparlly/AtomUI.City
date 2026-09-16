namespace AtomUI.City.State;

/// <summary>
/// Defines the supported state access policy values.
/// </summary>
public enum StateAccessPolicy
{
    /// <summary>
    /// Represents the read only value.
    /// </summary>
    ReadOnly,
    /// <summary>
    /// Represents the owner write value.
    /// </summary>
    OwnerWrite,
    /// <summary>
    /// Represents the host write value.
    /// </summary>
    HostWrite,
    /// <summary>
    /// Represents the authorized write value.
    /// </summary>
    AuthorizedWrite,
    /// <summary>
    /// Represents the plugin isolated value.
    /// </summary>
    PluginIsolated,
}
