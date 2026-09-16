namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package load result.
/// </summary>
public sealed class LanguagePackageLoadResult
{
    private LanguagePackageLoadResult(LanguagePackage? package, LocalizationError? error)
    {
        Package = package;
        Error = error;
    }

    /// <summary>
    /// Gets package.
    /// </summary>
    public LanguagePackage? Package { get; }

    /// <summary>
    /// Gets error.
    /// </summary>
    public LocalizationError? Error { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static LanguagePackageLoadResult Success(LanguagePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return new LanguagePackageLoadResult(package, error: null);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static LanguagePackageLoadResult Failed(LocalizationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new LanguagePackageLoadResult(package: null, error);
    }
}
