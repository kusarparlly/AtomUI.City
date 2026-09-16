using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localized string.
/// </summary>
public sealed class LocalizedString
{
    private LocalizedString(
        string key,
        string value,
        CultureInfo culture,
        bool isFallback,
        bool isMissing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(culture);

        Key = key;
        Value = value;
        Culture = CultureInfoSnapshot.Create(culture);
        IsFallback = isFallback;
        IsMissing = isMissing;
    }

    /// <summary>
    /// Gets key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets culture.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets a value indicating whether is fallback.
    /// </summary>
    public bool IsFallback { get; }

    /// <summary>
    /// Gets a value indicating whether is missing.
    /// </summary>
    public bool IsMissing { get; }

    /// <summary>
    /// Executes the found operation.
    /// </summary>
    public static LocalizedString Found(string key, string value, CultureInfo culture)
    {
        return new LocalizedString(key, value, culture, isFallback: false, isMissing: false);
    }

    /// <summary>
    /// Executes the fallback operation.
    /// </summary>
    public static LocalizedString Fallback(string key, string value, CultureInfo culture)
    {
        return new LocalizedString(key, value, culture, isFallback: true, isMissing: false);
    }

    /// <summary>
    /// Executes the missing operation.
    /// </summary>
    public static LocalizedString Missing(string key, CultureInfo culture)
    {
        return new LocalizedString(key, $"!{key}!", culture, isFallback: false, isMissing: true);
    }
}
