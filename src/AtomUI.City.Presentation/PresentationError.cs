namespace AtomUI.City.Presentation;

public enum PresentationError
{
    DuplicateView,
    DuplicateWindow,
    DuplicateOutlet,
    ViewNotFound,
    ViewModelNotFound,
    ViewModelCreationFailed,
    ViewCreationFailed,
    BindingFailed,
    OutletNotFound,
    OutletCommitFailed,
    RuntimeNotReady,
    RuntimeStopping,
    DispatcherUnavailable,
    WindowNotFound,
    WindowCloseRejected,
    PresentationOutOfSync,
    OutletQueueFull,
    InteractionQueueFull,
    CandidateOwnershipViolation,
    MultipleCloseConfirmations,
}
