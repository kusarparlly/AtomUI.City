using System.Collections.Concurrent;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class ModuleRegistryConcurrencyTests
{
    [Fact]
    public async Task SynchronousServiceConfigurationObservesCancellationBeforeModuleHook()
    {
        CancellableServiceConfigurationModule.Count = 0;
        var registry = ModuleRegistry.CreateForTesting([typeof(CancellableServiceConfigurationModule)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => registry.ConfigureServices(
            ApplicationHostTestBuilder.CreateContext(),
            new ServiceCollection(),
            cancellation.Token));
        Assert.Equal(0, CancellableServiceConfigurationModule.Count);

        await registry.DisposeAsync();
    }

    [Fact]
    public async Task ForwardPhaseConcurrencyHonorsSynchronousAndAsynchronousContracts()
    {
        GatedPhaseModule.Reset();
        var registry = ModuleRegistry.CreateForTesting([typeof(GatedPhaseModule)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(new InMemoryHostDiagnostics());

        var configureServices = Task.Factory.StartNew(
            () => registry.ConfigureServices(applicationContext, services),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await GatedPhaseModule.Services.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(configureServices.IsCompleted);
        Assert.Throws<InvalidOperationException>(() =>
            registry.ConfigureServices(applicationContext, services));
        GatedPhaseModule.Services.Release.TrySetResult();
        await configureServices;
        registry.ConfigureServices(applicationContext, services);
        Assert.Equal(1, GatedPhaseModule.Services.Count);

        await using var provider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        var configureContributions = Enumerable.Range(0, 64)
            .Select(_ => registry.ConfigureContributionsAsync(applicationContext, provider).AsTask())
            .ToArray();
        await GatedPhaseModule.Contributions.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.All(configureContributions, task => Assert.False(task.IsCompleted));
        GatedPhaseModule.Contributions.Release.TrySetResult();
        await Task.WhenAll(configureContributions);
        Assert.Equal(1, GatedPhaseModule.Contributions.Count);

        var initialize = Enumerable.Range(0, 64)
            .Select(_ => registry.InitializeAsync(applicationContext, provider, applicationScope).AsTask())
            .ToArray();
        await GatedPhaseModule.Initialization.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.All(initialize, task => Assert.False(task.IsCompleted));
        GatedPhaseModule.Initialization.Release.TrySetResult();
        await Task.WhenAll(initialize);
        Assert.Equal(1, GatedPhaseModule.Initialization.Count);

        await registry.ShutdownAsync(applicationContext, provider);
    }

    [Fact]
    public async Task InitializationRequiresOneApplicationScopeAcrossAllModuleStages()
    {
        ScopeRecordingModule.Reset();
        var registry = ModuleRegistry.CreateForTesting([typeof(ScopeRecordingModule)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        registry.ConfigureServices(applicationContext, services);
        await using var provider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        await registry.ConfigureContributionsAsync(applicationContext, provider);

        Assert.Throws<ArgumentNullException>(() =>
            registry.InitializeAsync(applicationContext, provider, null!));

        await registry.InitializeAsync(applicationContext, provider, applicationScope);

        Assert.Equal(3, ScopeRecordingModule.ApplicationScopes.Count);
        Assert.All(
            ScopeRecordingModule.ApplicationScopes,
            scope => Assert.Same(applicationScope, scope));

        await registry.ShutdownAsync(applicationContext, provider);
    }

    [Fact]
    public async Task ShutdownFirstMakesDisposeJoinTheSameTerminalTransaction()
    {
        TerminalRecorder.Reset(TerminalGate.Shutdown);
        var (registry, applicationContext, provider, applicationScope) = await CreateInitializedRegistryAsync();
        await using var providerLease = provider;
        await using var applicationScopeLease = applicationScope;

        var shutdown = registry.ShutdownAsync(applicationContext, provider).AsTask();
        await TerminalRecorder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var joiners = Enumerable.Range(0, 64)
            .Select(index => index % 2 == 0
                ? registry.ShutdownAsync(applicationContext, provider).AsTask()
                : registry.DisposeAsync().AsTask())
            .ToArray();

        Assert.All(joiners, task => Assert.False(task.IsCompleted));
        Assert.Empty(TerminalRecorder.DisposeOrder);
        TerminalRecorder.Release.TrySetResult();
        await Task.WhenAll(joiners.Append(shutdown));

        Assert.False(TerminalRecorder.OverlapDetected);
        Assert.Equal(["five", "four", "three", "two", "one"], TerminalRecorder.ShutdownOrder);
        Assert.Equal(["five", "four", "three", "two", "one"], TerminalRecorder.DisposeOrder);
    }

    [Fact]
    public async Task DisposeFirstMakesShutdownJoinWithoutRunningShutdownHooks()
    {
        TerminalRecorder.Reset(TerminalGate.Dispose);
        var (registry, applicationContext, provider, applicationScope) = await CreateInitializedRegistryAsync();
        await using var providerLease = provider;
        await using var applicationScopeLease = applicationScope;

        var dispose = registry.DisposeAsync().AsTask();
        await TerminalRecorder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var joiners = Enumerable.Range(0, 64)
            .Select(index => index % 2 == 0
                ? registry.ShutdownAsync(applicationContext, provider).AsTask()
                : registry.DisposeAsync().AsTask())
            .ToArray();

        Assert.All(joiners, task => Assert.False(task.IsCompleted));
        TerminalRecorder.Release.TrySetResult();
        await Task.WhenAll(joiners.Append(dispose));

        Assert.False(TerminalRecorder.OverlapDetected);
        Assert.Empty(TerminalRecorder.ShutdownOrder);
        Assert.Equal(["five", "four", "three", "two", "one"], TerminalRecorder.DisposeOrder);
    }

    [Fact]
    public async Task TerminalTransactionWaitsForActiveInitialization()
    {
        GatedPhaseModule.Reset();
        var registry = ModuleRegistry.CreateForTesting([typeof(GatedPhaseModule)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        var configureServices = Task.Factory.StartNew(
            () => registry.ConfigureServices(applicationContext, services),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await GatedPhaseModule.Services.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        GatedPhaseModule.Services.Release.TrySetResult();
        await configureServices;
        await using var provider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        var contributions = registry.ConfigureContributionsAsync(applicationContext, provider).AsTask();
        GatedPhaseModule.Contributions.Release.TrySetResult();
        await contributions;

        var initialization = registry.InitializeAsync(applicationContext, provider, applicationScope).AsTask();
        await GatedPhaseModule.Initialization.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var shutdown = registry.ShutdownAsync(applicationContext, provider).AsTask();

        Assert.False(shutdown.IsCompleted);
        Assert.Equal(0, GatedPhaseModule.ShutdownCount);
        Assert.Equal(0, GatedPhaseModule.DisposeCount);
        GatedPhaseModule.Initialization.Release.TrySetResult();
        await Task.WhenAll(initialization, shutdown);

        Assert.Equal(1, GatedPhaseModule.ShutdownCount);
        Assert.Equal(1, GatedPhaseModule.DisposeCount);
    }

    [Fact]
    public async Task FailedPhaseCannotBeRetriedAndStillAllowsDisposal()
    {
        FailingContributionModule.Count = 0;
        FailingContributionModule.DisposeCount = 0;
        var registry = ModuleRegistry.CreateForTesting([typeof(FailingContributionModule)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        registry.ConfigureServices(applicationContext, services);
        await using var provider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.ConfigureContributionsAsync(applicationContext, provider));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.ConfigureContributionsAsync(applicationContext, provider));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.InitializeAsync(applicationContext, provider, applicationScope));

        Assert.Equal(1, FailingContributionModule.Count);
        await registry.DisposeAsync();
        Assert.Equal(1, FailingContributionModule.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() =>
            registry.ConfigureServices(applicationContext, services));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await registry.ConfigureContributionsAsync(applicationContext, provider));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await registry.InitializeAsync(applicationContext, provider, applicationScope));
    }

    [Fact]
    public async Task CrossTerminalReentrancyFailsFastAndCleanupContinues()
    {
        ReentrantDisposeModule.Reset();
        var registry = ModuleRegistry.CreateForTesting([typeof(ReentrantDisposeModule)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        services.AddSingleton<IModuleLifecycleController>(registry);
        registry.ConfigureServices(applicationContext, services);
        await using var provider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        await registry.ConfigureContributionsAsync(applicationContext, provider);
        await registry.InitializeAsync(applicationContext, provider, applicationScope);

        await registry.ShutdownAsync(applicationContext, provider);

        var failure = Assert.IsType<InvalidOperationException>(ReentrantDisposeModule.Failure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, ReentrantDisposeModule.DisposeCount);
    }

    private static async Task<(
        ModuleRegistry Registry,
        IApplicationContext Context,
        ServiceProvider Provider,
        LifecycleScope ApplicationScope)>
        CreateInitializedRegistryAsync()
    {
        var registry = ModuleRegistry.CreateForTesting(
            [typeof(ModuleFive), typeof(ModuleThree), typeof(ModuleOne), typeof(ModuleFour), typeof(ModuleTwo)]);
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(new InMemoryHostDiagnostics());
        registry.ConfigureServices(applicationContext, services);
        var provider = services.BuildServiceProvider();
        var applicationScope = CreateApplicationScope();
        await registry.ConfigureContributionsAsync(applicationContext, provider);
        await registry.InitializeAsync(applicationContext, provider, applicationScope);
        return (registry, applicationContext, provider, applicationScope);
    }

    private static LifecycleScope CreateApplicationScope()
    {
        return LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
    }

    private sealed class PhaseGate
    {
        private int _count;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Count => Volatile.Read(ref _count);

        public async ValueTask WaitAsync()
        {
            Interlocked.Increment(ref _count);
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }

        public void Wait()
        {
            Interlocked.Increment(ref _count);
            Entered.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
        }
    }

    private sealed class ScopeRecordingModule : ModuleBase
    {
        private static readonly List<LifecycleScope> RecordedApplicationScopes = [];

        public static IReadOnlyList<LifecycleScope> ApplicationScopes => RecordedApplicationScopes;

        public static void Reset() => RecordedApplicationScopes.Clear();

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context) =>
            RecordedApplicationScopes.Add(context.ApplicationScope);

        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            RecordedApplicationScopes.Add(context.ApplicationScope);

        public override void OnPostApplicationInitialization(ApplicationInitializationContext context) =>
            RecordedApplicationScopes.Add(context.ApplicationScope);
    }

    private sealed class GatedPhaseModule : ModuleBase, IAsyncDisposable
    {
        public static PhaseGate Services { get; private set; } = new();

        public static PhaseGate Contributions { get; private set; } = new();

        public static PhaseGate Initialization { get; private set; } = new();

        public static int ShutdownCount;

        public static int DisposeCount;

        public static void Reset()
        {
            Services = new PhaseGate();
            Contributions = new PhaseGate();
            Initialization = new PhaseGate();
            ShutdownCount = 0;
            DisposeCount = 0;
        }

        public override void ConfigureServices(ServiceConfigurationContext context) => Services.Wait();

        public override ValueTask ConfigureContributionsAsync(
            ContributionConfigurationContext context,
            CancellationToken cancellationToken) => Contributions.WaitAsync();

        public override ValueTask OnApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken) => Initialization.WaitAsync();

        public override void OnApplicationShutdown(ApplicationShutdownContext context) =>
            Interlocked.Increment(ref ShutdownCount);

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref DisposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellableServiceConfigurationModule : ModuleBase
    {
        public static int Count;

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            Interlocked.Increment(ref Count);
    }

    private sealed class FailingContributionModule : ModuleBase, IDisposable
    {
        public static int Count;

        public static int DisposeCount;

        public override void ConfigureContributions(ContributionConfigurationContext context)
        {
            Interlocked.Increment(ref Count);
            throw new InvalidOperationException("contribution failed");
        }

        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    private sealed class ReentrantDisposeModule : ModuleBase, IDisposable
    {
        public static Exception? Failure;

        public static int DisposeCount;

        public static void Reset()
        {
            Failure = null;
            DisposeCount = 0;
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            try
            {
                context.Services.GetRequiredService<IModuleLifecycleController>()
                    .DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Failure = exception;
            }
        }

        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    private enum TerminalGate
    {
        Shutdown,
        Dispose,
    }

    private static class TerminalRecorder
    {
        private static int _active;
        private static TerminalGate _gate;

        public static ConcurrentQueue<string> ShutdownOrder { get; } = new();

        public static ConcurrentQueue<string> DisposeOrder { get; } = new();

        public static TaskCompletionSource Entered { get; private set; } = NewCompletion();

        public static TaskCompletionSource Release { get; private set; } = NewCompletion();

        public static bool OverlapDetected { get; private set; }

        public static void Reset(TerminalGate gate)
        {
            _gate = gate;
            _active = 0;
            OverlapDetected = false;
            ShutdownOrder.Clear();
            DisposeOrder.Clear();
            Entered = NewCompletion();
            Release = NewCompletion();
        }

        public static async ValueTask ShutdownAsync(string name)
        {
            Enter();
            try
            {
                ShutdownOrder.Enqueue(name);
                if (_gate == TerminalGate.Shutdown && name == "five")
                {
                    Entered.TrySetResult();
                    await Release.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                Exit();
            }
        }

        public static async ValueTask DisposeAsync(string name)
        {
            Enter();
            try
            {
                DisposeOrder.Enqueue(name);
                if (_gate == TerminalGate.Dispose && name == "five")
                {
                    Entered.TrySetResult();
                    await Release.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                Exit();
            }
        }

        private static void Enter()
        {
            if (Interlocked.Increment(ref _active) != 1)
            {
                OverlapDetected = true;
            }
        }

        private static void Exit() => Interlocked.Decrement(ref _active);

        private static TaskCompletionSource NewCompletion() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private abstract class TerminalModule(string name) : ModuleBase, IAsyncDisposable
    {
        public override ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken) => TerminalRecorder.ShutdownAsync(name);

        public ValueTask DisposeAsync() => TerminalRecorder.DisposeAsync(name);
    }

    private sealed class ModuleOne() : TerminalModule("one");

    [DependsOn(typeof(ModuleOne))]
    private sealed class ModuleTwo() : TerminalModule("two");

    [DependsOn(typeof(ModuleTwo))]
    private sealed class ModuleThree() : TerminalModule("three");

    [DependsOn(typeof(ModuleThree))]
    private sealed class ModuleFour() : TerminalModule("four");

    [DependsOn(typeof(ModuleFour))]
    private sealed class ModuleFive() : TerminalModule("five");
}
