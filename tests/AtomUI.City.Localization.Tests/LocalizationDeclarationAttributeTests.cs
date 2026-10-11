using System.Globalization;
using AtomUI.City.Localization;

[assembly: LanguagePackage(
    "Host.en-US",
    "en-US",
    Scope = ResourceScope.Host,
    ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
    FallbackCulture = "en",
    Version = "1.0.0",
    ContributionId = "host.localization")]
[assembly: LanguagePackage(
    "Missing.en-US",
    "en-US",
    Scope = ResourceScope.Plugin,
    ScopeId = "plugin.missing",
    ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Missing.en-US.locpack.json",
    ContributionId = "plugin.missing.localization")]

namespace AtomUI.City.Localization.Tests;

public sealed class LocalizationDeclarationAttributeTests
{
    [Fact]
    public void LanguagePackageAttributeStoresPackageDescriptor()
    {
        var attribute = new LanguagePackageAttribute("Host.zh-CN", "zh-CN")
        {
            Scope = ResourceScope.Host,
            ScopeId = null,
            ResourceBaseName = "Sample.App.Resources.Host",
            FallbackCulture = "zh-Hans",
            Version = "1.0.0",
            Checksum = "sha256:sample",
            ContributionId = "settings.localization",
        };

        Assert.Equal("Host.zh-CN", attribute.PackageId);
        Assert.Equal("zh-CN", attribute.Culture);
        Assert.Equal(ResourceScope.Host, attribute.Scope);
        Assert.Null(attribute.ScopeId);
        Assert.Equal("Sample.App.Resources.Host", attribute.ResourceBaseName);
        Assert.Equal("zh-Hans", attribute.FallbackCulture);
        Assert.Equal("1.0.0", attribute.Version);
        Assert.Equal("sha256:sample", attribute.Checksum);
        Assert.Equal("settings.localization", attribute.ContributionId);
    }

    [Fact]
    public void LocalizedResourceAttributeStoresResourceDescriptor()
    {
        var attribute = new LocalizedResourceAttribute("Settings.Title", "Settings.zh-CN")
        {
            Kind = LocalizedResourceKind.FormattedString,
            Scope = ResourceScope.Module,
            ScopeId = "settings.module",
            Culture = "zh-CN",
            Version = "1.0.0",
            Critical = true,
        };

        Assert.Equal("Settings.Title", attribute.Key);
        Assert.Equal("Settings.zh-CN", attribute.PackageId);
        Assert.Equal(LocalizedResourceKind.FormattedString, attribute.Kind);
        Assert.Equal(ResourceScope.Module, attribute.Scope);
        Assert.Equal("settings.module", attribute.ScopeId);
        Assert.Equal("zh-CN", attribute.Culture);
        Assert.Equal("1.0.0", attribute.Version);
        Assert.True(attribute.Critical);
    }

    [Fact]
    public void AssemblyProviderDiscoversDescriptorsDeclaredByAssemblyAttributes()
    {
        var provider = new AssemblyLanguagePackageProvider();

        var descriptors = provider.Discover(typeof(LocalizationDeclarationAttributeTests).Assembly);

        var descriptor = Assert.Single(descriptors, descriptor => descriptor.PackageId == "Host.en-US");
        Assert.Equal(CultureInfo.GetCultureInfo("en-US"), descriptor.Culture);
        Assert.Equal(ResourceScope.Host, descriptor.Scope);
        Assert.Equal(LanguagePackageProviderKind.Assembly, descriptor.ProviderKind);
        Assert.Same(typeof(LocalizationDeclarationAttributeTests).Assembly, descriptor.SourceAssembly);
        Assert.Null(descriptor.Location);
        Assert.Equal("AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json", descriptor.ResourceBaseName);
        Assert.Equal(CultureInfo.GetCultureInfo("en"), descriptor.FallbackCulture);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Null(descriptor.Checksum);
        Assert.Equal("host.localization", descriptor.ContributionId);
    }

    [Fact]
    public async Task AssemblyProviderLoadsResourcesFromDiscoveredDescriptor()
    {
        var provider = new AssemblyLanguagePackageProvider();
        var descriptor = provider
            .Discover(typeof(LocalizationDeclarationAttributeTests).Assembly)
            .Single(descriptor => descriptor.PackageId == "Host.en-US");

        var result = await provider.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.True(result.Package.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings", value);
    }

    [Fact]
    public async Task AssemblyProviderReturnsPackageNotFoundForDiscoveredMissingResource()
    {
        var provider = new AssemblyLanguagePackageProvider();
        var descriptor = provider
            .Discover(typeof(LocalizationDeclarationAttributeTests).Assembly)
            .Single(descriptor => descriptor.PackageId == "Missing.en-US");

        var result = await provider.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageNotFound, result.Error?.Kind);
    }

    [Fact]
    public void RegistryRevokesDiscoveredAssemblyDescriptorsByOwner()
    {
        var registry = new LanguagePackageRegistry();
        var provider = new AssemblyLanguagePackageProvider();
        var descriptor = provider
            .Discover(typeof(LocalizationDeclarationAttributeTests).Assembly)
            .Single(descriptor => descriptor.PackageId == "Host.en-US");

        var registerResult = registry.Register(descriptor, "plugin.localization");
        var revokedCount = registry.RevokeOwner("plugin.localization");
        var registerAfterRevokeResult = registry.Register(descriptor, "plugin.localization");

        Assert.True(registerResult.Succeeded);
        Assert.Equal(1, revokedCount);
        Assert.Empty(registry.Registrations);
        Assert.False(registerAfterRevokeResult.Succeeded);
        Assert.Equal(LocalizationErrorKind.OwnerRevoked, registerAfterRevokeResult.Error?.Kind);
    }
}
