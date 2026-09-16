namespace AtomUI.City.Localization;

/// <summary>
/// Defines the contract for ilocalization scope lease.
/// </summary>
public interface ILocalizationScopeLease : IDisposable
{
    /// <summary>
    /// Gets context.
    /// </summary>
    LocalizationLookupContext Context { get; }

    /// <summary>
    /// Gets a value indicating whether is disposed.
    /// </summary>
    bool IsDisposed { get; }
}
