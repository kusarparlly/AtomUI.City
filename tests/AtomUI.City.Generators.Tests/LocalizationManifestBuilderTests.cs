using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.Localization;

namespace AtomUI.City.Generators.Tests;

public sealed class LocalizationManifestBuilderTests
{
    [Fact]
    public void BuildCreatesDeterministicManifestWithSupportedCulturesAndFallbacks()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.en-US", "en-US"),
                Package("Settings.zh-CN", "zh-CN", fallbackCulture: "zh-Hans"),
                Package("Settings.zh-Hans", "zh-Hans"),
            ],
            [
                Resource("Settings.Title", "Settings.zh-CN"),
                Resource("Settings.Title", "Settings.en-US"),
            ]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["Settings.en-US", "Settings.zh-CN", "Settings.zh-Hans"], result.Manifest.Packages.Select(package => package.PackageId));
        Assert.Equal(["en-US", "zh-CN", "zh-Hans"], result.Manifest.SupportedCultures);
        Assert.Collection(
            result.Manifest.Fallbacks,
            fallback =>
            {
                Assert.Equal("zh-CN", fallback.Culture);
                Assert.Equal("zh-Hans", fallback.FallbackCulture);
            });
        Assert.Equal(["Settings.en-US:Settings.Title", "Settings.zh-CN:Settings.Title"], result.Manifest.Resources.Select(resource => resource.PackageId + ":" + resource.Key));
    }

    [Fact]
    public void BuildReportsResourcesThatReferenceMissingPackages()
    {
        var result = LocalizationManifestBuilder.Build(
            [],
            [
                Resource("Settings.Title", "Settings.zh-CN"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Empty(result.Manifest.Resources);
    }

    [Fact]
    public void BuildReportsInvalidPackageCultures()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.invalid", "invalid culture!"),
            ],
            [
                Resource("Settings.Title", "Settings.invalid"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("culture", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("Settings.invalid", diagnostic.Target);
        Assert.Empty(result.Manifest.Packages);
        Assert.Empty(result.Manifest.Resources);
    }

    [Fact]
    public void BuildReportsDuplicateResourceKeysWithinSamePackageAndScope()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.zh-CN", "zh-CN"),
            ],
            [
                Resource("Settings.Title", "Settings.zh-CN"),
                Resource("Settings.Title", "Settings.zh-CN"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Empty(result.Manifest.Resources);
    }

    [Fact]
    public void BuildReportsFallbackCulturesWithoutLanguagePackage()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.zh-CN", "zh-CN", fallbackCulture: "zh-Hans"),
            ],
            [
                Resource("Settings.Title", "Settings.zh-CN"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Empty(result.Manifest.Packages);
        Assert.Empty(result.Manifest.Fallbacks);
    }

    [Fact]
    public void BuildReturnsReadonlyManifestCollections()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.en-US", "en-US"),
            ],
            [
                Resource("Settings.Title", "Settings.en-US"),
            ]);
        var packages = Assert.IsAssignableFrom<IList<LanguagePackageManifestEntry>>(result.Manifest.Packages);
        var resources = Assert.IsAssignableFrom<IList<LocalizedResourceManifestEntry>>(result.Manifest.Resources);
        var supportedCultures = Assert.IsAssignableFrom<IList<string>>(result.Manifest.SupportedCultures);
        var diagnostics = Assert.IsAssignableFrom<IList<GeneratorDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => packages[0] = new LanguagePackageManifestEntry("Other", "en-US", ResourceScopeMetadata.Module, "Other", null, null, null));
        Assert.Throws<NotSupportedException>(() => resources[0] = new LocalizedResourceManifestEntry("Other", "Settings.en-US", "en-US", LocalizedResourceMetadataKind.String, ResourceScopeMetadata.Module, null, false));
        Assert.Throws<NotSupportedException>(() => supportedCultures[0] = "fr-FR");
        Assert.Throws<NotSupportedException>(() => diagnostics.Add(new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, "Changed")));
        Assert.Equal("Settings.en-US", result.Manifest.Packages[0].PackageId);
        Assert.Equal("Settings.Title", result.Manifest.Resources[0].Key);
        Assert.Equal("en-US", result.Manifest.SupportedCultures[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void BuildUsesCultureAndPackageIdAsPackageIdentity()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings", "en-US"),
                Package("Settings", "zh-CN"),
            ],
            [
                Resource("Settings.Title", "Settings", "en-US"),
                Resource("Settings.Title", "Settings", "zh-CN"),
            ]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Manifest.Packages.Count);
        Assert.Equal(["en-US", "zh-CN"], result.Manifest.Resources.Select(resource => resource.Culture));
    }

    [Fact]
    public void BuildRejectsAmbiguousResourcePackageIdWithoutCulture()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings", "en-US"),
                Package("Settings", "zh-CN"),
            ],
            [Resource("Settings.Title", "Settings")]);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("must specify Culture", StringComparison.Ordinal));
        Assert.Empty(result.Manifest.Resources);
    }

    [Fact]
    public void BuildRejectsCultureIdentityDuplicatesWithDifferentCasing()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings", "en-US"),
                Package("Settings", "en-us"),
            ],
            []);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("declared more than once", StringComparison.Ordinal));
        Assert.Empty(result.Manifest.Packages);
    }

    [Fact]
    public void BuildRejectsFallbackCycles()
    {
        var result = LocalizationManifestBuilder.Build(
            [
                Package("Settings.zh-CN", "zh-CN", fallbackCulture: "en-US"),
                Package("Settings.en-US", "en-US", fallbackCulture: "zh-CN"),
            ],
            []);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("fallback graph contains a cycle", StringComparison.Ordinal));
        Assert.Empty(result.Manifest.Packages);
    }

    [Fact]
    public void BuildRequiresAssemblyResourceBaseName()
    {
        var result = LocalizationManifestBuilder.Build(
            [new LanguagePackageMetadata(
                "Settings.en-US",
                "en-US",
                ResourceScopeMetadata.Host,
                resourceBaseName: null,
                fallbackCulture: null,
                version: null,
                checksum: null)],
            []);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("resource base name", StringComparison.Ordinal));
        Assert.Empty(result.Manifest.Packages);
    }

    [Fact]
    public void BuildRejectsUnknownPackageAndResourceScopes()
    {
        var result = LocalizationManifestBuilder.Build(
            [new LanguagePackageMetadata(
                "Settings.en-US",
                "en-US",
                (ResourceScopeMetadata)999,
                resourceBaseName: "Sample.App.Resources.Settings",
                fallbackCulture: null,
                version: null,
                checksum: null)],
            [new LocalizedResourceMetadata(
                "Settings.Title",
                "Settings.en-US",
                LocalizedResourceMetadataKind.String,
                (ResourceScopeMetadata)999,
                version: null,
                critical: false)]);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("unknown resource scope", StringComparison.Ordinal));
        Assert.Empty(result.Manifest.Packages);
    }

    [Theory]
    [InlineData(nameof(LocalizedResourceMetadataKind.Pluralization))]
    [InlineData(nameof(LocalizedResourceMetadataKind.ResourceObject))]
    [InlineData(nameof(LocalizedResourceMetadataKind.FlowDirection))]
    [InlineData(nameof(LocalizedResourceMetadataKind.CultureMetadata))]
    public void BuildRejectsResourceKindsWithoutRuntimeContract(string kindName)
    {
        var kind = Enum.Parse<LocalizedResourceMetadataKind>(kindName);
        var result = LocalizationManifestBuilder.Build(
            [Package("Settings.zh-CN", "zh-CN")],
            [new LocalizedResourceMetadata(
                "Settings.Value",
                "Settings.zh-CN",
                kind,
                ResourceScopeMetadata.Module,
                version: null,
                critical: false,
                scopeId: "settings.module")]);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("no runtime execution contract", StringComparison.Ordinal));
    }

    [Fact]
    public void LocalizationMetadataCollectionsRejectExternalMutation()
    {
        var packageList = new List<LanguagePackageMetadata> { Package("Settings.en-US", "en-US") };
        var resourceList = new List<LocalizedResourceMetadata> { Resource("Settings.Title", "Settings.en-US") };
        var metadata = new LocalizationMetadata(
            packageList,
            resourceList);
        var packages = Assert.IsAssignableFrom<IList<LanguagePackageMetadata>>(metadata.Packages);
        var resources = Assert.IsAssignableFrom<IList<LocalizedResourceMetadata>>(metadata.Resources);

        Assert.Throws<NotSupportedException>(() => packages[0] = Package("Other", "en-US"));
        Assert.Throws<NotSupportedException>(() => resources[0] = Resource("Other", "Settings.en-US"));
        Assert.Equal("Settings.en-US", metadata.Packages[0].PackageId);
        Assert.Equal("Settings.Title", metadata.Resources[0].Key);
    }

    private static LanguagePackageMetadata Package(
        string packageId,
        string culture,
        string? fallbackCulture = null)
    {
        return new LanguagePackageMetadata(
            packageId,
            culture,
            ResourceScopeMetadata.Module,
            resourceBaseName: "Sample.App.Resources." + packageId,
            fallbackCulture,
            version: null,
            checksum: null,
            scopeId: "settings.module");
    }

    private static LocalizedResourceMetadata Resource(
        string key,
        string packageId,
        string? culture = null)
    {
        return new LocalizedResourceMetadata(
            key,
            packageId,
            LocalizedResourceMetadataKind.String,
            ResourceScopeMetadata.Module,
            version: null,
            critical: false,
            scopeId: "settings.module",
            culture: culture);
    }
}
