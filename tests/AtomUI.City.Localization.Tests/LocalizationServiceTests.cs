using System.Globalization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Localization.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public async Task SetCultureLoadsOnlySelectedCulturePackages()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var provider = new RecordingLanguagePackageProvider(zh, en);
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Equal("Settings zh", text.Value);
        Assert.Equal(["zh-CN"], provider.LoadedCultures);
        Assert.DoesNotContain("en-US", provider.LoadedCultures);
    }

    [Fact]
    public async Task CallerCancellationBeforeCultureCommitKeepsPreviousState()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var provider = new RecordingLanguagePackageProvider(zh);
        await using var service = new LocalizationService([zh.Descriptor], [provider]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.SetCultureAsync("zh-CN", cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.Cancelled, result.Error?.Kind);
        Assert.Equal(string.Empty, service.CurrentCulture.Name);
        Assert.Empty(provider.LoadedCultures);
    }

    [Fact]
    public async Task LookupLoadsFallbackPackageOnDemand()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var provider = new RecordingLanguagePackageProvider(zh, en);
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(["zh-CN", "en-US"], provider.LoadedCultures);
        Assert.Equal(["Host.zh-CN", "Host.en-US"], service.CultureState.Value.LoadedPackageIds);
        Assert.Equal(2, service.CultureRevision);
    }

    [Fact]
    public async Task CultureSwitchIncludesAlreadyCachedFallbackPackagesInState()
    {
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var frDescriptor = new LanguagePackageDescriptor(
            "Host.fr-FR",
            CultureInfo.GetCultureInfo("fr-FR"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var fr = LanguagePackage.Create(frDescriptor, new Dictionary<string, string>());
        await using var service = new LocalizationService(
            [zh.Descriptor, fr.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, fr, en)]);

        await service.SetCultureAsync("zh-CN");
        Assert.Equal("Settings", (await service.GetStringAsync("Settings.Title")).Value);

        await service.SetCultureAsync("fr-FR");

        Assert.Equal(["Host.fr-FR", "Host.en-US"], service.CultureState.Value.LoadedPackageIds);
    }

    [Fact]
    public async Task ConcurrentLookupsShareInFlightPackageLoad()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var provider = new BlockingLanguagePackageProvider(zh);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        var service = new LocalizationService(options, [provider]);

        var first = service.GetStringAsync("Settings.Title").AsTask();
        await provider.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.GetStringAsync("Settings.Title").AsTask();

        await Task.Delay(50);
        Assert.Equal(1, provider.LoadCount);

        provider.Release();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal("Settings zh", result.Value));
        Assert.Equal(1, provider.LoadCount);
    }

    [Fact]
    public async Task CancellingOneLookupDoesNotCancelSharedPackageLoad()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var provider = new BlockingLanguagePackageProvider(zh);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        await using var service = new LocalizationService(options, [provider]);
        using var cancellation = new CancellationTokenSource();

        var cancelledLookup = service.GetStringAsync("Settings.Title", cancellation.Token).AsTask();
        await provider.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var survivingLookup = service.GetStringAsync("Settings.Title").AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledLookup);
        Assert.Equal(1, provider.LoadCount);

        provider.Release();
        var result = await survivingLookup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Settings zh", result.Value);
        Assert.Equal(1, provider.LoadCount);
    }

    [Fact]
    public async Task ThrowingProviderBecomesLoadFailureAndDoesNotEscapeLookup()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        await using var service = new LocalizationService(
            options,
            [new ThrowingLanguagePackageProvider()],
            diagnostics: diagnostics);

        var result = await service.GetStringAsync("Settings.Title");

        Assert.True(result.IsMissing);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed
                && record.ErrorKind == LocalizationErrorKind.PackageLoadFailed);
    }

    [Fact]
    public async Task LookupLoadFailureFallsBackAndWritesDiagnostic()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var provider = new RecordingLanguagePackageProvider(zh, en)
        {
            FailingCultureName = "zh-CN",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        options.LanguagePackages.Add(en.Descriptor);
        var service = new LocalizationService(
            options,
            [provider],
            diagnostics: diagnostics);

        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed
                && record.PackageId == "Host.zh-CN"
                && record.CultureName == "zh-CN"
                && record.ErrorKind == LocalizationErrorKind.PackageLoadFailed
                && record.ProviderKind == LanguagePackageProviderKind.InMemory
                && !string.IsNullOrWhiteSpace(record.OperationId)
                && record.Attempt == 1
                && record.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task FallbackPackageCacheIsIsolatedByCulture()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.Shared",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var enDescriptor = new LanguagePackageDescriptor(
            "Host.Shared",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host);
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = LanguagePackage.Create(
            enDescriptor,
            new Dictionary<string, string>
            {
                ["Settings.Title"] = "Settings",
            });
        var provider = new CultureKeyedLanguagePackageProvider(zh, en);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        options.LanguagePackages.Add(en.Descriptor);
        var service = new LocalizationService(options, [provider]);

        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(["zh-CN", "en-US"], provider.LoadedCultures);
    }

    [Fact]
    public async Task LookupUsesScopePriorityBeforeHostFallback()
    {
        var host = Package(
            "Host.zh-CN",
            "zh-CN",
            ResourceScope.Host,
            ("Settings.Title", "Host Settings"),
            ("Shell.Title", "Host Shell"));
        var presentation = Package(
            "Presentation.zh-CN",
            "zh-CN",
            ResourceScope.Presentation,
            ("Settings.Title", "Framework Settings"));
        var module = Package(
            "SettingsModule.zh-CN",
            "zh-CN",
            ResourceScope.Module,
            ("Settings.Title", "Module Settings"));
        var service = new LocalizationService(
            [host.Descriptor, presentation.Descriptor, module.Descriptor],
            [new RecordingLanguagePackageProvider(host, presentation, module)],
            bridge: new RecordingPresentationLocalizationBridge());
        var context = new LocalizationLookupContext(moduleId: "test.module");
        using var scope = service.ActivateScope(context);

        await service.SetCultureAsync("zh-CN");
        var scopedText = await service.GetStringAsync("Settings.Title", context);
        var hostFallbackText = await service.GetStringAsync("Shell.Title");

        Assert.Equal("Module Settings", scopedText.Value);
        Assert.Equal("zh-CN", scopedText.Culture.Name);
        Assert.False(scopedText.IsFallback);
        Assert.Equal("Host Shell", hostFallbackText.Value);
    }

    [Fact]
    public async Task InactiveRouteIsNotLoadedAndLeaseControlsScopedLookup()
    {
        var host = Package(
            "Host.zh-CN",
            "zh-CN",
            ResourceScope.Host,
            ("Settings.Title", "Host Settings"));
        var route = Package(
            "Route.zh-CN",
            "zh-CN",
            ResourceScope.Route,
            ("Settings.Title", "Route Settings"));
        var provider = new RecordingLanguagePackageProvider(host, route);
        var service = new LocalizationService(
            [host.Descriptor, route.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());
        var context = new LocalizationLookupContext(routeId: "test.route");

        await service.SetCultureAsync("zh-CN");
        var globalText = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Host Settings", globalText.Value);
        Assert.Equal(["zh-CN"], provider.LoadedCultures);

        using (service.ActivateScope(context))
        {
            var routeText = await service.GetStringAsync("Settings.Title", context);
            Assert.Equal("Route Settings", routeText.Value);
        }

        var afterDeactivate = await service.GetStringAsync("Settings.Title", context);
        Assert.Equal("Host Settings", afterDeactivate.Value);
    }

    [Fact]
    public async Task MissingResourceReturnsMarkerAndDiagnostic()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Missing");

        Assert.Equal("!Settings.Missing!", text.Value);
        Assert.True(text.IsMissing);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.ResourceMissing
                && record.ResourceKey == "Settings.Missing"
                && record.CultureName == "zh-CN");
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.FallbackMissing
                && record.ResourceKey == "Settings.Missing"
                && !string.IsNullOrWhiteSpace(record.OperationId));
    }

    [Fact]
    public async Task CultureSwitchRollsBackWhenPackageLoadFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var ja = Package("Host.ja-JP", "ja-JP", ("Settings.Title", "Settings ja"));
        var provider = new RecordingLanguagePackageProvider(zh, ja)
        {
            FailingCultureName = "ja-JP",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, ja.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var result = await service.SetCultureAsync("ja-JP");

        Assert.False(result.Succeeded);
        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Contains(diagnostics.Records, record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.CultureName == "ja-JP"
                && record.ErrorKind == LocalizationErrorKind.PackageLoadFailed);
    }

    [Fact]
    public async Task CultureSwitchRejectsPackageMissingCriticalResource()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            CriticalResourceKeys = ["Settings.Title"],
        };
        var package = LanguagePackage.Create(descriptor, new Dictionary<string, string>());
        var diagnostics = new InMemoryLocalizationDiagnostics();
        await using var service = new LocalizationService(
            [descriptor],
            [new RecordingLanguagePackageProvider(package)],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidResource, result.Error?.Kind);
        Assert.Equal(string.Empty, service.CurrentCulture.Name);
        Assert.True(package.IsDisposed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.ErrorKind == LocalizationErrorKind.InvalidResource);
    }

    [Fact]
    public async Task CultureSwitchCommitsStateAndRefreshesTextsWhenPresentationBridgeFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var ja = Package("Host.ja-JP", "ja-JP", ("Settings.Title", "Settings ja"));
        var bridge = new RecordingPresentationLocalizationBridge
        {
            FailingCultureName = "ja-JP",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, ja.Descriptor],
            [new RecordingLanguagePackageProvider(zh, ja)],
            bridge: bridge,
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateTextAsync("Settings.Title");
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        var result = await service.SetCultureAsync("ja-JP");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PresentationApplyFailed, result.Error?.Kind);
        Assert.Equal("ja-JP", service.CurrentCulture.Name);
        Assert.Equal(2, service.CultureRevision);
        Assert.Equal("Settings ja", text.Value);
        Assert.Equal(["Settings ja"], changes);
        Assert.Contains(diagnostics.Records, record => record.Code == LocalizationDiagnosticIds.AtomUiApplyFailed);
    }

    [Fact]
    public async Task ReentrantCultureSwitchFromBridgeFailsFastWithoutDeadlock()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var bridge = new ReentrantPresentationLocalizationBridge();
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge);
        bridge.Service = service;

        var result = await service.SetCultureAsync("zh-CN").AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.NotNull(bridge.ReentrantResult);
        Assert.False(bridge.ReentrantResult.Succeeded);
        Assert.Equal(LocalizationErrorKind.ReentrantOperation, bridge.ReentrantResult.Error?.Kind);
        Assert.Equal("zh-CN", service.CurrentCulture.Name);
    }

    [Fact]
    public async Task MutationStartedByChildTaskAfterCallbackCompletesIsNotRejectedAsReentrant()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var bridge = new DeferredMutationPresentationLocalizationBridge();
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge);
        bridge.Service = service;

        Assert.True((await service.SetCultureAsync("zh-CN")).Succeeded);
        bridge.ReleaseChildMutation();
        var childResult = await bridge.ChildMutation!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(childResult.Succeeded);
        Assert.Equal("en-US", service.CurrentCulture.Name);
    }

    [Fact]
    public async Task ThrowingPresentationBridgeReturnsFailureAndStillRefreshesTexts()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            new ThrowingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateTextAsync("Settings.Title");

        var result = await service.SetCultureAsync("en-US");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PresentationApplyFailed, result.Error?.Kind);
        Assert.IsType<InvalidOperationException>(result.Error?.Exception);
        Assert.Equal("Settings en", text.Value);
    }

    [Fact]
    public async Task SwitchingBackToCachedCultureDoesNotReloadOrDisposeActivePackage()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var provider = new RecordingLanguagePackageProvider(zh, en);
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [provider],
            new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        await service.SetCultureAsync("en-US");
        await service.SetCultureAsync("zh-CN");

        Assert.Equal(["zh-CN", "en-US"], provider.LoadedCultures);
        Assert.False(zh.IsDisposed);
        Assert.Equal("Settings zh", (await service.GetStringAsync("Settings.Title")).Value);
    }

    [Fact]
    public async Task SuccessfulCultureSwitchAppliesPresentationBridge()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var bridge = new RecordingPresentationLocalizationBridge();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: bridge);

        await service.SetCultureAsync("zh-CN");

        Assert.Single(bridge.AppliedCultures);
        Assert.Equal("zh-CN", bridge.AppliedCultures.Single());
        Assert.Equal(1, service.CultureRevision);
    }

    [Fact]
    public async Task SuccessfulCultureSwitchSendsLoadedPackageBatchToPresentationBridge()
    {
        var host = Package("Host.zh-CN", "zh-CN", ResourceScope.Host, ("Shell.Title", "Shell"));
        var module = Package("Module.zh-CN", "zh-CN", ResourceScope.Module, ("Settings.Title", "Settings"));
        var bridge = new RecordingPresentationLocalizationBridge();
        var service = new LocalizationService(
            [host.Descriptor, module.Descriptor],
            [new RecordingLanguagePackageProvider(host, module)],
            bridge: bridge);
        using var scope = service.ActivateScope(
            new LocalizationLookupContext(moduleId: "test.module"));

        await service.SetCultureAsync("zh-CN");

        var state = Assert.Single(bridge.AppliedStates);
        Assert.Equal("zh-CN", state.CurrentCulture.Name);
        Assert.Equal(1, state.Revision);
        Assert.Equal(["Module.zh-CN", "Host.zh-CN"], state.LoadedPackageIds);
    }

    [Fact]
    public void LocalizationPresentationBridgeContractDoesNotReferenceAvaloniaTypes()
    {
        var references = typeof(IPresentationLocalizationBridge).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task RevokePackagesByContributionIdRemovesPluginResourcesAndKeepsOldStateSnapshotStable()
    {
        var plugin = Package(
            "Plugin.zh-CN",
            "zh-CN",
            ResourceScope.Plugin,
            contributionId: "plugin.settings.localization",
            ("Settings.Title", "Plugin Settings"));
        var host = Package(
            "Host.zh-CN",
            "zh-CN",
            ResourceScope.Host,
            contributionId: null,
            ("Settings.Title", "Host Settings"));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [plugin.Descriptor, host.Descriptor],
            [new RecordingLanguagePackageProvider(plugin, host)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);
        var context = new LocalizationLookupContext(pluginId: "test.plugin");
        using var scope = service.ActivateScope(context);

        await service.SetCultureAsync("zh-CN");
        var oldState = service.CultureState.Value;
        var beforeRevoke = await service.GetStringAsync("Settings.Title", context);

        var revokedCount = await service.RevokePackagesByContributionIdAsync("plugin.settings.localization");
        var afterRevoke = await service.GetStringAsync("Settings.Title");
        var secondRevokeCount = await service.RevokePackagesByContributionIdAsync("plugin.settings.localization");

        Assert.Equal("Plugin Settings", beforeRevoke.Value);
        Assert.Equal(1, revokedCount);
        Assert.Equal(0, secondRevokeCount);
        Assert.True(plugin.IsDisposed);
        Assert.Equal("Host Settings", afterRevoke.Value);
        Assert.DoesNotContain(
            service.CultureState.Value.LoadedPackageIds,
            packageId => packageId == "Plugin.zh-CN");
        Assert.Contains(oldState.LoadedPackageIds, packageId => packageId == "Plugin.zh-CN");
        Assert.Equal(2, service.CultureRevision);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.PluginPackagesRevoked
                && record.ContributionId == "plugin.settings.localization"
                && record.RevokedPackageCount == 1
                && record.ErrorKind == LocalizationErrorKind.ResourceRevoked);
    }

    [Fact]
    public async Task RevokePackagesByContributionIdRefreshesActiveLocalizedTexts()
    {
        var plugin = Package(
            "Plugin.zh-CN",
            "zh-CN",
            ResourceScope.Plugin,
            contributionId: "plugin.settings.localization",
            ("Settings.Title", "Plugin Settings"));
        var host = Package(
            "Host.zh-CN",
            "zh-CN",
            ResourceScope.Host,
            contributionId: null,
            ("Settings.Title", "Host Settings"));
        var service = new LocalizationService(
            [plugin.Descriptor, host.Descriptor],
            [new RecordingLanguagePackageProvider(plugin, host)],
            bridge: new RecordingPresentationLocalizationBridge());
        var context = new LocalizationLookupContext(pluginId: "test.plugin");
        using var scope = service.ActivateScope(context);

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateTextAsync("Settings.Title", context);
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        await service.RevokePackagesByContributionIdAsync("plugin.settings.localization");

        Assert.Equal("Host Settings", text.Value);
        Assert.Equal(["Host Settings"], changes);
    }

    [Fact]
    public async Task LookupCompletingAfterContributionRevokeCannotPublishRevokedPackage()
    {
        var plugin = Package(
            "Plugin.zh-CN",
            "zh-CN",
            ResourceScope.Plugin,
            contributionId: "plugin.settings.localization",
            ("Settings.Title", "Plugin Settings"));
        var host = Package(
            "Host.zh-CN",
            "zh-CN",
            ResourceScope.Host,
            contributionId: null,
            ("Settings.Title", "Host Settings"));
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(plugin.Descriptor);
        options.LanguagePackages.Add(host.Descriptor);
        var provider = new BlockingContributionLanguagePackageProvider(
            blockedPackageId: "Plugin.zh-CN",
            plugin,
            host);
        var service = new LocalizationService(
            options,
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());
        var context = new LocalizationLookupContext(pluginId: "test.plugin");
        using var scope = service.ActivateScope(context);

        var lookup = service.GetStringAsync("Settings.Title", context).AsTask();
        await provider.BlockedLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await service.RevokePackagesByContributionIdAsync("plugin.settings.localization");
        provider.ReleaseBlockedLoad();
        var oldSnapshotResult = await lookup;
        var nextLookupResult = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Host Settings", oldSnapshotResult.Value);
        Assert.Equal("Host Settings", nextLookupResult.Value);
        Assert.True(plugin.IsDisposed);
    }

    [Fact]
    public async Task GetMessageAsyncFormatsMessageWithCurrentCulture()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Errors.Upload.Size", "文件大小不能超过 {0:N1} MB"));
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Upload.Size", [12.345]);

        Assert.Equal("文件大小不能超过 12.3 MB", message.Value);
        Assert.Equal("Errors.Upload.Size", message.Key);
        Assert.Equal("zh-CN", message.Culture.Name);
        Assert.False(message.IsMissing);
        Assert.False(message.IsFormatFailed);
    }

    [Fact]
    public async Task GetMessageAsyncReturnsMissingMarkerWhenMessageKeyIsMissing()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Missing", ["value"]);

        Assert.Equal("!Errors.Missing!", message.Value);
        Assert.True(message.IsMissing);
        Assert.False(message.IsFormatFailed);
    }

    [Fact]
    public async Task GetMessageAsyncReturnsTemplateAndDiagnosticWhenFormatFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Errors.Range", "Value must be between {0} and {1}."));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Range", [1]);

        Assert.Equal("Value must be between {0} and {1}.", message.Value);
        Assert.True(message.IsFormatFailed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.MessageFormatFailed
                && record.ResourceKey == "Errors.Range"
                && record.ErrorKind == LocalizationErrorKind.FormatFailed);
    }

    [Fact]
    public async Task LocalizedTextRefreshesWhenCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateTextAsync("Settings.Title");
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Settings", text.Value);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(2, text.Revision);
        Assert.Equal(["Settings"], changes);
    }

    [Fact]
    public async Task CallerCancellationAfterCultureCommitDoesNotLeaveTextsPartiallyRefreshed()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)]);
        Assert.True((await service.SetCultureAsync("zh-CN")).Succeeded);
        using var first = await service.CreateTextAsync("Settings.Title");
        using var second = await service.CreateTextAsync("Settings.Title");
        using var cancellation = new CancellationTokenSource();
        first.Changed += (_, _) => cancellation.Cancel();

        var result = await service.SetCultureAsync("en-US", cancellation.Token);

        Assert.True(result.Succeeded);
        Assert.Equal("Settings en", second.Value);
    }

    [Fact]
    public async Task LocalizedMessageTextRefreshesFormattedValueWhenCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Validation.Length", "字段 {0} 不能超过 {1} 个字符"));
        var en = Package("Host.en-US", "en-US", ("Validation.Length", "{0} must be at most {1} characters."));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateMessageTextAsync("Validation.Length", ["Name", 12]);
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Name must be at most 12 characters.", text.Value);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(2, text.Revision);
        Assert.Equal(["Name must be at most 12 characters."], changes);
    }

    [Fact]
    public async Task ScopedLookupUsesFallbackDeclaredByNewlyActivatedScope()
    {
        var enDescriptor = new LanguagePackageDescriptor(
            "Route.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Route)
        {
            ScopeId = "reports",
            FallbackCulture = CultureInfo.GetCultureInfo("fr-FR"),
        };
        var frDescriptor = new LanguagePackageDescriptor(
            "Route.fr-FR",
            CultureInfo.GetCultureInfo("fr-FR"),
            ResourceScope.Route)
        {
            ScopeId = "reports",
        };
        var en = LanguagePackage.Create(enDescriptor, new Dictionary<string, string>());
        var fr = LanguagePackage.Create(
            frDescriptor,
            new Dictionary<string, string> { ["Reports.Title"] = "Rapports" });
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        options.LanguagePackages.Add(enDescriptor);
        options.LanguagePackages.Add(frDescriptor);
        await using var service = new LocalizationService(
            options,
            [new RecordingLanguagePackageProvider(en, fr)]);
        var context = new LocalizationLookupContext(routeId: "reports");
        using var scope = service.ActivateScope(context);

        var result = await service.GetStringAsync("Reports.Title", context);

        Assert.Equal("Rapports", result.Value);
        Assert.True(result.IsFallback);
        Assert.Equal("fr-FR", result.Culture.Name);
    }

    [Fact]
    public async Task LookupFallbackIsNotPollutedByAnotherActiveScope()
    {
        var routeAEn = ScopedPackage(
            "RouteA.en-US",
            "en-US",
            ResourceScope.Route,
            "route-a",
            fallbackCulture: "fr-FR");
        var routeAFr = ScopedPackage(
            "RouteA.fr-FR",
            "fr-FR",
            ResourceScope.Route,
            "route-a");
        var routeBEn = ScopedPackage(
            "RouteB.en-US",
            "en-US",
            ResourceScope.Route,
            "route-b",
            fallbackCulture: "de-DE");
        var routeBFr = ScopedPackage(
            "RouteB.fr-FR",
            "fr-FR",
            ResourceScope.Route,
            "route-b",
            resources: [("Reports.Title", "Rapports incorrects")]);
        var routeBDe = ScopedPackage(
            "RouteB.de-DE",
            "de-DE",
            ResourceScope.Route,
            "route-b",
            resources: [("Reports.Title", "Berichte")]);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        foreach (var package in new[] { routeAEn, routeAFr, routeBEn, routeBFr, routeBDe })
        {
            options.LanguagePackages.Add(package.Descriptor);
        }

        await using var service = new LocalizationService(
            options,
            [new RecordingLanguagePackageProvider(routeAEn, routeAFr, routeBEn, routeBFr, routeBDe)]);
        var routeAContext = new LocalizationLookupContext(routeId: "route-a");
        var routeBContext = new LocalizationLookupContext(routeId: "route-b");
        using var routeAScope = service.ActivateScope(routeAContext);
        using var routeBScope = service.ActivateScope(routeBContext);
        Assert.True((await service.SetCultureAsync("en-US")).Succeeded);

        var result = await service.GetStringAsync("Reports.Title", routeBContext);

        Assert.Equal("Berichte", result.Value);
        Assert.Equal("de-DE", result.Culture.Name);
    }

    [Fact]
    public async Task FormatterExceptionReturnsRawTemplateAndWritesDiagnostic()
    {
        var en = Package("Host.en-US", "en-US", ("Errors.Value", "Value: {0}"));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        options.LanguagePackages.Add(en.Descriptor);
        await using var service = new LocalizationService(
            options,
            [new RecordingLanguagePackageProvider(en)],
            diagnostics: diagnostics);

        var result = await service.GetMessageAsync("Errors.Value", [new ThrowingFormattable()]);

        Assert.Equal("Value: {0}", result.Value);
        Assert.True(result.IsFormatFailed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.MessageFormatFailed
                && record.ErrorKind == LocalizationErrorKind.FormatFailed);
    }

    [Fact]
    public async Task CallerCancellationAfterRevocationCommitDoesNotInterruptTextRefresh()
    {
        using var cancellation = new CancellationTokenSource();
        var descriptor = new LanguagePackageDescriptor(
            "Plugin.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Plugin)
        {
            ScopeId = "sales",
            ContributionId = "sales.localization",
        };
        var package = LanguagePackage.Create(
            descriptor,
            new Dictionary<string, string> { ["Sales.Title"] = "Sales" });
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        options.LanguagePackages.Add(descriptor);
        await using var service = new LocalizationService(
            options,
            [new RecordingLanguagePackageProvider(package)],
            bridge: new CancellingPresentationLocalizationBridge(cancellation));
        var context = new LocalizationLookupContext(pluginId: "sales");
        using var scope = service.ActivateScope(context);
        using var text = await service.CreateTextAsync("Sales.Title", context);

        var revoked = await service.RevokePackagesByContributionIdAsync(
            "sales.localization",
            cancellation.Token);

        Assert.Equal(1, revoked);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(text.IsMissing);
    }

    [Fact]
    public async Task DisposedLocalizedMessageTextDoesNotRefreshAfterCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Validation.Required", "请输入 {0}"));
        var en = Package("Host.en-US", "en-US", ("Validation.Required", "{0} is required."));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateMessageTextAsync("Validation.Required", ["Name"]);
        var changed = false;
        text.Changed += (_, _) => changed = true;

        text.Dispose();
        await service.SetCultureAsync("en-US");

        Assert.False(changed);
        Assert.Equal("请输入 Name", text.Value);
        Assert.Equal(1, text.Revision);
    }

    [Fact]
    public async Task DisposedLocalizedTextDoesNotRefreshAfterCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateTextAsync("Settings.Title");
        var changed = false;
        text.Changed += (_, _) => changed = true;

        text.Dispose();
        await service.SetCultureAsync("en-US");

        Assert.False(changed);
        Assert.Equal("设置", text.Value);
        Assert.Equal(1, text.Revision);
    }

    [Fact]
    public async Task LocalizedTextDisposeWaitsForInFlightNotificationAndPreventsLaterCallbacks()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateTextAsync("Settings.Title");
        var notificationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationCount = 0;
        text.Changed += (_, _) =>
        {
            Interlocked.Increment(ref notificationCount);
            notificationStarted.TrySetResult();
            releaseNotification.Task.GetAwaiter().GetResult();
        };

        var cultureSwitch = service.SetCultureAsync("en-US").AsTask();
        await notificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispose = Task.Run(text.Dispose);
        await Task.Delay(50);

        Assert.False(dispose.IsCompleted);

        releaseNotification.TrySetResult();
        await Task.WhenAll(cultureSwitch, dispose).WaitAsync(TimeSpan.FromSeconds(5));
        await text.RefreshAsync();

        Assert.Equal(1, Volatile.Read(ref notificationCount));
    }

    [Fact]
    public async Task LocalizedTextCanDisposeItselfFromChangedHandlerWithoutDeadlock()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        await using var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateTextAsync("Settings.Title");
        text.Changed += (_, _) => text.Dispose();

        var result = await service.SetCultureAsync("en-US").AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        await text.RefreshAsync();
    }

    [Fact]
    public async Task ChildTaskCanRefreshTextAfterOriginatingNotificationCompletes()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var plugin = LanguagePackage.Create(
            new LanguagePackageDescriptor(
                "Plugin.en-US",
                CultureInfo.GetCultureInfo("en-US"),
                ResourceScope.Plugin)
            {
                ScopeId = "test.plugin",
            },
            new Dictionary<string, string>
            {
                ["Settings.Title"] = "Plugin Settings",
            });
        var provider = new RecordingLanguagePackageProvider(zh, en, plugin);
        var services = new ServiceCollection();
        services.AddSingleton<ILanguagePackageProvider>(provider);
        services.AddLocalization(options =>
        {
            options.LanguagePackages.Add(zh.Descriptor);
            options.LanguagePackages.Add(en.Descriptor);
        });
        await using var serviceProvider = services.BuildServiceProvider();
        var service = (LocalizationService)serviceProvider.GetRequiredService<ILocalizationService>();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();
        var context = new LocalizationLookupContext(pluginId: "test.plugin");
        using var scope = service.ActivateScope(context);
        Assert.True((await service.SetCultureAsync("zh-CN")).Succeeded);
        using var text = await service.CreateTextAsync("Settings.Title", context);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? childRefresh = null;
        text.Changed += (_, args) =>
        {
            if (args.Value == "Settings en")
            {
                childRefresh = Task.Run(async () =>
                {
                    await releaseRefresh.Task;
                    await text.RefreshAsync();
                });
            }
        };

        Assert.True((await service.SetCultureAsync("en-US")).Succeeded);
        Assert.NotNull(childRefresh);
        Assert.True(registry.Register(plugin.Descriptor, "plugin").Succeeded);
        releaseRefresh.TrySetResult();
        await childRefresh!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Plugin Settings", text.Value);
    }

    [Fact]
    public async Task LocalizedTextRefreshFailureIsDiagnosticAndDoesNotStopOtherSubscribers()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        using var throwingText = await service.CreateTextAsync("Settings.Title");
        using var secondText = await service.CreateTextAsync("Settings.Title");
        var secondChanged = false;
        throwingText.Changed += (_, _) => throw new InvalidOperationException("refresh failed");
        secondText.Changed += (_, _) => secondChanged = true;

        var result = await service.SetCultureAsync("en-US");

        Assert.True(result.Succeeded);
        Assert.True(secondChanged);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.TextRefreshFailed
                && record.ResourceKey == "Settings.Title"
                && record.ErrorKind == LocalizationErrorKind.RefreshFailed);
    }

    [Fact]
    public async Task DisposeAsyncDisposesLoadedPackagesAndTrackedTexts()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateTextAsync("Settings.Title");
        await service.DisposeAsync();

        Assert.True(zh.IsDisposed);
        await text.RefreshAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.GetStringAsync("Settings.Title").AsTask());
    }

    [Fact]
    public async Task ConcurrentDisposeCallsShareCompletionAndWaitForIgnoringProvider()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var provider = new NonCancellingBlockingLanguagePackageProvider(zh);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        var service = new LocalizationService(options, [provider]);
        var lookup = service.GetStringAsync("Settings.Title").AsTask();
        await provider.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var firstDispose = service.DisposeAsync().AsTask();
        var secondDispose = Task.Run(service.Dispose);
        await Task.Delay(50);

        Assert.False(firstDispose.IsCompleted);
        Assert.False(secondDispose.IsCompleted);

        provider.Release();
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => lookup.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(zh.IsDisposed);
    }

    private static LanguagePackage ScopedPackage(
        string packageId,
        string cultureName,
        ResourceScope scope,
        string scopeId,
        string? fallbackCulture = null,
        params (string Key, string Value)[] resources)
    {
        return LanguagePackage.Create(
            new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(cultureName),
                scope)
            {
                ScopeId = scopeId,
                FallbackCulture = fallbackCulture is null
                    ? null
                    : CultureInfo.GetCultureInfo(fallbackCulture),
            },
            resources.ToDictionary(resource => resource.Key, resource => resource.Value));
    }

    private static LanguagePackage Package(
        string packageId,
        string cultureName,
        params (string Key, string Value)[] resources)
    {
        return Package(packageId, cultureName, ResourceScope.Host, resources);
    }

    private static LanguagePackage Package(
        string packageId,
        string cultureName,
        ResourceScope scope,
        params (string Key, string Value)[] resources)
    {
        return LanguagePackage.Create(
            new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(cultureName),
                scope)
            {
                ScopeId = GetTestScopeId(scope),
            },
            resources.ToDictionary(resource => resource.Key, resource => resource.Value));
    }

    private static LanguagePackage Package(
        string packageId,
        string cultureName,
        ResourceScope scope,
        string? contributionId,
        params (string Key, string Value)[] resources)
    {
        return LanguagePackage.Create(
            new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(cultureName),
                scope)
            {
                ScopeId = GetTestScopeId(scope),
                ContributionId = contributionId,
            },
            resources.ToDictionary(resource => resource.Key, resource => resource.Value));
    }

    private static string? GetTestScopeId(ResourceScope scope)
    {
        return scope switch
        {
            ResourceScope.Module => "test.module",
            ResourceScope.Plugin => "test.plugin",
            ResourceScope.Route => "test.route",
            ResourceScope.Window => "test.window",
            _ => null,
        };
    }

    private sealed class ReentrantPresentationLocalizationBridge : IPresentationLocalizationBridge
    {
        public ILocalizationService? Service { get; set; }

        public LocalizationResult? ReentrantResult { get; private set; }

        public async ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            ReentrantResult = await Service!.SetCultureAsync("en-US", cancellationToken);

            return LocalizationResult.Success();
        }
    }

    private sealed class DeferredMutationPresentationLocalizationBridge : IPresentationLocalizationBridge
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public ILocalizationService? Service { get; set; }

        public Task<LocalizationResult>? ChildMutation { get; private set; }

        public ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            if (state.CurrentCulture.Name == "zh-CN"
                && Interlocked.Exchange(ref _started, 1) == 0)
            {
                ChildMutation = Task.Run(async () =>
                {
                    await _release.Task;
                    return await Service!.SetCultureAsync("en-US");
                });
            }

            return ValueTask.FromResult(LocalizationResult.Success());
        }

        public void ReleaseChildMutation() => _release.TrySetResult();
    }

    private sealed class ThrowingPresentationLocalizationBridge : IPresentationLocalizationBridge
    {
        public ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("bridge failed");
        }
    }

    private sealed class CancellingPresentationLocalizationBridge(
        CancellationTokenSource cancellation) : IPresentationLocalizationBridge
    {
        public ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();

            return ValueTask.FromResult(LocalizationResult.Success());
        }
    }

    private sealed class ThrowingFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            throw new InvalidOperationException("Formatting failed.");
        }

        public override string ToString()
        {
            throw new InvalidOperationException("Formatting failed.");
        }
    }

    private sealed class ThrowingLanguagePackageProvider : ILanguagePackageProvider
    {
        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider failed");
        }
    }

    private sealed class BlockingContributionLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly string _blockedPackageId;
        private readonly Dictionary<string, LanguagePackage> _packages;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingContributionLanguagePackageProvider(
            string blockedPackageId,
            params LanguagePackage[] packages)
        {
            _blockedPackageId = blockedPackageId;
            _packages = packages.ToDictionary(package => package.Descriptor.PackageId, StringComparer.Ordinal);
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public TaskCompletionSource BlockedLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseBlockedLoad()
        {
            _release.TrySetResult();
        }

        public async ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            if (descriptor.PackageId == _blockedPackageId)
            {
                BlockedLoadStarted.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return _packages.TryGetValue(descriptor.PackageId, out var package)
                ? LanguagePackageLoadResult.Success(package)
                : LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Package was not found."));
        }
    }

    private sealed class BlockingLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly LanguagePackage _package;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingLanguagePackageProvider(LanguagePackage package)
        {
            _package = package;
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int LoadCount { get; private set; }

        public void Release()
        {
            _release.TrySetResult();
        }

        public async ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            FirstLoadStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            return LanguagePackageLoadResult.Success(_package);
        }
    }

    private sealed class NonCancellingBlockingLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly LanguagePackage _package;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NonCancellingBlockingLanguagePackageProvider(LanguagePackage package)
        {
            _package = package;
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public TaskCompletionSource LoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public async ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            LoadStarted.TrySetResult();
            await _release.Task;
            return LanguagePackageLoadResult.Success(_package);
        }
    }

    private sealed class CultureKeyedLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly Dictionary<string, LanguagePackage> _packages;

        public CultureKeyedLanguagePackageProvider(params LanguagePackage[] packages)
        {
            _packages = packages.ToDictionary(
                package => GetKey(package.Descriptor),
                StringComparer.Ordinal);
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public List<string> LoadedCultures { get; } = [];

        public ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            LoadedCultures.Add(descriptor.Culture.Name);

            return _packages.TryGetValue(GetKey(descriptor), out var package)
                ? ValueTask.FromResult(LanguagePackageLoadResult.Success(package))
                : ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            "Package was not found.")));
        }

        private static string GetKey(LanguagePackageDescriptor descriptor)
        {
            return $"{descriptor.Culture.Name}|{descriptor.PackageId}";
        }
    }
}
