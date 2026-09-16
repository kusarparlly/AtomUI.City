namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported presentation error values.
/// </summary>
public enum PresentationError
{
    /// <summary>
    /// Represents the duplicate view value.
    /// </summary>
    DuplicateView,
    /// <summary>
    /// Represents the duplicate window value.
    /// </summary>
    DuplicateWindow,
    /// <summary>
    /// Represents the duplicate outlet value.
    /// </summary>
    DuplicateOutlet,
    /// <summary>
    /// Represents the view not found value.
    /// </summary>
    ViewNotFound,
    /// <summary>
    /// Represents the view model not found value.
    /// </summary>
    ViewModelNotFound,
    /// <summary>
    /// Represents the view model creation failed value.
    /// </summary>
    ViewModelCreationFailed,
    /// <summary>
    /// Represents the view creation failed value.
    /// </summary>
    ViewCreationFailed,
    /// <summary>
    /// Represents the binding failed value.
    /// </summary>
    BindingFailed,
    /// <summary>
    /// Represents the outlet not found value.
    /// </summary>
    OutletNotFound,
    /// <summary>
    /// Represents the outlet commit failed value.
    /// </summary>
    OutletCommitFailed,
    /// <summary>
    /// Represents the runtime not ready value.
    /// </summary>
    RuntimeNotReady,
    /// <summary>
    /// Represents the runtime stopping value.
    /// </summary>
    RuntimeStopping,
    /// <summary>
    /// Represents the dispatcher unavailable value.
    /// </summary>
    DispatcherUnavailable,
    /// <summary>
    /// Represents the window not found value.
    /// </summary>
    WindowNotFound,
    /// <summary>
    /// Represents the window close rejected value.
    /// </summary>
    WindowCloseRejected,
    /// <summary>
    /// Represents the presentation out of sync value.
    /// </summary>
    PresentationOutOfSync,
    /// <summary>
    /// Represents the outlet queue full value.
    /// </summary>
    OutletQueueFull,
    /// <summary>
    /// Represents the interaction queue full value.
    /// </summary>
    InteractionQueueFull,
    /// <summary>
    /// Represents the candidate ownership violation value.
    /// </summary>
    CandidateOwnershipViolation,
    /// <summary>
    /// Represents the multiple close confirmations value.
    /// </summary>
    MultipleCloseConfirmations,
}
