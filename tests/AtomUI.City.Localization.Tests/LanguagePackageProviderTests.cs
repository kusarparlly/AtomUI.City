using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Localization.Tests;

public sealed class LanguagePackageProviderTests
{
    [Fact]
    public void AssemblyProviderUsesDescriptorLoadContextWithoutPinningDefaultContext()
    {
        var loadContext = LoadAssemblyPackageInCollectibleContext();

        for (var attempt = 0; loadContext.IsAlive && attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(loadContext.IsAlive);
    }

    [Fact]
    public void AddLocalizationRegistersLanguagePackageRegistry()
    {
        var services = new ServiceCollection();

        services.AddLocalization();

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();

        Assert.Empty(registry.Registrations);
    }

    [Fact]
    public void RegistryRegistersDescriptorsWithOwners()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Host.zh-CN", "zh-CN");

        var result = registry.Register(descriptor, "host");

        Assert.True(result.Succeeded);
        var registration = Assert.Single(registry.Registrations);
        Assert.Same(descriptor, registration.Descriptor);
        Assert.Equal("host", registration.OwnerId);
    }

    [Fact]
    public void RegistryRejectsDuplicatePackageIds()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Host.zh-CN", "zh-CN");

        registry.Register(descriptor, "host");
        var result = registry.Register(descriptor, "module.settings");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageAlreadyRegistered, result.Error?.Kind);
        Assert.Single(registry.Registrations);
    }

    [Fact]
    public void RegistryAllowsSamePackageIdForDifferentCultures()
    {
        var registry = new LanguagePackageRegistry();
        var zh = Descriptor("Host.Shared", "zh-CN");
        var en = Descriptor("Host.Shared", "en-US");

        var zhResult = registry.Register(zh, "host");
        var enResult = registry.Register(en, "host");

        Assert.True(zhResult.Succeeded);
        Assert.True(enResult.Succeeded);
        Assert.Equal(2, registry.Registrations.Count);
    }

    [Fact]
    public void RegistryRegisterRangeDoesNotPublishPartialBatchWhenIdentityConflicts()
    {
        var registry = new LanguagePackageRegistry();
        var existing = Descriptor("Existing", "en-US");
        var pending = Descriptor("Pending", "en-US");
        Assert.True(registry.Register(existing, "host").Succeeded);

        var result = registry.RegisterRange([pending, existing], "plugin.sales");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageAlreadyRegistered, result.Error?.Kind);
        Assert.Equal([existing], registry.Descriptors);
    }

    [Fact]
    public void DescriptorAndRegistryRejectUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            (ResourceScope)999));
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = (LanguagePackageProviderKind)999,
        };

        var result = new LanguagePackageRegistry().Register(descriptor, "host");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, result.Error?.Kind);
    }

    [Fact]
    public void DescriptorTakesReadonlyCultureSnapshots()
    {
        var culture = new CultureInfo("en-US");
        var fallback = new CultureInfo("fr-FR");
        var descriptor = new LanguagePackageDescriptor("Host", culture, ResourceScope.Host)
        {
            FallbackCulture = fallback,
        };

        culture.DateTimeFormat.ShortDatePattern = "yyyy";
        fallback.DateTimeFormat.ShortDatePattern = "MM";

        Assert.True(descriptor.Culture.IsReadOnly);
        Assert.True(descriptor.FallbackCulture!.IsReadOnly);
        Assert.NotEqual("yyyy", descriptor.Culture.DateTimeFormat.ShortDatePattern);
        Assert.NotEqual("MM", descriptor.FallbackCulture.DateTimeFormat.ShortDatePattern);
    }

    [Fact]
    public async Task BuiltInProvidersRejectDescriptorKindMismatch()
    {
        var descriptor = Descriptor("Host.en-US", "en-US");
        var fileDescriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
        };

        var fileResult = await new FileLanguagePackageProvider().LoadAsync(descriptor);
        var assemblyResult = await new AssemblyLanguagePackageProvider().LoadAsync(descriptor);
        var inMemoryResult = await new InMemoryLanguagePackageProvider().LoadAsync(fileDescriptor);

        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, fileResult.Error?.Kind);
        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, assemblyResult.Error?.Kind);
        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, inMemoryResult.Error?.Kind);
    }

    [Fact]
    public void RegistryRevokesOwnerDescriptorsAndRejectsFutureRegistrations()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Plugin.sales.zh-CN", "zh-CN");

        registry.Register(descriptor, "plugin.sales");
        var revokedCount = registry.RevokeOwner("plugin.sales");
        var result = registry.Register(descriptor, "plugin.sales");

        Assert.Equal(1, revokedCount);
        Assert.Empty(registry.Registrations);
        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.OwnerRevoked, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderLoadsLocPackForRequestedCulture()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 1,
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": {
                "Settings.Title": "Settings zh"
              }
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
            AllowedRootPath = workspace.Root,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.True(result.Package.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings zh", value);
    }

    [Fact]
    public async Task FileProviderValidatesSha256Checksum()
    {
        using var workspace = new LocalizationTestWorkspace();
        const string json = """
            {
              "schemaVersion": 1,
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": { "Settings.Title": "Settings" }
            }
            """;
        var path = workspace.WriteLocPack(json);
        var checksum = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var provider = new FileLanguagePackageProvider();
        var valid = await provider.LoadAsync(new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = path,
            AllowedRootPath = workspace.Root,
            Checksum = checksum,
        });
        var invalid = await provider.LoadAsync(new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = path,
            AllowedRootPath = workspace.Root,
            Checksum = "sha256:00",
        });

        Assert.True(valid.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageChecksumMismatch, invalid.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderRejectsUnsupportedSchemaAndPathOutsideRoot()
    {
        using var workspace = new LocalizationTestWorkspace();
        using var outsideWorkspace = new LocalizationTestWorkspace();
        var unsupportedPath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 2,
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": {}
            }
            """);
        var provider = new FileLanguagePackageProvider();
        var unsupported = await provider.LoadAsync(new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = unsupportedPath,
            AllowedRootPath = workspace.Root,
        });
        var outsideRoot = await provider.LoadAsync(new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = unsupportedPath,
            AllowedRootPath = outsideWorkspace.Root,
        });

        Assert.Equal(LocalizationErrorKind.PackageSchemaMismatch, unsupported.Error?.Kind);
        Assert.Equal(LocalizationErrorKind.InvalidDescriptor, outsideRoot.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderReturnsCancelledResultWhenTokenIsCancelled()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 1,
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": {}
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
            AllowedRootPath = workspace.Root,
        };
        var provider = new FileLanguagePackageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.LoadAsync(descriptor, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task AssemblyProviderLoadsEmbeddedLocPackResource()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
        };
        var provider = new AssemblyLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.True(result.Package.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings", value);
    }

    [Fact]
    public async Task AssemblyProviderLoadsEmbeddedLocPackFromSourceAssemblyWithoutLocation()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            SourceAssembly = typeof(LanguagePackageProviderTests).Assembly,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
        };

        var result = await new AssemblyLanguagePackageProvider().LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.True(result.Package!.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings", value);
    }

    [Fact]
    public async Task AssemblyProviderLoadsUniqueResourceSuffix()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "Fixtures.Host.en-US.locpack.json",
        };

        var result = await new AssemblyLanguagePackageProvider().LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.True(result.Package!.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings", value);
    }

    [Fact]
    public async Task AssemblyProviderRejectsAmbiguousResourceSuffix()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "Host.en-US.locpack.json",
        };

        var result = await new AssemblyLanguagePackageProvider().LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageNotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task AssemblyProviderReturnsCancelledResultWhenTokenIsCancelled()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
        };
        var provider = new AssemblyLanguagePackageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.LoadAsync(descriptor, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderRejectsCultureMismatch()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 1,
              "packageId": "Host.zh-CN",
              "culture": "en-US",
              "resources": {
                "Settings.Title": "Settings"
              }
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
            AllowedRootPath = workspace.Root,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageCultureMismatch, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderRejectsIdentityVersionAndResourceShapeMismatch()
    {
        using var workspace = new LocalizationTestWorkspace();
        var provider = new FileLanguagePackageProvider();
        var cases = new[]
        {
            (
                Json: """{"schemaVersion":1,"packageId":"Other","culture":"zh-CN","resources":{}}""",
                Version: (string?)null,
                Error: LocalizationErrorKind.PackageIdentityMismatch),
            (
                Json: """{"schemaVersion":1,"packageId":"Host.zh-CN","culture":"zh-CN","version":"2.0","resources":{}}""",
                Version: (string?)"1.0",
                Error: LocalizationErrorKind.PackageVersionMismatch),
            (
                Json: """{"schemaVersion":1,"packageId":"Host.zh-CN","culture":"zh-CN","resources":{"Settings.Count":42}}""",
                Version: (string?)null,
                Error: LocalizationErrorKind.InvalidResource),
            (
                Json: """{"packageId":"Host.zh-CN","culture":"zh-CN","resources":{}}""",
                Version: (string?)null,
                Error: LocalizationErrorKind.PackageSchemaMismatch),
            (
                Json: """{"schemaVersion":1,"packageId":"Host.zh-CN","culture":"invalid culture!","resources":{}}""",
                Version: (string?)null,
                Error: LocalizationErrorKind.PackageCultureMismatch),
            (
                Json: """{"schemaVersion":1,"packageId":"Host.zh-CN","culture":"zh-CN","resources":{"": "value"}}""",
                Version: (string?)null,
                Error: LocalizationErrorKind.InvalidResource),
            (
                Json: """{"schemaVersion":1,"packageId":"Host.zh-CN","culture":"zh-CN","resources":{}}""",
                Version: (string?)"1.0",
                Error: LocalizationErrorKind.PackageSchemaMismatch),
        };

        foreach (var testCase in cases)
        {
            var path = workspace.WriteLocPack(testCase.Json);
            var result = await provider.LoadAsync(new LanguagePackageDescriptor(
                "Host.zh-CN",
                CultureInfo.GetCultureInfo("zh-CN"),
                ResourceScope.Host)
            {
                ProviderKind = LanguagePackageProviderKind.File,
                Location = path,
                AllowedRootPath = workspace.Root,
                Version = testCase.Version,
            });

            Assert.Equal(testCase.Error, result.Error?.Kind);
        }
    }

    [Fact]
    public async Task FileProviderRejectsOversizedAndDuplicateRootProperties()
    {
        using var workspace = new LocalizationTestWorkspace();
        var oversizedPath = Path.Combine(workspace.Root, "oversized.locpack.json");
        await using (var stream = File.Create(oversizedPath))
        {
            stream.SetLength((16L * 1024 * 1024) + 1);
        }

        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = oversizedPath,
            AllowedRootPath = workspace.Root,
        };
        var provider = new FileLanguagePackageProvider();

        var oversized = await provider.LoadAsync(descriptor);

        Assert.False(oversized.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageTooLarge, oversized.Error?.Kind);

        var duplicatePath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 1,
              "packageId": "Wrong",
              "packageId": "Host.en-US",
              "culture": "en-US",
              "resources": {}
            }
            """);
        var duplicateDescriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = duplicatePath,
            AllowedRootPath = workspace.Root,
        };

        var duplicate = await provider.LoadAsync(duplicateDescriptor);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageSchemaMismatch, duplicate.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderReturnsFailedResultForMalformedLocPack()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "schemaVersion": 1,
              "packageId": "Host.zh-CN",
              "resources": {}
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
            AllowedRootPath = workspace.Root,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageSchemaMismatch, result.Error?.Kind);
    }

    private static LanguagePackageDescriptor Descriptor(
        string packageId,
        string cultureName)
    {
        return new LanguagePackageDescriptor(
            packageId,
            CultureInfo.GetCultureInfo(cultureName),
            ResourceScope.Host);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAssemblyPackageInCollectibleContext()
    {
        var context = new AssemblyLoadContext(
            "localization-test-" + Guid.NewGuid().ToString("N"),
            isCollectible: true);
        var weakReference = new WeakReference(context);
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
            LoadContext = context,
        };
        var provider = new AssemblyLanguagePackageProvider();
        var result = provider.LoadAsync(descriptor).AsTask().GetAwaiter().GetResult();

        Assert.True(result.Succeeded);
        Assert.Contains(
            context.Assemblies,
            assembly => string.Equals(
                assembly.Location,
                typeof(LanguagePackageProviderTests).Assembly.Location,
                StringComparison.OrdinalIgnoreCase));
        result.Package!.Dispose();
        context.Unload();

        return weakReference;
    }
}
