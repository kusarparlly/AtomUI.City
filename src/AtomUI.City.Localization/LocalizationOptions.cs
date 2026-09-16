using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization options.
/// </summary>
public sealed class LocalizationOptions
{
    private CultureInfo _defaultCulture = CultureInfo.InvariantCulture;
    private CultureInfo _defaultUICulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets language packages.
    /// </summary>
    public IList<LanguagePackageDescriptor> LanguagePackages { get; } =
        new List<LanguagePackageDescriptor>();

    /// <summary>
    /// Represents the default culture value.
    /// </summary>
    public CultureInfo DefaultCulture
    {
        get => _defaultCulture;
        set => _defaultCulture = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Represents the default uiculture value.
    /// </summary>
    public CultureInfo DefaultUICulture
    {
        get => _defaultUICulture;
        set => _defaultUICulture = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets fallback cultures.
    /// </summary>
    public IList<CultureInfo> FallbackCultures { get; } =
        new List<CultureInfo>();
}
