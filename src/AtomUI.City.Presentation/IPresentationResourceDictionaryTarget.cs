namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation resource dictionary target.
/// </summary>
public interface IPresentationResourceDictionaryTarget
{
    /// <summary>
    /// Executes the revoke resources async operation.
    /// </summary>
    ValueTask RevokeResourcesAsync(
        PresentationResourceDictionaryRevocation revocation,
        CancellationToken cancellationToken = default);
}
