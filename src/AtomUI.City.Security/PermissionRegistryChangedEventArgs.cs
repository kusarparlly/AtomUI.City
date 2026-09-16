namespace AtomUI.City.Security;

/// <summary>
/// Represents permission registry changed event args.
/// </summary>
public sealed class PermissionRegistryChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>PermissionRegistryChangedEventArgs</c> type.
    /// </summary>
    public PermissionRegistryChangedEventArgs(
        long revision,
        string? permissionName = null,
        string? contributionId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (permissionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        Revision = revision;
        PermissionName = permissionName;
        ContributionId = contributionId;
    }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets permission name.
    /// </summary>
    public string? PermissionName { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }
}
