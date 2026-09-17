using System.Globalization;
using AtomUI.City.Localization;

namespace AtomUI.City.Localization.Tests;

public sealed class CultureStateTests
{
    [Fact]
    public async Task CultureStateIsPublishedThroughCityStateContract()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        await using var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)]);
        var published = new List<CultureState>();
        using var subscription = service.CultureState.OnChange(
            change => published.Add(change.NewValue));

        await service.SetCultureAsync("zh-CN");

        var state = Assert.Single(published);
        Assert.Same(service.CultureState.Value, state);
        Assert.Equal("zh-CN", service.CultureState.Value.CurrentCulture.Name);
        Assert.Equal(1, service.CultureState.Version);
    }

    [Fact]
    public void LocalizationOptionsDefaultCultureInitializesServiceState()
    {
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        options.FallbackCultures.Add(CultureInfo.GetCultureInfo("ja-JP"));
        var service = new LocalizationService(options, []);

        Assert.Equal("en-US", service.CurrentCulture.Name);
        Assert.Equal("en-US", service.CultureState.Value.CurrentUICulture.Name);
        Assert.Equal(0, service.CultureRevision);
        Assert.Equal(
            ["ja-JP", "en", ""],
            service.CultureState.Value.FallbackCultures.Select(culture => culture.Name).ToArray());
        Assert.Empty(service.CultureState.Value.LoadedPackageIds);
    }

    [Fact]
    public async Task SetCulturePublishesFallbackChainInStableOrder()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["en-US", "zh-Hans", "zh", ""],
            service.CultureState.Value.FallbackCultures.Select(culture => culture.Name).ToArray());
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureChanged
                && record.CultureName == "zh-CN"
                && record.FallbackCultureName == "en-US;zh-Hans;zh;");
    }

    [Fact]
    public async Task SetCultureRejectsInvalidCultureWithoutChangingState()
    {
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var service = new LocalizationService(
            options,
            [],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("not a culture");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidCulture, result.Error?.Kind);
        Assert.Equal("en-US", service.CurrentCulture.Name);
        Assert.Equal(0, service.CultureRevision);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.CultureName == "not a culture"
                && record.ErrorKind == LocalizationErrorKind.InvalidCulture);
    }

    [Fact]
    public async Task SetCultureSkipsRepeatedCultureWithoutReloadingPackages()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var provider = new RecordingLanguagePackageProvider(zh);
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [provider],
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var revision = service.CultureRevision;
        var result = await service.SetCultureAsync("zh-CN");

        Assert.True(result.Succeeded);
        Assert.Equal(revision, service.CultureRevision);
        Assert.Equal(["zh-CN"], provider.LoadedCultures);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchSkipped
                && record.CultureName == "zh-CN");
    }

    [Fact]
    public async Task SetCultureRejectsFallbackCycleWithoutChangingState()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidCulture, result.Error?.Kind);
        Assert.Equal(CultureInfo.InvariantCulture, service.CurrentCulture);
        Assert.Equal(0, service.CultureRevision);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.CultureName == "zh-CN"
                && record.FallbackCultureName == "zh-CN");
    }

    [Fact]
    public async Task SetCultureExpandsMultiNodeFallbackGraph()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var enDescriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("fr-FR"),
        };
        var frDescriptor = new LanguagePackageDescriptor(
            "Host.fr-FR",
            CultureInfo.GetCultureInfo("fr-FR"),
            ResourceScope.Host);
        var packages = new[]
        {
            LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>()),
            LanguagePackage.Create(enDescriptor, new Dictionary<string, string>()),
            LanguagePackage.Create(frDescriptor, new Dictionary<string, string>()),
        };
        await using var service = new LocalizationService(
            [zhDescriptor, enDescriptor, frDescriptor],
            [new RecordingLanguagePackageProvider(packages)]);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["en-US", "fr-FR", "zh-Hans", "zh", ""],
            service.CultureState.Value.FallbackCultures.Select(culture => culture.Name));
    }

    [Fact]
    public async Task SetCultureRejectsMultiNodeFallbackCycle()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var enDescriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        var packages = new[]
        {
            LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>()),
            LanguagePackage.Create(enDescriptor, new Dictionary<string, string>()),
        };
        await using var service = new LocalizationService(
            [zhDescriptor, enDescriptor],
            [new RecordingLanguagePackageProvider(packages)]);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidCulture, result.Error?.Kind);
        Assert.Equal(string.Empty, service.CurrentCulture.Name);
    }

    [Fact]
    public void CollectionsRejectExternalListMutation()
    {
        var state = new CultureState(
            CultureInfo.GetCultureInfo("zh-CN"),
            CultureInfo.GetCultureInfo("zh-CN"),
            [CultureInfo.GetCultureInfo("en-US")],
            revision: 1,
            ["Host.zh-CN"]);
        var fallbackCultures = Assert.IsAssignableFrom<IList<CultureInfo>>(state.FallbackCultures);
        var loadedPackageIds = Assert.IsAssignableFrom<IList<string>>(state.LoadedPackageIds);

        Assert.Throws<NotSupportedException>(() => fallbackCultures[0] = CultureInfo.GetCultureInfo("ja-JP"));
        Assert.Throws<NotSupportedException>(() => loadedPackageIds[0] = "Changed");
        Assert.Equal("en-US", state.FallbackCultures[0].Name);
        Assert.Equal("Host.zh-CN", state.LoadedPackageIds[0]);
    }

    [Fact]
    public void CultureStateTakesDeepReadonlyCultureSnapshots()
    {
        var current = new CultureInfo("en-US");
        var fallback = new CultureInfo("fr-FR");
        var originalPattern = current.DateTimeFormat.ShortDatePattern;
        var state = new CultureState(
            current,
            current,
            [fallback],
            revision: 0,
            loadedPackageIds: []);

        current.DateTimeFormat.ShortDatePattern = "yyyy";
        fallback.DateTimeFormat.ShortDatePattern = "MM";

        Assert.True(state.CurrentCulture.IsReadOnly);
        Assert.True(state.CurrentUICulture.IsReadOnly);
        Assert.True(state.FallbackCultures[0].IsReadOnly);
        Assert.Equal(originalPattern, state.CurrentCulture.DateTimeFormat.ShortDatePattern);
        Assert.Throws<InvalidOperationException>(
            () => state.FallbackCultures[0].DateTimeFormat.ShortDatePattern = "dd");
    }

    [Fact]
    public void CultureStateRejectsInvalidCollectionInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new CultureState(
            CultureInfo.InvariantCulture,
            CultureInfo.InvariantCulture,
            null!,
            revision: 0,
            loadedPackageIds: []));
        Assert.Throws<ArgumentException>(() => new CultureState(
            CultureInfo.InvariantCulture,
            CultureInfo.InvariantCulture,
            [null!],
            revision: 0,
            loadedPackageIds: []));
        Assert.Throws<ArgumentException>(() => new CultureState(
            CultureInfo.InvariantCulture,
            CultureInfo.InvariantCulture,
            [],
            revision: 0,
            loadedPackageIds: [" "]));
    }

    private static LanguagePackage Package(
        string packageId,
        string cultureName,
        params (string Key, string Value)[] resources)
    {
        return LanguagePackage.Create(
            new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(cultureName),
                ResourceScope.Host),
            resources.ToDictionary(resource => resource.Key, resource => resource.Value));
    }
}
