using System.Globalization;

namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ilocalized text.
/// </summary>
public interface ILocalizedText : IDisposable
{
    /// <summary>
    /// Occurs when changed.
    /// </summary>
    event EventHandler<LocalizedTextChangedEventArgs>? Changed;

    /// <summary>
    /// Gets key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Gets culture.
    /// </summary>
    CultureInfo Culture { get; }

    /// <summary>
    /// Gets revision.
    /// </summary>
    long Revision { get; }

    /// <summary>
    /// Gets a value indicating whether is fallback.
    /// </summary>
    bool IsFallback { get; }

    /// <summary>
    /// Gets a value indicating whether is missing.
    /// </summary>
    bool IsMissing { get; }

    /// <summary>
    /// Executes the refresh async operation.
    /// </summary>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
