namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ilocalization diagnostics.
/// </summary>
public interface ILocalizationDiagnostics
{
    /// <summary>
    /// Gets records.
    /// </summary>
    IReadOnlyList<LocalizationDiagnosticRecord> Records { get; }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    void Write(LocalizationDiagnosticRecord record);
}
