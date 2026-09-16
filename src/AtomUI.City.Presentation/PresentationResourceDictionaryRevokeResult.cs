namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation resource dictionary revoke result.
/// </summary>
public sealed class PresentationResourceDictionaryRevokeResult
{
    /// <summary>
    /// Initializes a new instance of the <c>PresentationResourceDictionaryRevokeResult</c> type.
    /// </summary>
    public PresentationResourceDictionaryRevokeResult(IReadOnlyList<Exception> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Gets errors.
    /// </summary>
    public IReadOnlyList<Exception> Errors { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets success.
    /// </summary>
    public static PresentationResourceDictionaryRevokeResult Success() => new([]);
}
