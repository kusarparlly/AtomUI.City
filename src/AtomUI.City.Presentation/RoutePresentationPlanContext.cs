using AtomUI.City.Routing;

namespace AtomUI.City.Presentation;

/// <summary>
/// Describes the committed route for which an application must create a presentation plan.
/// </summary>
public sealed class RoutePresentationPlanContext
{
    internal RoutePresentationPlanContext(
        NavigationResult navigation,
        NavigationCommitReceipt receipt,
        string outletName)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        ArgumentException.ThrowIfNullOrWhiteSpace(outletName);
        OutletName = outletName;
    }

    /// <summary>
    /// Gets the completed Router result.
    /// </summary>
    public NavigationResult Navigation { get; }

    /// <summary>
    /// Gets the identity and snapshots of the Router commit.
    /// </summary>
    public NavigationCommitReceipt Receipt { get; }

    /// <summary>
    /// Gets the target outlet name.
    /// </summary>
    public string OutletName { get; }
}
