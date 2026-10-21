namespace AtomUI.City.Presentation;

/// <summary>
/// Defines coordinated Routing-Presentation outcomes.
/// </summary>
public enum RoutePresentationResultStatus
{
    /// <summary>
    /// Router and Presentation both committed.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Router did not commit, so no Presentation work was admitted.
    /// </summary>
    RoutingNotCommitted,

    /// <summary>
    /// Presentation failed and Router safely restored the preceding commit checkpoint.
    /// </summary>
    PresentationFailedRouteRestored,

    /// <summary>
    /// Presentation failed and the committed route was not restored.
    /// </summary>
    PresentationFailedRouteCommitted,

    /// <summary>
    /// A newer Router commit superseded the receipt, so the coordinator did not undo it.
    /// </summary>
    RestoreRejectedByNewerNavigation,

    /// <summary>
    /// The coordinator could not establish or preserve a trustworthy technical relationship.
    /// </summary>
    StructuralFailure,

    /// <summary>
    /// Coordination was cancelled before Router commit.
    /// </summary>
    Cancelled,
}
