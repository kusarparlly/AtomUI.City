namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ipresentation localization bridge.
/// </summary>
public interface IPresentationLocalizationBridge
{
    /// <summary>
    /// Executes the apply culture async operation.
    /// </summary>
    ValueTask<LocalizationResult> ApplyCultureAsync(
        CultureState state,
        CancellationToken cancellationToken = default);
}
