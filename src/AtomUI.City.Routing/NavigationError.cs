namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation error.
/// </summary>
/// <param name="Code">The code value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record NavigationError(
    string Code,
    string Message,
    Exception? Exception = null);
