namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package registration.
/// </summary>
public sealed class LanguagePackageRegistration
{
    /// <summary>
    /// Initializes a new instance of the <c>LanguagePackageRegistration</c> type.
    /// </summary>
    internal LanguagePackageRegistration(
        LanguagePackageDescriptor descriptor,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        Descriptor = descriptor;
        OwnerId = ownerId;
    }

    /// <summary>
    /// Gets descriptor.
    /// </summary>
    public LanguagePackageDescriptor Descriptor { get; }

    /// <summary>
    /// Gets owner id.
    /// </summary>
    public string OwnerId { get; }
}
