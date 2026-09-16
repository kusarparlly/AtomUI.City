namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported ui state feedback kind values.
/// </summary>
public enum UiStateFeedbackKind
{
    /// <summary>
    /// Represents the value changed value.
    /// </summary>
    ValueChanged,
    /// <summary>
    /// Represents the command invoked value.
    /// </summary>
    CommandInvoked,
    /// <summary>
    /// Represents the interaction completed value.
    /// </summary>
    InteractionCompleted,
    /// <summary>
    /// Represents the validation requested value.
    /// </summary>
    ValidationRequested,
    /// <summary>
    /// Represents the selection changed value.
    /// </summary>
    SelectionChanged,
    /// <summary>
    /// Represents the hover changed value.
    /// </summary>
    HoverChanged,
    /// <summary>
    /// Represents the pointer moved value.
    /// </summary>
    PointerMoved,
    /// <summary>
    /// Represents the layout updated value.
    /// </summary>
    LayoutUpdated,
    /// <summary>
    /// Represents the scroll offset changed value.
    /// </summary>
    ScrollOffsetChanged,
    /// <summary>
    /// Represents the animation state changed value.
    /// </summary>
    AnimationStateChanged,
}
