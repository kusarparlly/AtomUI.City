using System.Globalization;
using AtomUI.City.State;

namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ilocalization service.
/// </summary>
public interface ILocalizationService : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets current culture.
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Gets culture revision.
    /// </summary>
    long CultureRevision { get; }

    /// <summary>
    /// Gets culture state.
    /// </summary>
    IReadOnlyState<CultureState> CultureState { get; }

    /// <summary>
    /// Executes the activate scope operation.
    /// </summary>
    ILocalizationScopeLease ActivateScope(LocalizationLookupContext context);

    /// <summary>
    /// Executes the set culture async operation.
    /// </summary>
    ValueTask<LocalizationResult> SetCultureAsync(
        string cultureName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get string async operation.
    /// </summary>
    ValueTask<LocalizedString> GetStringAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get string async operation.
    /// </summary>
    ValueTask<LocalizedString> GetStringAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get message async operation.
    /// </summary>
    ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the get message async operation.
    /// </summary>
    ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the create text async operation.
    /// </summary>
    ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the create text async operation.
    /// </summary>
    ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the create message text async operation.
    /// </summary>
    ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the create message text async operation.
    /// </summary>
    ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the revoke packages by contribution id async operation.
    /// </summary>
    ValueTask<int> RevokePackagesByContributionIdAsync(
        string contributionId,
        CancellationToken cancellationToken = default);
}
