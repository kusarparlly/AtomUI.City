namespace AtomUI.City.Presentation;

/// <summary>
/// Represents ui state feedback policy.
/// </summary>
public static class UiStateFeedbackPolicy
{
    /// <summary>
    /// Executes the can notify view model operation.
    /// </summary>
    public static bool CanNotifyViewModel(UiStateFeedbackKind kind)
    {
        return kind is
            UiStateFeedbackKind.ValueChanged or
            UiStateFeedbackKind.CommandInvoked or
            UiStateFeedbackKind.InteractionCompleted or
            UiStateFeedbackKind.ValidationRequested or
            UiStateFeedbackKind.SelectionChanged;
    }
}
