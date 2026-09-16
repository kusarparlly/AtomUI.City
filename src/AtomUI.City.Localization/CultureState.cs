using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents culture state.
/// </summary>
public sealed class CultureState
{
    /// <summary>
    /// Initializes a new instance of the <c>CultureState</c> type.
    /// </summary>
    public CultureState(
        CultureInfo currentCulture,
        CultureInfo currentUICulture,
        IReadOnlyList<CultureInfo> fallbackCultures,
        long revision,
        IReadOnlyList<string> loadedPackageIds)
    {
        ArgumentNullException.ThrowIfNull(fallbackCultures);
        ArgumentNullException.ThrowIfNull(loadedPackageIds);
        if (fallbackCultures.Any(culture => culture is null))
        {
            throw new ArgumentException("Fallback cultures cannot contain null values.", nameof(fallbackCultures));
        }

        if (loadedPackageIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Loaded package ids cannot contain empty values.", nameof(loadedPackageIds));
        }

        CurrentCulture = CultureInfoSnapshot.Create(currentCulture);
        CurrentUICulture = CultureInfoSnapshot.Create(currentUICulture);
        FallbackCultures = Array.AsReadOnly(
            fallbackCultures.Select(CultureInfoSnapshot.Create).ToArray());
        Revision = revision;
        LoadedPackageIds = Array.AsReadOnly(loadedPackageIds.ToArray());
    }

    /// <summary>
    /// Gets current culture.
    /// </summary>
    public CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Gets current uiculture.
    /// </summary>
    public CultureInfo CurrentUICulture { get; }

    /// <summary>
    /// Gets fallback cultures.
    /// </summary>
    public IReadOnlyList<CultureInfo> FallbackCultures { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets loaded package ids.
    /// </summary>
    public IReadOnlyList<string> LoadedPackageIds { get; }
}
