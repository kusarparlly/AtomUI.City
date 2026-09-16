namespace AtomUI.City.PluginSystem;

/// <summary>
/// Defines the supported plugin runtime lease state values.
/// </summary>
public enum PluginRuntimeLeaseState
{
    /// <summary>
    /// Represents the active value.
    /// </summary>
    Active,
    /// <summary>
    /// Represents the revoking value.
    /// </summary>
    Revoking,
    /// <summary>
    /// Represents the revoked value.
    /// </summary>
    Revoked,
    /// <summary>
    /// Represents the revoke failed value.
    /// </summary>
    RevokeFailed,
}
