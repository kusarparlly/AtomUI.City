namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation plugin unload error.
/// </summary>
/// <param name="Kind">The kind value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record PresentationPluginUnloadError(
    PresentationPluginUnloadErrorKind Kind,
    string Message,
    Exception? Exception = null);
