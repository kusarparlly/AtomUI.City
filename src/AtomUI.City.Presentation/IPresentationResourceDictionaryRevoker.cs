namespace AtomUI.City.Presentation;

public interface IPresentationResourceDictionaryRevoker
{
    ValueTask<PresentationResourceDictionaryRevokeResult> RevokeAsync(
        PresentationResourceDictionaryRevocation revocation,
        CancellationToken cancellationToken = default);
}
