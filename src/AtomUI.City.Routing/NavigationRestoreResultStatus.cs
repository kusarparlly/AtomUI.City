namespace AtomUI.City.Routing;

/// <summary>
/// Defines conditional navigation restore outcomes.
/// </summary>
public enum NavigationRestoreResultStatus
{
    /// <summary>
    /// The receipt was current and the previous snapshot and journal checkpoint were restored.
    /// </summary>
    Restored,

    /// <summary>
    /// The receipt was not eligible for restoration.
    /// </summary>
    Rejected,

    /// <summary>
    /// Waiting for restoration was cancelled before mutation.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Restoration failed unexpectedly.
    /// </summary>
    Failed,
}
