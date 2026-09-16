namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ilanguage package provider.
/// </summary>
public interface ILanguagePackageProvider
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    LanguagePackageProviderKind Kind { get; }

    /// <summary>
    /// Executes the load async operation.
    /// </summary>
    ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default);
}
