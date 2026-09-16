namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation exception.
/// </summary>
public sealed class PresentationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationException</c> type.
    /// </summary>
    public PresentationException(PresentationError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <c>PresentationException</c> type.
    /// </summary>
    public PresentationException(
        PresentationError error,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    /// <summary>
    /// Gets error.
    /// </summary>
    public PresentationError Error { get; }
}
