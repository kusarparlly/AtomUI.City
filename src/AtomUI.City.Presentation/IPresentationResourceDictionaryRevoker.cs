namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation resource dictionary revoker.
/// </summary>
public interface IPresentationResourceDictionaryRevoker
{
    /// <summary>
    /// Executes the revoke async operation.
    /// </summary>
    ValueTask<PresentationResourceDictionaryRevokeResult> RevokeAsync(
        PresentationResourceDictionaryRevocation revocation,
        CancellationToken cancellationToken = default);
}
