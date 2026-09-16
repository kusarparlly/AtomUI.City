namespace AtomUI.City.Localization;

/// <summary>
/// Represents file language package provider.
/// </summary>
public sealed class FileLanguagePackageProvider : ILanguagePackageProvider
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.File;

    /// <summary>
    /// Executes the load async operation.
    /// </summary>
    public async ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.ProviderKind != Kind)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.InvalidDescriptor,
                    $"Language package '{descriptor.PackageId}' is not a file package."));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(descriptor.Location))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageNotFound,
                    $"File language package '{descriptor.Location}' was not found."));
        }

        if (string.IsNullOrWhiteSpace(descriptor.AllowedRootPath))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.InvalidDescriptor,
                    $"File language package '{descriptor.PackageId}' requires an allowed root path."));
        }

        try
        {
            var fullPath = Path.GetFullPath(descriptor.Location);
            var fullRootPath = Path.GetFullPath(descriptor.AllowedRootPath);
            var relativePath = Path.GetRelativePath(fullRootPath, fullPath);
            if (Path.IsPathRooted(relativePath)
                || relativePath.Equals("..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.InvalidDescriptor,
                        $"File language package path '{fullPath}' is outside allowed root '{fullRootPath}'."));
            }

            if (!File.Exists(fullPath))
            {
                return LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        $"File language package '{fullPath}' was not found."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(fullPath);
            cancellationToken.ThrowIfCancellationRequested();

            var result = LocPackReader.Read(stream, descriptor, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }
        catch (OperationCanceledException exception)
        {
            return Cancelled(cancellationToken, exception);
        }
        catch (Exception exception)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageLoadFailed,
                    exception.Message,
                    Exception: exception));
        }
    }

    private static LanguagePackageLoadResult Cancelled(
        CancellationToken cancellationToken,
        OperationCanceledException? exception = null)
    {
        return LanguagePackageLoadResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.Cancelled,
                "Language package load was cancelled.",
                exception ?? new OperationCanceledException(cancellationToken)));
    }
}
