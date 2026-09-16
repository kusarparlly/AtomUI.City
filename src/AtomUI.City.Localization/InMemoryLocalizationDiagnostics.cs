namespace AtomUI.City.Localization;

/// <summary>
/// Represents in memory localization diagnostics.
/// </summary>
public sealed class InMemoryLocalizationDiagnostics : ILocalizationDiagnostics
{
    private readonly List<LocalizationDiagnosticRecord> _records = [];
    private readonly object _syncRoot = new();

    /// <summary>
    /// Represents the records value.
    /// </summary>
    public IReadOnlyList<LocalizationDiagnosticRecord> Records
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    public void Write(LocalizationDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_syncRoot)
        {
            _records.Add(record);
        }
    }
}
