using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localized text changed event args.
/// </summary>
public sealed class LocalizedTextChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>LocalizedTextChangedEventArgs</c> type.
    /// </summary>
    public LocalizedTextChangedEventArgs(LocalizedString text, long revision)
    {
        ArgumentNullException.ThrowIfNull(text);

        Key = text.Key;
        Value = text.Value;
        Culture = text.Culture;
        IsFallback = text.IsFallback;
        IsMissing = text.IsMissing;
        Revision = revision;
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
    /// Gets revision.
    /// </summary>
    public long Revision { get; }
}
