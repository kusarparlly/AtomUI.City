namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization result.
/// </summary>
public sealed class LocalizationResult
{
    private LocalizationResult(LocalizationError? error)
    {
        Error = error;
    }

    /// <summary>
    /// Gets error.
    /// </summary>
    public LocalizationError? Error { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static LocalizationResult Success()
    {
        return new LocalizationResult(error: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static LocalizationResult Failed(LocalizationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new LocalizationResult(error);
    }
}
