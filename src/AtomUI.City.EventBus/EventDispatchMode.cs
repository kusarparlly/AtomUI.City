namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event dispatch mode values.
/// </summary>
public enum EventDispatchMode
{
    /// <summary>
    /// Represents the post value.
    /// </summary>
    Post = 0,
    /// <summary>
    /// Represents the inline if allowed value.
    /// </summary>
    InlineIfAllowed = 1,
}
