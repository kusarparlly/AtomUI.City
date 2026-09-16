using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localized message.
/// </summary>
public sealed class LocalizedMessage
{
    private LocalizedMessage(
        string key,
        string value,
        CultureInfo culture,
        bool isFallback,
        bool isMissing,
        bool isFormatFailed)
    {
        Key = key;
        Value = value;
        Culture = culture;
        IsFallback = isFallback;
        IsMissing = isMissing;
        IsFormatFailed = isFormatFailed;
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
    /// Gets a value indicating whether is format failed.
    /// </summary>
    public bool IsFormatFailed { get; }

    /// <summary>
    /// Executes the from string operation.
    /// </summary>
    public static LocalizedMessage FromString(
        LocalizedString localizedString,
        string value,
        bool isFormatFailed = false)
    {
        ArgumentNullException.ThrowIfNull(localizedString);
        ArgumentNullException.ThrowIfNull(value);

        return new LocalizedMessage(
            localizedString.Key,
            value,
            localizedString.Culture,
            localizedString.IsFallback,
            localizedString.IsMissing,
            isFormatFailed);
    }
}
