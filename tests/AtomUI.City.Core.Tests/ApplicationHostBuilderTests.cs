using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.Configuration;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostBuilderTests
{
    [Fact]
    public async Task DefaultLoggingProvidersArePortableAndExcludeEventLog()
    {
        await using var host = ApplicationHostTestBuilder.Create().Build();

        var providerNames = host.Services
            .GetServices<ILoggerProvider>()
            .Select(static provider => provider.GetType().FullName)
            .ToArray();

        Assert.Contains("Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider", providerNames);
        Assert.Contains("Microsoft.Extensions.Logging.Debug.DebugLoggerProvider", providerNames);
        Assert.Contains("Microsoft.Extensions.Logging.EventSource.EventSourceLoggerProvider", providerNames);
        Assert.DoesNotContain(
            providerNames,
            static name => name?.Contains("EventLogLoggerProvider", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task WindowsApplicationCanExplicitlyOptIntoEventLog()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(AddWindowsEventLog);

        await using var host = builder.Build();

        Assert.Contains(
            host.Services.GetServices<ILoggerProvider>(),
            static provider => provider.GetType().FullName ==
                "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider");
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsEventLog(IServiceCollection services) =>
        services.AddLogging(logging => logging.AddEventLog());

    [Fact]
    public void CreateBuilderCreatesConfigurableApplicationHostBuilder()
    {
        var builder = ApplicationHost.CreateBuilder(["--sample:enabled=true"]);

        Assert.IsAssignableFrom<IApplicationHostBuilder>(builder);
        Assert.NotNull(builder.Configuration);
        Assert.Equal("true", builder.Configuration["sample:enabled"]);
    }

    [Fact]
    public void BuilderDoesNotExposeMutableServiceCollectionOrProperties()
    {
        Assert.Null(typeof(IApplicationHostBuilder).GetProperty("Services"));
        Assert.Null(typeof(ApplicationHostBuilder).GetProperty("Services"));
        Assert.Null(typeof(IApplicationHostBuilder).GetProperty("Properties"));
        Assert.Null(typeof(ApplicationHostBuilder).GetProperty("Properties"));
    }

    [Fact]
    public async Task BuildCreatesApplicationHostWithServicesAndContext()
    {
        var builder = ApplicationHostTestBuilder.Create(["--mode=test"]);

        builder.ConfigureServices(services => services.AddSingleton<TestService>());

        await using var host = builder.Build();

        Assert.IsAssignableFrom<IApplicationHost>(host);
        Assert.IsType<TestService>(host.Services.GetRequiredService<TestService>());
        Assert.Equal(["--mode=test"], host.Context.StartupArguments);
        Assert.Equal("test", host.Services.GetRequiredService<IConfiguration>()["mode"]);
        Assert.Equal(ApplicationHostTestBuilder.ApplicationId, host.Context.ApplicationId);
        Assert.Equal(ApplicationHostTestBuilder.ApplicationName, host.Context.ApplicationName);
        Assert.NotEqual(Guid.Empty, host.Context.ApplicationInstanceId);
        Assert.False(string.IsNullOrWhiteSpace(host.Context.ApplicationVersion));
        Assert.True(Path.IsPathFullyQualified(host.Context.ContentRootPath));
    }

    [Fact]
    public void UseDefaultServiceProviderRejectsNullArguments()
    {
        var builder = ApplicationHostTestBuilder.Create();

        Assert.Throws<ArgumentNullException>(() =>
            ApplicationHostBuilderServiceProviderExtensions.UseDefaultServiceProvider(
                null!,
                static _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            builder.UseDefaultServiceProvider(null!));
    }

    [Fact]
    public async Task UseDefaultServiceProviderAppliesCallbacksInRegistrationOrder()
    {
        var calls = new List<string>();
        var builder = ApplicationHostTestBuilder.Create();

        var returned = builder.UseDefaultServiceProvider(options =>
        {
            calls.Add("first");
            options.ValidateOnBuild = true;
        });
        builder.UseDefaultServiceProvider(options =>
        {
            Assert.True(options.ValidateOnBuild);
            calls.Add("second");
            options.ValidateScopes = true;
        });

        Assert.Same(builder, returned);
        Assert.Empty(calls);

        await using var host = builder.Build();

        Assert.Equal(["first", "second"], calls);
    }

    [Fact]
    public void UseDefaultServiceProviderValidateOnBuildRejectsMissingDependency()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services => services.AddSingleton<MissingDependencyService>());
        builder.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

        var failure = Assert.Throws<AggregateException>(() => builder.Build());

        Assert.Contains("Unable to resolve service", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UseDefaultServiceProviderValidateScopesRejectsSingletonCapturingScopedService()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ScopedDependency>();
            services.AddSingleton<SingletonCapturingScopedService>();
        });
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        var failure = Assert.Throws<AggregateException>(() => builder.Build());

        Assert.Contains("Cannot consume scoped service", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultServiceProviderBehaviorIsUnchangedWhenExtensionIsUnused()
    {
        var builder = ApplicationHostTestBuilder.Create(["--environment=Production"]);
        builder.ConfigureServices(services => services.AddSingleton<MissingDependencyService>());

        await using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            host.Services.GetRequiredService<MissingDependencyService>());
    }

    [Fact]
    public async Task UseDefaultServiceProviderCreatesAndDisposesOneRootSingleton()
    {
        RootSingletonProbe.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services => services.AddSingleton<RootSingletonProbe>());
        builder.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

        await using (var host = builder.Build())
        {
            var first = host.Services.GetRequiredService<RootSingletonProbe>();
            var second = host.Services.GetRequiredService<RootSingletonProbe>();

            Assert.Same(first, second);
            Assert.Equal(1, RootSingletonProbe.CreatedCount);
            Assert.Equal(0, RootSingletonProbe.DisposeCount);
        }

        Assert.Equal(1, RootSingletonProbe.CreatedCount);
        Assert.Equal(1, RootSingletonProbe.DisposeCount);
    }

    [Fact]
    public void DefaultServiceProviderConfigurationFailureIsDiagnosedAndFreezesBuilder()
    {
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.UseDefaultServiceProvider(_ =>
            throw new InvalidOperationException("service provider configuration failed"));

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("service provider configuration failed", failure.Message);
        Assert.Throws<InvalidOperationException>(() =>
            builder.UseDefaultServiceProvider(static _ => { }));
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildFailed &&
            record.Context["stage"] == "ServiceProviderOptions");
    }

    [Fact]
    public async Task ConfigureServicesRunsAfterAllModuleServiceStagesInRegistrationOrder()
    {
        ServiceOrderingModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ServiceOrderingModule>();
        builder.ConfigureServices(services =>
        {
            ServiceOrderingModule.Record("UserServices:First");
            services.AddSingleton<IOrderedService, UserOrderedService>();
        });
        builder.ConfigureServices(_ => ServiceOrderingModule.Record("UserServices:Second"));

        Assert.Empty(ServiceOrderingModule.Calls);

        await using var host = builder.Build();

        Assert.Equal(
            [
                "Module:PreConfigureServices",
                "Module:ConfigureServices",
                "Module:PostConfigureServices",
                "UserServices:First",
                "UserServices:Second",
            ],
            ServiceOrderingModule.Calls);
        Assert.IsType<UserOrderedService>(host.Services.GetRequiredService<IOrderedService>());
        Assert.Collection(
            host.Services.GetServices<IOrderedService>(),
            service => Assert.IsType<ModuleOrderedService>(service),
            service => Assert.IsType<UserOrderedService>(service));
    }

    [Fact]
    public async Task ConfigureServicesCanRemoveAndReplaceModuleDefaults()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ReplaceableServiceModule>();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRemovableService>();
            services.Replace(ServiceDescriptor.Singleton<IReplaceableService, UserReplaceableService>());
        });

        await using var host = builder.Build();

        Assert.Null(host.Services.GetService<IRemovableService>());
        Assert.IsType<UserReplaceableService>(host.Services.GetRequiredService<IReplaceableService>());
        Assert.Single(host.Services.GetServices<IReplaceableService>());
    }

    [Fact]
    public async Task UserServiceCollectionCapturedByCallbackIsFrozenAfterBuild()
    {
        IServiceCollection? capturedServices = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services =>
        {
            capturedServices = services;
            services.AddSingleton<TestService>();
        });

        await using var host = builder.Build();

        Assert.NotNull(capturedServices);
        Assert.True(capturedServices.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => capturedServices.AddSingleton<LateService>());
    }

    [Fact]
    public void UserServiceFailureIsDiagnosedAndFreezesBuilder()
    {
        IServiceCollection? capturedServices = null;
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.ConfigureServices(services =>
        {
            capturedServices = services;
            throw new InvalidOperationException("user service configuration failed");
        });

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("user service configuration failed", failure.Message);
        Assert.NotNull(capturedServices);
        Assert.True(capturedServices.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => capturedServices.AddSingleton<LateService>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureServices(services => services.AddSingleton<LateService>()));
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildFailed &&
            record.Context["stage"] == "UserServices");
    }

    [Fact]
    public void ModuleServiceFailureSkipsUserServiceCallbacks()
    {
        var userServicesCalled = false;
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<FailingServiceModule>();
        builder.ConfigureServices(_ => userServicesCalled = true);

        Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.False(userServicesCalled);
    }

    [Fact]
    public void BuildPreservesPrimaryFailureWhenGenericHostCleanupAlsoFails()
    {
        ThrowingBuildCleanupService.DisposeCount = 0;
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ThrowingBuildCleanupService>();
            services.AddSingleton<IHostDiagnostics>(services =>
            {
                _ = services.GetRequiredService<ThrowingBuildCleanupService>();
                throw new InvalidOperationException("runtime diagnostics activation failed");
            });
        });

        var failure = Assert.Throws<AggregateException>(() => builder.Build());

        Assert.Collection(
            failure.InnerExceptions,
            primary => Assert.Equal("runtime diagnostics activation failed", primary.Message),
            cleanup => Assert.Equal("generic host cleanup failed", cleanup.Message));
        Assert.Equal(1, ThrowingBuildCleanupService.DisposeCount);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
            record.Context["resourceKind"] == "GenericHost" &&
            record.Context["buildStage"] == "GenericHost");
    }

    [Fact]
    public void BuildFailureAwaitsAsyncOnlyGenericHostServices()
    {
        AsyncOnlyBuildCleanupService.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<AsyncOnlyBuildCleanupService>();
            services.AddSingleton<IHostDiagnostics>(provider =>
            {
                _ = provider.GetRequiredService<AsyncOnlyBuildCleanupService>();
                throw new InvalidOperationException("runtime diagnostics activation failed");
            });
        });

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("runtime diagnostics activation failed", failure.Message);
        Assert.Equal(1, AsyncOnlyBuildCleanupService.DisposeCount);
        Assert.True(AsyncOnlyBuildCleanupService.DisposeCompleted);
    }

    [Fact]
    public void BuildPreservesPrimaryFailureWhenAsyncGenericHostCleanupAlsoFails()
    {
        ThrowingAsyncOnlyBuildCleanupService.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ThrowingAsyncOnlyBuildCleanupService>();
            services.AddSingleton<IHostDiagnostics>(provider =>
            {
                _ = provider.GetRequiredService<ThrowingAsyncOnlyBuildCleanupService>();
                throw new InvalidOperationException("runtime diagnostics activation failed");
            });
        });

        var failure = Assert.Throws<AggregateException>(() => builder.Build());

        Assert.Collection(
            failure.InnerExceptions,
            primary => Assert.Equal("runtime diagnostics activation failed", primary.Message),
            cleanup => Assert.Equal("async generic host cleanup failed", cleanup.Message));
        Assert.Equal(1, ThrowingAsyncOnlyBuildCleanupService.DisposeCount);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
            record.Context["resourceKind"] == "GenericHost" &&
            record.Context["buildStage"] == "GenericHost");
    }

    [Fact]
    public void BuildAsyncCleanupUsesBoundedDeadlineAndReportsStillRunningCleanup()
    {
        HangingAsyncOnlyBuildCleanupService.Reset();
        ExpiredBudgetCleanupModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(100));
        builder.UseModule<ExpiredBudgetCleanupModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HangingAsyncOnlyBuildCleanupService>();
            services.AddSingleton<IHostDiagnostics>(provider =>
            {
                _ = provider.GetRequiredService<HangingAsyncOnlyBuildCleanupService>();
                throw new InvalidOperationException("runtime diagnostics activation failed");
            });
        });

        try
        {
            var failure = Assert.Throws<AggregateException>(() => builder.Build());

            Assert.Equal("runtime diagnostics activation failed", failure.InnerExceptions[0].Message);
            Assert.Equal(2, failure.InnerExceptions.Count(exception => exception is TimeoutException));
            Assert.Equal(1, HangingAsyncOnlyBuildCleanupService.DisposeCount);
            Assert.False(HangingAsyncOnlyBuildCleanupService.DisposeCompleted);
            Assert.True(SpinWait.SpinUntil(
                () => ExpiredBudgetCleanupModule.DisposeCount == 1,
                TimeSpan.FromSeconds(5)));
            Assert.Contains(diagnostics.Records, record =>
                record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
                record.Context["resourceKind"] == "GenericHost" &&
                record.Context["exceptionType"] == typeof(AsyncCleanupTimeoutException).FullName &&
                record.Context["cleanupTimeout"] == TimeSpan.FromMilliseconds(100).ToString() &&
                record.Context["cleanupStarted"] == bool.TrueString &&
                record.Context["cleanupMayStillBeRunning"] == bool.TrueString);
            Assert.Contains(diagnostics.Records, record =>
                record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
                record.Context["resourceKind"] == "ModuleRegistry" &&
                record.Context["exceptionType"] == typeof(AsyncCleanupTimeoutException).FullName &&
                record.Context["remainingWaitTimeout"] == TimeSpan.Zero.ToString() &&
                record.Context["cleanupStarted"] == bool.TrueString);
        }
        finally
        {
            HangingAsyncOnlyBuildCleanupService.Release();
            ExpiredBudgetCleanupModule.Release();
            Assert.True(SpinWait.SpinUntil(
                () => HangingAsyncOnlyBuildCleanupService.DisposeCompleted,
                TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(
                () => ExpiredBudgetCleanupModule.DisposeCompleted,
                TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void BuildAggregatesModuleServiceAndCleanupFailures()
    {
        FailingServiceAndDisposeModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.UseModule<FailingServiceAndDisposeModule>();

        var failure = Assert.Throws<AggregateException>(() => builder.Build());

        Assert.Collection(
            failure.InnerExceptions,
            primary => Assert.Equal("module service configuration failed", primary.Message),
            cleanup => Assert.Equal("module cleanup failed", cleanup.Message));
        Assert.Equal(1, FailingServiceAndDisposeModule.DisposeCount);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.ModuleLifecycleFailed &&
            record.Context["stage"] == "Dispose" &&
            record.Context["moduleType"] == typeof(FailingServiceAndDisposeModule).FullName);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
            record.Context["resourceKind"] == "ModuleRegistry" &&
            record.Context["buildStage"] == "ModuleServices");
    }

    [Fact]
    public async Task StartupArgumentsRejectExternalListMutation()
    {
        await using var host = ApplicationHostTestBuilder.Create(["--mode=test"]).Build();
        var arguments = Assert.IsAssignableFrom<IList<string>>(host.Context.StartupArguments);

        Assert.Throws<NotSupportedException>(() => arguments[0] = "--mode=changed");
        Assert.Equal("--mode=test", host.Context.StartupArguments[0]);
    }

    [Fact]
    public async Task StartupArgumentsAreCopiedBeforeBuild()
    {
        var source = new[] { "--mode=original" };
        var builder = ApplicationHostTestBuilder.Create(source);
        source[0] = "--mode=changed";

        await using var host = builder.Build();

        Assert.Equal("--mode=original", host.Context.StartupArguments[0]);
    }

    [Fact]
    public void ApplicationContextPublicContractContainsOnlyImmutableDescriptorFields()
    {
        var propertyNames = typeof(IApplicationContext)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "AppDataPath",
                "ApplicationId",
                "ApplicationInstanceId",
                "ApplicationName",
                "ApplicationVersion",
                "ContentRootPath",
                "EnvironmentName",
                "StartupArguments",
            },
            propertyNames);
        Assert.All(typeof(IApplicationContext).GetProperties(), property => Assert.False(property.CanWrite));
    }

    [Fact]
    public async Task BuildFreezesPublicBuilderMutationEntrypoints()
    {
        var builder = ApplicationHostTestBuilder.Create();

        await using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureServices(services => services.AddSingleton<TestService>()));
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureHost(options => options.ApplicationName = "changed"));
        Assert.Throws<InvalidOperationException>(() =>
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["changed"] = "true" }));
    }

    [Fact]
    public async Task BuildFreezesEveryCapturedConfigurationSectionTraversal()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.Configuration["Boxer:Mode"] = "Original";
        builder.Configuration["Boxer:Nested:Level"] = "One";
        var section = builder.Configuration.GetSection("Boxer");
        var mode = section.GetSection("Mode");
        var nested = Assert.Single(section.GetChildren(), child => child.Key == "Nested");
        var managerChild = Assert.Single(
            builder.Configuration.GetChildren(),
            child => child.Key == "Boxer");
        var lazyChildren = section.GetChildren();

        await using var host = builder.Build();
        var lazyMode = Assert.Single(lazyChildren, child => child.Key == "Mode");

        Assert.Throws<InvalidOperationException>(() => section["Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => section.Value = "Changed");
        Assert.Throws<InvalidOperationException>(() => mode.Value = "Changed");
        Assert.Throws<InvalidOperationException>(() => nested["Level"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => managerChild["Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => lazyMode.Value = "Changed");
        Assert.Throws<InvalidOperationException>(() =>
            builder.Configuration.GetSection("Boxer")["Mode"] = "Changed");

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("Original", configuration["Boxer:Mode"]);
        Assert.Equal("One", configuration["Boxer:Nested:Level"]);
        Assert.Null(configuration["Boxer"]);
    }

    [Fact]
    public async Task ConfigurationGuardsAllowEveryMutationPathBeforeBuild()
    {
        var builder = ApplicationHostTestBuilder.Create();
        var sources = builder.Configuration.Sources;
        var properties = builder.Configuration.Properties;

        Assert.False(sources.IsReadOnly);
        Assert.False(properties.IsReadOnly);

        builder.Configuration.AddInMemoryCollection();
        var section = builder.Configuration.GetSection("BeforeBuild");
        var child = section.GetSection("Child");
        var root = builder.Configuration.Build();
        var provider = root.Providers.Last();

        root.Reload();
        section.Value = "Section";
        child.Value = "Child";
        root["BeforeBuild:Root"] = "Root";
        provider.Set("BeforeBuild:Provider", "Provider");

        await using var host = builder.Build();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        Assert.True(sources.IsReadOnly);
        Assert.True(properties.IsReadOnly);
        Assert.Equal("Section", configuration["BeforeBuild"]);
        Assert.Equal("Child", configuration["BeforeBuild:Child"]);
        Assert.Equal("Root", configuration["BeforeBuild:Root"]);
        Assert.Equal("Provider", configuration["BeforeBuild:Provider"]);
    }

    [Fact]
    public async Task BuildFreezesCapturedConfigurationRootAndProviders()
    {
        const string sectionName = "AtomUICityConfigurationFreezeProbe";
        var builder = ApplicationHostTestBuilder.Create();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{sectionName}:Mode"] = "Original",
                [$"{sectionName}:Nested:Level"] = "One",
            });
        var root = builder.Configuration.Build();
        var rootSection = root.GetSection(sectionName);
        var rootChild = Assert.Single(
            root.GetChildren(),
            child => string.Equals(child.Key, sectionName, StringComparison.OrdinalIgnoreCase));
        var providers = root.Providers.ToArray();
        Assert.NotEmpty(providers);

        Assert.Equal("Original", root[$"{sectionName}:Mode"]);
        Assert.Equal("One", root[$"{sectionName}:Nested:Level"]);

        await using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => root[$"{sectionName}:Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => rootSection["Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => rootChild["Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(root.Reload);
        Assert.All(providers, provider =>
        {
            Assert.Throws<InvalidOperationException>(() =>
                provider.Set($"{sectionName}:Mode", "Changed"));
            Assert.Throws<InvalidOperationException>(provider.Load);
        });

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("Original", configuration[$"{sectionName}:Mode"]);
        Assert.Equal("One", configuration[$"{sectionName}:Nested:Level"]);
    }

    [Fact]
    public void FailedBuildFreezesCapturedConfigurationHandles()
    {
        var builder = ApplicationHostTestBuilder.Create();
        var sources = builder.Configuration.Sources;
        var properties = builder.Configuration.Properties;
        builder.Configuration["Failure:Mode"] = "Original";
        var section = builder.Configuration.GetSection("Failure");
        var root = builder.Configuration.Build();
        var providers = root.Providers.ToArray();
        builder.ConfigureServices(_ =>
            throw new InvalidOperationException("configuration freeze build failure"));

        Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.True(sources.IsReadOnly);
        Assert.True(properties.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => section["Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(() => root["Failure:Mode"] = "Changed");
        Assert.Throws<InvalidOperationException>(root.Reload);
        Assert.All(providers, provider =>
            Assert.Throws<InvalidOperationException>(() =>
                provider.Set("Failure:Mode", "Changed")));
        Assert.Equal("Original", section["Mode"]);
    }

    [Fact]
    public async Task BuildFreezesLargeConfigurationSectionGraph()
    {
        var builder = ApplicationHostTestBuilder.Create();
        for (var index = 0; index < 64; index++)
        {
            builder.Configuration[$"Matrix:Owner{index}:Mode"] = "Original";
        }

        var sections = builder.Configuration
            .GetSection("Matrix")
            .GetChildren()
            .Select(owner => owner.GetSection("Mode"))
            .ToArray();
        Assert.Equal(64, sections.Length);

        await using var host = builder.Build();

        Assert.All(sections, section =>
            Assert.Throws<InvalidOperationException>(() => section.Value = "Changed"));
        Assert.All(sections, section => Assert.Equal("Original", section.Value));
    }

    [Fact]
    public async Task BuildCanOnlyRunOnce()
    {
        var builder = ApplicationHostTestBuilder.Create();

        await using var host = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("only build once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildFreezesModuleAndLifecycleExtensionStores()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<LateModule>();
        builder.UseModule<LateModule>();

        await using var host = builder.Build();

        Assert.Single(
            host.Services.GetRequiredService<IModuleRegistry>().Modules,
            descriptor => descriptor.ModuleType == typeof(LateModule));
        Assert.Throws<InvalidOperationException>(() => builder.UseModule<LateModule>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureLifecycle(lifecycle =>
                lifecycle.Use(static (_, next, _) => next())));
    }

    [Fact]
    public void BuildFailureKeepsModuleAndLifecycleExtensionStoresFrozen()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(_ => builder.UseModule<LateModule>());

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("frozen after Build", failure.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => builder.UseModule<LateModule>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureLifecycle(static _ => { }));
    }

    private interface IOrderedService;

    private interface IRemovableService;

    private interface IReplaceableService;

    private sealed class TestService;

    private sealed class LateService;

    private interface IMissingDependency;

    private sealed class MissingDependencyService(IMissingDependency dependency)
    {
        public IMissingDependency Dependency { get; } = dependency;
    }

    private sealed class ScopedDependency;

    private sealed class SingletonCapturingScopedService(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class RootSingletonProbe : IDisposable
    {
        private static int _createdCount;
        private static int _disposeCount;

        public RootSingletonProbe()
        {
            Interlocked.Increment(ref _createdCount);
        }

        public static int CreatedCount => Volatile.Read(ref _createdCount);

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static void Reset()
        {
            Volatile.Write(ref _createdCount, 0);
            Volatile.Write(ref _disposeCount, 0);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
        }
    }

    private sealed class ModuleOrderedService : IOrderedService;

    private sealed class UserOrderedService : IOrderedService;

    private sealed class ModuleRemovableService : IRemovableService;

    private sealed class ModuleReplaceableService : IReplaceableService;

    private sealed class UserReplaceableService : IReplaceableService;

    private sealed class LateModule : ModuleBase;

    private sealed class ServiceOrderingModule : ModuleBase
    {
        private static readonly List<string> RecordedCalls = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static void Reset() => RecordedCalls.Clear();

        public static void Record(string call) => RecordedCalls.Add(call);

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:PreConfigureServices");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:ConfigureServices");
            context.Services.AddSingleton<IOrderedService, ModuleOrderedService>();
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:PostConfigureServices");
        }
    }

    private sealed class ReplaceableServiceModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IRemovableService, ModuleRemovableService>();
            context.Services.AddSingleton<IReplaceableService, ModuleReplaceableService>();
        }
    }

    private sealed class FailingServiceModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            throw new InvalidOperationException("module service configuration failed");
        }
    }

    private sealed class FailingServiceAndDisposeModule : ModuleBase, IDisposable
    {
        public static int DisposeCount { get; private set; }

        public static void Reset() => DisposeCount = 0;

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            throw new InvalidOperationException("module service configuration failed");

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("module cleanup failed");
        }
    }

    private sealed class ThrowingBuildCleanupService : IDisposable
    {
        public static int DisposeCount { get; set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("generic host cleanup failed");
        }
    }

    private sealed class AsyncOnlyBuildCleanupService : IAsyncDisposable
    {
        public static int DisposeCount { get; private set; }

        public static bool DisposeCompleted { get; private set; }

        public static void Reset()
        {
            DisposeCount = 0;
            DisposeCompleted = false;
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await Task.Yield();
            DisposeCompleted = true;
        }
    }

    private sealed class ThrowingAsyncOnlyBuildCleanupService : IAsyncDisposable
    {
        public static int DisposeCount { get; private set; }

        public static void Reset() => DisposeCount = 0;

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await Task.Yield();
            throw new InvalidOperationException("async generic host cleanup failed");
        }
    }

    private sealed class HangingAsyncOnlyBuildCleanupService : IAsyncDisposable
    {
        private static TaskCompletionSource _release = CreateCompletionSource();
        private static int _disposeCount;
        private static int _disposeCompleted;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static bool DisposeCompleted => Volatile.Read(ref _disposeCompleted) != 0;

        public static void Reset()
        {
            _release = CreateCompletionSource();
            Volatile.Write(ref _disposeCount, 0);
            Volatile.Write(ref _disposeCompleted, 0);
        }

        public static void Release() => _release.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            await _release.Task.ConfigureAwait(false);
            Volatile.Write(ref _disposeCompleted, 1);
        }

        private static TaskCompletionSource CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ExpiredBudgetCleanupModule : ModuleBase, IAsyncDisposable
    {
        private static TaskCompletionSource _release = CreateCompletionSource();
        private static int _disposeCount;
        private static int _disposeCompleted;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static bool DisposeCompleted => Volatile.Read(ref _disposeCompleted) != 0;

        public static void Reset()
        {
            _release = CreateCompletionSource();
            Volatile.Write(ref _disposeCount, 0);
            Volatile.Write(ref _disposeCompleted, 0);
        }

        public static void Release() => _release.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            await _release.Task.ConfigureAwait(false);
            Volatile.Write(ref _disposeCompleted, 1);
        }

        private static TaskCompletionSource CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
