namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation resource lease.
/// </summary>
public interface IPresentationResourceLease : IDisposable
{
    /// <summary>
    /// Gets contribution.
    /// </summary>
    PresentationResourceContribution Contribution { get; }
}
