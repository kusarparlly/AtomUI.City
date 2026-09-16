namespace AtomUI.City.PluginSystem;

/// <summary>
/// Defines the supported plugin runtime state values.
/// </summary>
public enum PluginRuntimeState
{
    /// <summary>
    /// Represents the verified value.
    /// </summary>
    Verified,
    /// <summary>
    /// Represents the loaded value.
    /// </summary>
    Loaded,
    /// <summary>
    /// Represents the active value.
    /// </summary>
    Active,
    /// <summary>
    /// Represents the deactivating value.
    /// </summary>
    Deactivating,
    /// <summary>
    /// Represents the inactive value.
    /// </summary>
    Inactive,
    /// <summary>
    /// Represents the unloading value.
    /// </summary>
    Unloading,
    /// <summary>
    /// Represents the unloaded value.
    /// </summary>
    Unloaded,
    /// <summary>
    /// Represents the faulted value.
    /// </summary>
    Faulted,
    /// <summary>
    /// Represents the unload pending value.
    /// </summary>
    UnloadPending,
}
