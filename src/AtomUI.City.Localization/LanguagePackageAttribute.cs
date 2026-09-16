namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LanguagePackageAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>LanguagePackageAttribute</c> type.
    /// </summary>
    public LanguagePackageAttribute(string packageId, string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        PackageId = packageId;
        Culture = culture;
    }

    /// <summary>
    /// Gets package id.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// Gets culture.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public ResourceScope Scope { get; set; } = ResourceScope.Module;

    /// <summary>
    /// Gets or sets scope id.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// Gets or sets resource base name.
    /// </summary>
    public string? ResourceBaseName { get; set; }

    /// <summary>
    /// Gets or sets fallback culture.
    /// </summary>
    public string? FallbackCulture { get; set; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets checksum.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Gets or sets contribution id.
    /// </summary>
    public string? ContributionId { get; set; }
}
