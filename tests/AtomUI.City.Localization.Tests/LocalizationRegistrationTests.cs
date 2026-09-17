using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Localization.Tests;

public sealed class LocalizationRegistrationTests
{
    [Fact]
    public void LanguagePackageRegistrationCanOnlyBeCreatedByTheRegistry()
    {
        Assert.Empty(typeof(LanguagePackageRegistration).GetConstructors());

        var constructor = Assert.Single(typeof(LanguagePackageRegistration).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

        Assert.True(constructor.IsAssembly);
    }

    [Fact]
    public void RegistryRejectsScopedDescriptorWithoutScopeId()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = new LanguagePackageDescriptor(
            "Settings.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Module);

        var result = registry.Register(descriptor, "settings.module");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, result.Error?.Kind);
        Assert.Empty(registry.Registrations);
    }

    [Fact]
    public async Task DefaultInMemoryProviderLoadsDescriptorResources()
    {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.LanguagePackages.Add(
            new LanguagePackageDescriptor(
                "Host.zh-CN",
                CultureInfo.GetCultureInfo("zh-CN"),
                ResourceScope.Host)
            {
                InMemoryResources = new Dictionary<string, string>
                {
                    ["Settings.Title"] = "Settings",
                },
            }));

        await using var serviceProvider = services.BuildServiceProvider();
        var localization = serviceProvider.GetRequiredService<ILocalizationService>();

        Assert.True((await localization.SetCultureAsync("zh-CN")).Succeeded);
        Assert.Equal("Settings", (await localization.GetStringAsync("Settings.Title")).Value);
    }

    [Fact]
    public void DuplicateCustomProviderKindsAreRejectedWithClearError()
    {
        var exception = Assert.Throws<ArgumentException>(() => new LocalizationService(
            [],
            [
                new EmptyCustomProvider(),
                new EmptyCustomProvider(),
            ]));

        Assert.Contains("More than one custom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceRejectsNullAndUnknownProviderKinds()
    {
        Assert.Throws<ArgumentException>(() => new LocalizationService(
            [],
            new ILanguagePackageProvider[] { null! }));
        Assert.Throws<ArgumentException>(() => new LocalizationService(
            [],
            [new UnknownKindProvider()]));
    }

    [Fact]
    public async Task AddLocalizationRegistersCoreServices()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var services = new ServiceCollection();

        services.AddSingleton<ILanguagePackageProvider>(new RecordingLanguagePackageProvider(zh));
        services.AddLocalization(options => options.LanguagePackages.Add(zh.Descriptor));

        using var serviceProvider = services.BuildServiceProvider();
        var localization = serviceProvider.GetRequiredService<ILocalizationService>();
        var diagnostics = serviceProvider.GetRequiredService<ILocalizationDiagnostics>();
        var providers = serviceProvider.GetServices<ILanguagePackageProvider>().ToArray();

        await localization.SetCultureAsync("zh-CN");
        var text = await localization.GetStringAsync("Settings.Title");

        Assert.IsType<LocalizationService>(localization);
        Assert.IsType<InMemoryLocalizationDiagnostics>(diagnostics);
        Assert.Contains(providers, provider => provider.Kind == LanguagePackageProviderKind.InMemory);
        Assert.Contains(providers, provider => provider.Kind == LanguagePackageProviderKind.File);
        Assert.Contains(providers, provider => provider.Kind == LanguagePackageProviderKind.Assembly);
        Assert.Equal("设置", text.Value);
    }

    [Fact]
    public void AddLocalizationSeedsRegistryWithHostOwnedOptionDescriptors()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var services = new ServiceCollection();

        services.AddLocalization(options => options.LanguagePackages.Add(zh.Descriptor));

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();
        var registration = Assert.Single(registry.Registrations);

        Assert.Same(zh.Descriptor, registration.Descriptor);
        Assert.Equal("host", registration.OwnerId);
    }

    [Fact]
    public async Task RuntimeRegistryRegistrationAndOwnerRevocationDriveServiceLookup()
    {
        var host = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Host Settings"));
        var plugin = LanguagePackage.Create(
            new LanguagePackageDescriptor(
                "Plugin.zh-CN",
                CultureInfo.GetCultureInfo("zh-CN"),
                ResourceScope.Plugin)
            {
                ScopeId = "plugin.settings",
            },
            new Dictionary<string, string>
            {
                ["Settings.Title"] = "Plugin Settings",
            });
        var services = new ServiceCollection();

        services.AddSingleton<ILanguagePackageProvider>(new RecordingLanguagePackageProvider(host, plugin));
        services.AddLocalization(options => options.LanguagePackages.Add(host.Descriptor));

        using var serviceProvider = services.BuildServiceProvider();
        var localization = serviceProvider.GetRequiredService<ILocalizationService>();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();
        var context = new LocalizationLookupContext(pluginId: "plugin.settings");
        using var scope = localization.ActivateScope(context);

        await localization.SetCultureAsync("zh-CN");
        Assert.Equal("Host Settings", (await localization.GetStringAsync("Settings.Title")).Value);

        var registerResult = registry.Register(plugin.Descriptor, "plugin.settings");
        var pluginText = await localization.GetStringAsync("Settings.Title", context);
        var revokedCount = registry.RevokeOwner("plugin.settings");
        var hostText = await localization.GetStringAsync("Settings.Title");

        Assert.True(registerResult.Succeeded);
        Assert.Equal("Plugin Settings", pluginText.Value);
        Assert.Equal(1, revokedCount);
        Assert.True(plugin.IsDisposed);
        Assert.Equal("Host Settings", hostText.Value);
        Assert.DoesNotContain(
            registry.Registrations,
            registration => registration.OwnerId == "plugin.settings");
    }

    [Fact]
    public async Task OwnerRevocationDuringCultureLoadCannotResurrectRevokedPackage()
    {
        var host = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Host Settings"));
        var plugin = LanguagePackage.Create(
            new LanguagePackageDescriptor(
                "Plugin.zh-CN",
                CultureInfo.GetCultureInfo("zh-CN"),
                ResourceScope.Plugin)
            {
                ScopeId = "plugin.settings",
            },
            new Dictionary<string, string>
            {
                ["Settings.Title"] = "Plugin Settings",
            });
        var provider = new BlockingRegistrationProvider("Plugin.zh-CN", host, plugin);
        var services = new ServiceCollection();
        services.AddSingleton<ILanguagePackageProvider>(provider);
        services.AddLocalization(options => options.LanguagePackages.Add(host.Descriptor));

        using var serviceProvider = services.BuildServiceProvider();
        var localization = serviceProvider.GetRequiredService<ILocalizationService>();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();
        Assert.True(registry.Register(plugin.Descriptor, "plugin.settings").Succeeded);
        using var scope = localization.ActivateScope(
            new LocalizationLookupContext(pluginId: "plugin.settings"));

        var cultureSwitch = localization.SetCultureAsync("zh-CN").AsTask();
        await provider.BlockedLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, registry.RevokeOwner("plugin.settings"));
        provider.Release();

        var result = await cultureSwitch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.True(plugin.IsDisposed);
        Assert.DoesNotContain(
            "Plugin.zh-CN",
            ((LocalizationService)localization).State.LoadedPackageIds);
        Assert.Equal("Host Settings", (await localization.GetStringAsync("Settings.Title")).Value);
    }

    [Fact]
    public async Task OwnerRevocationRemovesLoadedFallbackPackagesFromCultureState()
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
        var services = new ServiceCollection();
        services.AddSingleton<ILanguagePackageProvider>(new RecordingLanguagePackageProvider(zh, en));
        services.AddLocalization(options =>
        {
            options.LanguagePackages.Add(zh.Descriptor);
            options.LanguagePackages.Add(en.Descriptor);
        });
        await using var serviceProvider = services.BuildServiceProvider();
        var localization = serviceProvider.GetRequiredService<ILocalizationService>();
        var service = Assert.IsType<LocalizationService>(localization);
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();

        await localization.SetCultureAsync("zh-CN");
        Assert.Equal("Settings", (await localization.GetStringAsync("Settings.Title")).Value);
        Assert.Equal(["Host.zh-CN", "Host.en-US"], service.State.LoadedPackageIds);

        Assert.Equal(2, registry.RevokeOwner("host"));

        Assert.Empty(service.State.LoadedPackageIds);
        Assert.True(zh.IsDisposed);
        Assert.True(en.IsDisposed);
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

    private sealed class BlockingRegistrationProvider : ILanguagePackageProvider
    {
        private readonly string _blockedPackageId;
        private readonly Dictionary<string, LanguagePackage> _packages;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingRegistrationProvider(
            string blockedPackageId,
            params LanguagePackage[] packages)
        {
            _blockedPackageId = blockedPackageId;
            _packages = packages.ToDictionary(
                package => package.Descriptor.PackageId,
                StringComparer.Ordinal);
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public TaskCompletionSource BlockedLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public async ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(descriptor.PackageId, _blockedPackageId, StringComparison.Ordinal))
            {
                BlockedLoadStarted.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }

            return LanguagePackageLoadResult.Success(_packages[descriptor.PackageId]);
        }
    }

    private sealed class EmptyCustomProvider : ILanguagePackageProvider
    {
        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Not found.")));
        }
    }

    private sealed class UnknownKindProvider : ILanguagePackageProvider
    {
        public LanguagePackageProviderKind Kind => (LanguagePackageProviderKind)999;

        public ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(LocalizationErrorKind.PackageNotFound, "Not used.")));
        }
    }
}
