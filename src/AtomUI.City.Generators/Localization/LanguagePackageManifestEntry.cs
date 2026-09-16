namespace AtomUI.City.Generators.Localization;

internal sealed class LanguagePackageManifestEntry
{
    public LanguagePackageManifestEntry(
        string packageId,
        string culture,
        ResourceScopeMetadata scope,
        string? resourceBaseName,
        string? fallbackCulture,
        string? version,
        string? checksum,
        string? scopeId = null,
        string? contributionId = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id cannot be empty.", nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(culture))
        {
            throw new ArgumentException("Culture cannot be empty.", nameof(culture));
        }

        PackageId = packageId;
        Culture = culture;
        Scope = scope;
        ResourceBaseName = resourceBaseName;
        FallbackCulture = fallbackCulture;
        Version = version;
        Checksum = checksum;
        ScopeId = scopeId;
        ContributionId = contributionId;
    }

    public string PackageId { get; }

    public string Culture { get; }

    public ResourceScopeMetadata Scope { get; }

    public string? ResourceBaseName { get; }

    public string? FallbackCulture { get; }

    public string? Version { get; }

    public string? Checksum { get; }

    public string? ScopeId { get; }

    public string? ContributionId { get; }
}
