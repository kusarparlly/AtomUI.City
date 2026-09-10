namespace AtomUI.City.Presentation;

public sealed class PresentationResourceDictionaryRevokeResult
{
    public PresentationResourceDictionaryRevokeResult(IReadOnlyList<Exception> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<Exception> Errors { get; }

    public bool Succeeded => Errors.Count == 0;

    public static PresentationResourceDictionaryRevokeResult Success() => new([]);
}
