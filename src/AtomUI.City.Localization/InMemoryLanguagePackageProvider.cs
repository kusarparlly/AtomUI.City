namespace AtomUI.City.Localization;

/// <summary>
/// Represents in memory language package provider.
/// </summary>
public sealed class InMemoryLanguagePackageProvider : ILanguagePackageProvider
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

    /// <summary>
    /// Executes the load async operation.
    /// </summary>
    public ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.ProviderKind != Kind)
        {
            return ValueTask.FromResult(ProviderKindMismatch(descriptor));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.Cancelled,
                        "Language package load was cancelled.",
                        new OperationCanceledException(cancellationToken))));
        }

        if (descriptor.InMemoryResources is null)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        $"In-memory language package '{descriptor.PackageId}' has no resources.")));
        }

        return ValueTask.FromResult(
            LanguagePackageLoadResult.Success(
                LanguagePackage.Create(descriptor, descriptor.InMemoryResources)));
    }

    private static LanguagePackageLoadResult ProviderKindMismatch(LanguagePackageDescriptor descriptor)
    {
        return LanguagePackageLoadResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.InvalidDescriptor,
                $"Language package '{descriptor.PackageId}' is not an in-memory package."));
    }
}
