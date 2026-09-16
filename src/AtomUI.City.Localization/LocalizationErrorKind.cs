namespace AtomUI.City.Localization;

/// <summary>
/// Defines the supported localization error kind values.
/// </summary>
public enum LocalizationErrorKind
{
    /// <summary>
    /// Represents the package not found value.
    /// </summary>
    PackageNotFound,
    /// <summary>
    /// Represents the package load failed value.
    /// </summary>
    PackageLoadFailed,
    /// <summary>
    /// Represents the package version mismatch value.
    /// </summary>
    PackageVersionMismatch,
    /// <summary>
    /// Represents the package culture mismatch value.
    /// </summary>
    PackageCultureMismatch,
    /// <summary>
    /// Represents the presentation apply failed value.
    /// </summary>
    PresentationApplyFailed,
    /// <summary>
    /// Represents the resource missing value.
    /// </summary>
    ResourceMissing,
    /// <summary>
    /// Represents the format failed value.
    /// </summary>
    FormatFailed,
    /// <summary>
    /// Represents the refresh failed value.
    /// </summary>
    RefreshFailed,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents the unknown value.
    /// </summary>
    Unknown,
    /// <summary>
    /// Represents the invalid culture value.
    /// </summary>
    InvalidCulture,
    /// <summary>
    /// Represents the package already registered value.
    /// </summary>
    PackageAlreadyRegistered,
    /// <summary>
    /// Represents the owner revoked value.
    /// </summary>
    OwnerRevoked,
    /// <summary>
    /// Represents the resource revoked value.
    /// </summary>
    ResourceRevoked,
    /// <summary>
    /// Represents the invalid descriptor value.
    /// </summary>
    InvalidDescriptor,
    /// <summary>
    /// Represents the reentrant operation value.
    /// </summary>
    ReentrantOperation,
    /// <summary>
    /// Represents the service disposed value.
    /// </summary>
    ServiceDisposed,
    /// <summary>
    /// Represents the package identity mismatch value.
    /// </summary>
    PackageIdentityMismatch,
    /// <summary>
    /// Represents the package checksum mismatch value.
    /// </summary>
    PackageChecksumMismatch,
    /// <summary>
    /// Represents the package schema mismatch value.
    /// </summary>
    PackageSchemaMismatch,
    /// <summary>
    /// Represents the package too large value.
    /// </summary>
    PackageTooLarge,
    /// <summary>
    /// Represents the invalid resource value.
    /// </summary>
    InvalidResource,
}
