namespace AtomUI.City.Routing;

/// <summary>
/// Identifies one committed navigation and the snapshots on both sides of that commit.
/// </summary>
public sealed class NavigationCommitReceipt
{
    internal NavigationCommitReceipt(
        Guid navigationId,
        long generation,
        NavigationSnapshot previous,
        NavigationSnapshot current,
        object ownerToken,
        object restoreCheckpoint)
    {
        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        NavigationId = navigationId;
        Generation = generation;
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
        RestoreCheckpoint = restoreCheckpoint ?? throw new ArgumentNullException(nameof(restoreCheckpoint));
    }

    /// <summary>
    /// Gets the navigation operation identity.
    /// </summary>
    public Guid NavigationId { get; }

    /// <summary>
    /// Gets the monotonically increasing commit generation within its router.
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// Gets the snapshot that was current before the commit.
    /// </summary>
    public NavigationSnapshot Previous { get; }

    /// <summary>
    /// Gets the snapshot produced by the commit.
    /// </summary>
    public NavigationSnapshot Current { get; }

    internal object OwnerToken { get; }

    internal object RestoreCheckpoint { get; }
}
