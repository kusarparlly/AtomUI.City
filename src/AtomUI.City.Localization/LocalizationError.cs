namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization error.
/// </summary>
/// <param name="Kind">The kind value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record LocalizationError(
    LocalizationErrorKind Kind,
    string Message,
    Exception? Exception = null);
