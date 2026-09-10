namespace AtomUI.City.Presentation;

public interface IPresentationResourceDictionaryTarget
{
    ValueTask RevokeResourcesAsync(
        PresentationResourceDictionaryRevocation revocation,
        CancellationToken cancellationToken = default);
}
