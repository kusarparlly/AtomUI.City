using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostModuleLifecycleTests
{
    [Fact]
    public async Task BuildAndStartRunModulesInDependencyOrderAndShutdownInReverseOrder()
    {
        ModuleRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();

        builder
            .UseModule<AppModule>()
            .UseModule<CoreModule>();

        await using var host = builder.Build();

        Assert.IsType<CoreService>(host.Services.GetRequiredService<ICoreService>());

        await host.StartAsync();

        Assert.NotNull(host.ApplicationScope);
        Assert.Equal(6, ModuleRecorder.ApplicationScopes.Count);
        Assert.All(
            ModuleRecorder.ApplicationScopes,
            scope => Assert.Same(host.ApplicationScope, scope));

        await host.StopAsync();

        Assert.Equal(
            [
                "Core:PreConfigureServices",
                "App:PreConfigureServices",
                "Core:ConfigureServices",
                "App:ConfigureServices",
                "Core:PostConfigureServices",
                "App:PostConfigureServices",
                "Core:ConfigureContributions",
                "App:ConfigureContributions",
                "Core:OnPreApplicationInitialization",
                "App:OnPreApplicationInitialization",
                "Core:OnApplicationInitialization",
                "App:OnApplicationInitialization",
                "Core:OnPostApplicationInitialization",
                "App:OnPostApplicationInitialization",
                "App:OnApplicationShutdown",
                "Core:OnApplicationShutdown",
            ],
            ModuleRecorder.Calls);
    }

    [Fact]
    public async Task AsyncModuleInitializationStagesAreAwaited()
    {
        ModuleRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();

        builder.UseModule<AsyncModule>();

        await using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(
            [
                "Async:OnApplicationInitialization.Start",
                "Async:OnApplicationInitialization.End",
                "Async:OnApplicationShutdown.Start",
                "Async:OnApplicationShutdown.End",
            ],
            ModuleRecorder.Calls.Where(call => call.StartsWith("Async:", StringComparison.Ordinal)));
    }

    [Fact]
    public void BuildFailsWhenRequiredDependencyIsMissing()
    {
        var builder = ApplicationHostTestBuilder.Create();

        builder.UseModule<MissingRequiredDependencyModule>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(MissingRequiredDependencyModule), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(CoreModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleGraphFailureDoesNotCreateModuleInstances()
    {
        DisposableBuildModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<DisposableBuildModule>();
        builder.UseModule<MissingRequiredDependencyModule>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.False(DisposableBuildModule.WasDisposed);
    }

    [Fact]
    public void ModuleConstructionFailureAggregatesDependencyCleanupFailure()
    {
        ThrowingConstructionCleanupModule.DisposeCount = 0;

        var failure = Assert.Throws<AggregateException>(() => ModuleRegistry.CreateForTesting(
            [typeof(FailingConstructorModule), typeof(ThrowingConstructionCleanupModule)]));

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal("construction rollback cleanup failed", failure.InnerExceptions[1].Message);
        Assert.Equal(1, ThrowingConstructionCleanupModule.DisposeCount);
    }

    [Fact]
    public void BuildFailureAwaitsAsyncModuleCleanupWithoutPumpingSynchronizationContext()
    {
        AsyncBuildFailureCleanupModule.Reset();

        var result = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = ApplicationHostTestBuilder.Create();
            builder.UseModule<AsyncBuildFailureCleanupModule>();
            using var host = builder.Build();
        });

        Assert.True(result.Completed, "Build deadlocked while awaiting asynchronous failure cleanup.");
        var failure = Assert.IsType<InvalidOperationException>(result.Failure);
        Assert.Equal("async build failed", failure.Message);
        Assert.Equal(1, AsyncBuildFailureCleanupModule.DisposeCount);
    }

    [Fact]
    public void ModuleConstructionFailureAwaitsAsyncDependencyCleanupWithoutPumpingSynchronizationContext()
    {
        AsyncConstructionCleanupModule.Reset();

        var result = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = ApplicationHostTestBuilder.Create();
            builder.UseModule<AsyncConstructionCleanupModule>();
            builder.UseModule<AsyncCleanupFailingConstructorModule>();
            using var host = builder.Build();
        });

        Assert.True(result.Completed, "Build deadlocked while rolling back partially constructed modules.");
        var failure = Assert.IsType<System.Reflection.TargetInvocationException>(result.Failure);
        var cause = Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal("async cleanup construction failed", cause.Message);
        Assert.Equal(1, AsyncConstructionCleanupModule.DisposeCount);
    }

    [Fact]
    public void ModuleConstructionRollbackUsesBoundedAsyncCleanupDeadline()
    {
        HangingConstructionCleanupModule.Reset();
        var diagnostics = new InMemoryHostDiagnostics();

        try
        {
            var result = RunBuildOnNonPumpingSynchronizationContext(() =>
                ModuleRegistry.CreateForTesting(
                    [typeof(HangingConstructionCleanupModule), typeof(HangingCleanupFailingConstructorModule)],
                    diagnostics,
                    TimeSpan.FromMilliseconds(100)));

            Assert.True(result.Completed, "Module construction rollback exceeded its cleanup deadline.");
            var failure = Assert.IsType<AggregateException>(result.Failure);
            Assert.Contains(failure.InnerExceptions, exception => exception is TimeoutException);
            Assert.Equal(1, HangingConstructionCleanupModule.DisposeCount);
            Assert.False(HangingConstructionCleanupModule.DisposeCompleted);
            Assert.Contains(diagnostics.Records, record =>
                record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
                record.Context["resourceKind"] == "Module" &&
                record.Context["moduleType"] == typeof(HangingConstructionCleanupModule).FullName &&
                record.Context["cleanupTimeout"] == TimeSpan.FromMilliseconds(100).ToString() &&
                record.Context["cleanupMayStillBeRunning"] == bool.TrueString);
        }
        finally
        {
            HangingConstructionCleanupModule.Release();
            Assert.True(SpinWait.SpinUntil(
                () => HangingConstructionCleanupModule.DisposeCompleted,
                TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public async Task OptionalDependencyCanBeMissing()
    {
        ModuleRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();

        builder.UseModule<MissingOptionalDependencyModule>();

        await using var host = builder.Build();

        await host.StartAsync();

        Assert.Contains("Optional:OnApplicationInitialization", ModuleRecorder.Calls);
    }

    [Fact]
    public async Task ModuleRegistryModulesRejectExternalListMutation()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<CoreModule>();

        await using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IModuleRegistry>();
        var modules = Assert.IsAssignableFrom<IList<ModuleDescriptor>>(registry.Modules);

        Assert.Throws<NotSupportedException>(() => modules[0] = new ModuleDescriptor(
            "Replacement",
            typeof(AsyncModule),
            version: null,
            description: null,
            []));
        Assert.Equal(typeof(CoreModule), registry.Modules[0].ModuleType);
    }

    [Fact]
    public async Task PublicModuleRegistryIsAReadOnlyViewWithoutHostControlCapabilities()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<CoreModule>();

        await using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IModuleRegistry>();

        Assert.Equal(
            ["get_Modules"],
            typeof(IModuleRegistry).GetMethods().Select(method => method.Name));
        Assert.IsNotAssignableFrom<IAsyncDisposable>(registry);
        Assert.IsNotAssignableFrom<IDisposable>(registry);
        Assert.False(typeof(IModuleLifecycleController).IsVisible);
        Assert.Null(host.Services.GetService<IModuleLifecycleController>());
        Assert.False(registry.GetType().IsVisible);

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void ModuleConfigureServicesRejectsTemporaryProviderCreationAndRecordsDiagnostic()
    {
        AssertTemporaryProviderCreationRejected<TemporaryProviderModule>();
    }

    [Fact]
    public void ModuleConfigureServicesRejectsTemporaryProviderCreationWithScopeValidation()
    {
        AssertTemporaryProviderCreationRejected<ValidateScopesTemporaryProviderModule>();
    }

    [Fact]
    public void ModuleConfigureServicesRejectsTemporaryProviderCreationWithOptions()
    {
        AssertTemporaryProviderCreationRejected<OptionsTemporaryProviderModule>();
    }

    [Fact]
    public void ModuleServiceProviderGuardRejectsNullOptions()
    {
        var context = new ServiceConfigurationContext(
            ApplicationHostTestBuilder.CreateContext(),
            new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() =>
            context.Services.BuildServiceProvider(options: null!));
    }

    [Fact]
    public async Task ModuleShutdownRunsInReverseDependencyOrder()
    {
        ModuleRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();

        builder
            .UseModule<FeatureModule>()
            .UseModule<FoundationModule>();

        await using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(
            ["foundation:init", "feature:init", "feature:shutdown", "foundation:shutdown"],
            ModuleRecorder.Calls);
    }

    [Fact]
    public void PreConfigureActionsRunInDependencyOrderBeforeConfigureServices()
    {
        ModuleRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();

        builder
            .UseModule<ApplicationOptionsModule>()
            .UseModule<FeatureOptionsModule>()
            .UseModule<FoundationOptionsModule>();

        using var host = builder.Build();

        Assert.Equal(["foundation", "feature", "application"], ModuleRecorder.Calls);
    }

    [Fact]
    public void ServiceConfigurationContextPreConfigureRejectsNullArguments()
    {
        var context = new ServiceConfigurationContext(
            ApplicationHostTestBuilder.CreateContext(),
            new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() =>
            context.PreConfigure<RecordedOptions>(null!));
        Assert.Throws<ArgumentNullException>(() =>
            context.ExecutePreConfigure<RecordedOptions>(null!));
    }

    [Fact]
    public void CapturedModuleServiceCollectionRejectsMutationAfterServiceConfigurationPhase()
    {
        CapturedServicesModule.CapturedServices = null;
        var registry = ModuleRegistry.CreateForTesting([typeof(CapturedServicesModule)]);
        var services = new ServiceCollection();

        registry.ConfigureServices(ApplicationHostTestBuilder.CreateContext(), services);
        var capturedServices = Assert.IsType<ModuleServiceCollection>(CapturedServicesModule.CapturedServices);

        Assert.Throws<InvalidOperationException>(() =>
            capturedServices.AddSingleton<CapturedService>());
    }

    private interface ICoreService;

    private sealed class CoreService : ICoreService;

    private sealed class CoreModule : RecordingModule
    {
        public CoreModule()
            : base("Core")
        {
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            base.ConfigureServices(context);
            context.Services.AddSingleton<ICoreService, CoreService>();
        }
    }

    [DependsOn(typeof(CoreModule))]
    private sealed class AppModule : RecordingModule
    {
        public AppModule()
            : base("App")
        {
        }
    }

    [DependsOn(typeof(CoreModule))]
    private sealed class MissingRequiredDependencyModule : ModuleBase;

    [DependsOn(typeof(CoreModule), Optional = true)]
    private sealed class MissingOptionalDependencyModule : RecordingModule
    {
        public MissingOptionalDependencyModule()
            : base("Optional")
        {
        }
    }

    private sealed class AsyncModule : ModuleBase
    {
        public override async ValueTask OnApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            ModuleRecorder.Record("Async:OnApplicationInitialization.Start");
            await Task.Yield();
            ModuleRecorder.Record("Async:OnApplicationInitialization.End");
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken)
        {
            ModuleRecorder.Record("Async:OnApplicationShutdown.Start");
            await Task.Yield();
            ModuleRecorder.Record("Async:OnApplicationShutdown.End");
        }
    }

    private sealed class TemporaryProviderModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.BuildServiceProvider();
        }
    }

    private sealed class ValidateScopesTemporaryProviderModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.BuildServiceProvider(validateScopes: true);
        }
    }

    private sealed class OptionsTemporaryProviderModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }
    }

    private sealed class FoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            ModuleRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ModuleRecorder.Record("foundation:shutdown");
        }
    }

    [DependsOn(typeof(FoundationModule))]
    private sealed class FeatureModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            ModuleRecorder.Record("feature:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ModuleRecorder.Record("feature:shutdown");
        }
    }

    private sealed class RecordedOptions
    {
        public List<string> Calls { get; } = [];
    }

    private sealed class CapturedService;

    private sealed class DisposableBuildModule : ModuleBase, IDisposable
    {
        public static bool WasDisposed { get; private set; }

        public static void Reset() => WasDisposed = false;

        public void Dispose() => WasDisposed = true;
    }

    private sealed class ThrowingConstructionCleanupModule : ModuleBase, IDisposable
    {
        public static int DisposeCount { get; set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("construction rollback cleanup failed");
        }
    }

    [DependsOn(typeof(ThrowingConstructionCleanupModule))]
    private sealed class FailingConstructorModule : ModuleBase
    {
        public FailingConstructorModule() =>
            throw new InvalidOperationException("module construction failed");
    }

    private sealed class AsyncBuildFailureCleanupModule : ModuleBase, IAsyncDisposable
    {
        private static int _disposeCount;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static void Reset() => Volatile.Write(ref _disposeCount, 0);

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            throw new InvalidOperationException("async build failed");

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Interlocked.Increment(ref _disposeCount);
        }
    }

    private sealed class AsyncConstructionCleanupModule : ModuleBase, IAsyncDisposable
    {
        private static int _disposeCount;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static void Reset() => Volatile.Write(ref _disposeCount, 0);

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Interlocked.Increment(ref _disposeCount);
        }
    }

    [DependsOn(typeof(AsyncConstructionCleanupModule))]
    private sealed class AsyncCleanupFailingConstructorModule : ModuleBase
    {
        public AsyncCleanupFailingConstructorModule() =>
            throw new InvalidOperationException("async cleanup construction failed");
    }

    private sealed class HangingConstructionCleanupModule : ModuleBase, IAsyncDisposable
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

    [DependsOn(typeof(HangingConstructionCleanupModule))]
    private sealed class HangingCleanupFailingConstructorModule : ModuleBase
    {
        public HangingCleanupFailingConstructorModule() =>
            throw new InvalidOperationException("hanging cleanup construction failed");
    }

    private sealed class CapturedServicesModule : ModuleBase
    {
        public static ModuleServiceCollection? CapturedServices { get; set; }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            CapturedServices = context.Services;
            context.Services.AddSingleton<CapturedService>();
        }
    }

    private static BuildThreadResult RunBuildOnNonPumpingSynchronizationContext(Action build)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

            try
            {
                build();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        var completed = thread.Join(TimeSpan.FromSeconds(5));
        return new BuildThreadResult(completed, failure);
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    private sealed record BuildThreadResult(bool Completed, Exception? Failure);

    private sealed class FoundationOptionsModule : ModuleBase
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.PreConfigure<RecordedOptions>(options => options.Calls.Add("foundation"));
        }
    }

    [DependsOn(typeof(FoundationOptionsModule))]
    private sealed class FeatureOptionsModule : ModuleBase
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.PreConfigure<RecordedOptions>(options => options.Calls.Add("feature"));
        }
    }

    [DependsOn(typeof(FeatureOptionsModule))]
    private sealed class ApplicationOptionsModule : ModuleBase
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.PreConfigure<RecordedOptions>(options => options.Calls.Add("application"));
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var options = new RecordedOptions();

            context.ExecutePreConfigure(options);

            foreach (var call in options.Calls)
            {
                ModuleRecorder.Record(call);
            }
        }
    }

    private abstract class RecordingModule(string name) : ModuleBase
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            ModuleRecorder.Record($"{name}:PreConfigureServices");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            ModuleRecorder.Record($"{name}:ConfigureServices");
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            ModuleRecorder.Record($"{name}:PostConfigureServices");
        }

        public override void ConfigureContributions(ContributionConfigurationContext context)
        {
            ModuleRecorder.Record($"{name}:ConfigureContributions");
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            ModuleRecorder.RecordScope(context.ApplicationScope);
            ModuleRecorder.Record($"{name}:OnPreApplicationInitialization");
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            ModuleRecorder.RecordScope(context.ApplicationScope);
            ModuleRecorder.Record($"{name}:OnApplicationInitialization");
        }

        public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
        {
            ModuleRecorder.RecordScope(context.ApplicationScope);
            ModuleRecorder.Record($"{name}:OnPostApplicationInitialization");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ModuleRecorder.Record($"{name}:OnApplicationShutdown");
        }
    }

    private static class ModuleRecorder
    {
        private static readonly List<string> RecordedCalls = [];

        private static readonly List<LifecycleScope> RecordedApplicationScopes = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static IReadOnlyList<LifecycleScope> ApplicationScopes =>
            RecordedApplicationScopes;

        public static void Record(string call)
        {
            RecordedCalls.Add(call);
        }

        public static void RecordScope(LifecycleScope applicationScope)
        {
            RecordedApplicationScopes.Add(applicationScope);
        }

        public static void Reset()
        {
            RecordedCalls.Clear();
            RecordedApplicationScopes.Clear();
        }
    }

    private static void AssertTemporaryProviderCreationRejected<TModule>()
        where TModule : IModule, new()
    {
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();

        builder.UseModule<TModule>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("temporary service provider", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.ModuleLifecycleFailed &&
            record.Context["moduleType"] == typeof(TModule).FullName &&
            record.Context["stage"] == "ConfigureServices");
    }
}
