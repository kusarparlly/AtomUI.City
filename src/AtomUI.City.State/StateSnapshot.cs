namespace AtomUI.City.State;

/// <summary>
/// Represents state snapshot.
/// </summary>
public sealed class StateSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <c>StateSnapshot</c> type.
    /// </summary>
    public StateSnapshot(IReadOnlyList<StateSnapshotEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var snapshotEntries = entries.ToArray();

        if (snapshotEntries.Any(entry => entry is null))
        {
            throw new ArgumentException("State snapshot entries must not contain null.", nameof(entries));
        }

        Entries = Array.AsReadOnly(snapshotEntries);
    }

    /// <summary>
    /// Gets entries.
    /// </summary>
    public IReadOnlyList<StateSnapshotEntry> Entries { get; }
}
