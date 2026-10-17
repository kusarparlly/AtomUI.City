using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostIndustrialLifecycleTests
{
    [Fact]
    public async Task LifecyclePipelineWrapsHostAndModuleTransactions()
    {
        var calls = new List<string>();
        var operationIds = new Dictionary<string, string>();
        var builder = ApplicationHostTestBuilder.Create();

        builder.ConfigureLifecycle(lifecycle =>
        {
            foreach (var stage in new[]
                     {
                         LifecycleStages.ApplicationStart,
                         LifecycleStages.ModuleInitialize,
                         LifecycleStages.ModuleStart,
                         LifecycleStages.ApplicationStop,
                         LifecycleStages.ModuleStop,
                     })
            {
                lifecycle.Use(stage, async (context, next, cancellationToken) =>
                {
                    operationIds[stage.Key] = context.OperationId;
                    calls.Add($"{stage.Key}:before");
                    await next();
                    calls.Add($"{stage.Key}:after");
                });
            }
        });

        await using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<LifecyclePipeline>());

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(
            [
                "Application.Start:before",
                "Module.Initialize:before",
                "Module.Initialize:after",
                "Module.Start:before",
                "Module.Start:after",
                "Application.Start:after",
                "Application.Stop:before",
                "Module.Stop:before",
                "Module.Stop:after",
                "Application.Stop:after",
            ],
            calls);
        Assert.Equal(
            operationIds[LifecycleStages.ApplicationStart.Key],
            operationIds[LifecycleStages.ModuleInitialize.Key]);
        Assert.Equal(
            operationIds[LifecycleStages.ApplicationStart.Key],
            operationIds[LifecycleStages.ModuleStart.Key]);
        Assert.Equal(
            operationIds[LifecycleStages.ApplicationStop.Key],
            operationIds[LifecycleStages.ModuleStop.Key]);
        Assert.NotEqual(
            operationIds[LifecycleStages.ApplicationStart.Key],
            operationIds[LifecycleStages.ApplicationStop.Key]);
    }

    [Fact]
    public async Task InitializationFailureRollsBackEnteredModulesInReverseOrder()
    {
        RollbackRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<FailingApplicationModule>();
        builder.UseModule<FoundationModule>();
        var host = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Equal("module initialization failed", exception.Message);
        Assert.Equal(
            ["foundation:init", "application:init", "application:shutdown", "foundation:shutdown"],
            RollbackRecorder.Calls);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task InitializationFailureAggregatesAllRollbackFailuresAfterCleanupCompletes()
    {
        StartupRollbackFailureRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<RollbackFailingApplicationModule>();
        builder.UseModule<RollbackFailingFoundationModule>();
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StartAsync());

        Assert.Equal(
            [
                "startup initialization failed",
                "application rollback failed",
                "foundation rollback failed",
            ],
            failure.InnerExceptions.Select(exception => exception.Message));
        Assert.Equal(
            ["foundation:init", "application:init", "application:shutdown", "foundation:shutdown"],
            StartupRollbackFailureRecorder.Calls);
        var startDiagnostic = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStartFailed);
        var rollbackDiagnostics = diagnostics.Records.Where(record =>
            record.Code == HostDiagnosticIds.HostStopFailed &&
            record.Context["operation"] == "startupRollback").ToArray();
        Assert.Equal(2, rollbackDiagnostics.Length);
        Assert.All(rollbackDiagnostics, diagnostic =>
            Assert.Equal(
                startDiagnostic.Context["operationId"],
                diagnostic.Context["operationId"]));

        await host.StopAsync();
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task CanceledStartupWithRollbackFailureReturnsFaultedAggregate()
    {
        CancelableRollbackFailureModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<CancelableRollbackFailureModule>();
        var host = builder.Build();
        using var cancellation = new CancellationTokenSource();

        var startTask = host.StartAsync(cancellation.Token);
        await CancelableRollbackFailureModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => startTask);

        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerExceptions[0]);
        Assert.Equal("canceled startup rollback failed", failure.InnerExceptions[1].Message);
        Assert.True(startTask.IsFaulted);
        Assert.False(startTask.IsCanceled);

        await host.StopAsync();
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task StartupMiddlewareFailureDiagnosticsShareOperationIdWithRollback()
    {
        string? startOperationId = null;
        string? rollbackOperationId = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureLifecycle(lifecycle =>
        {
            lifecycle.Use<StartupFailureMiddleware>(
                LifecycleStages.ApplicationStart,
                (context, _, cancellationToken) =>
                {
                    startOperationId = context.OperationId;
                    return ValueTask.FromException(
                        new InvalidOperationException("startup middleware failed"));
                });
            lifecycle.Use<RollbackProbeMiddleware>(
                LifecycleStages.ApplicationStop,
                async (context, next, cancellationToken) =>
                {
                    rollbackOperationId = context.OperationId;
                    await next();
                });
        });
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        var middlewareFailure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        var hostFailure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStartFailed);

        Assert.False(string.IsNullOrWhiteSpace(startOperationId));
        Assert.Equal(startOperationId, rollbackOperationId);
        Assert.Equal(startOperationId, middlewareFailure.Context["operationId"]);
        Assert.Equal(startOperationId, hostFailure.Context["operationId"]);
        Assert.Equal(LifecycleStages.ApplicationStart, middlewareFailure.Stage);
        Assert.Equal(
            typeof(StartupFailureMiddleware).FullName,
            middlewareFailure.Context["middlewareType"]);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ShutdownContinuesAfterModuleFailuresAndStopsGenericHost()
    {
        ShutdownRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<SecondFailingShutdownModule>();
        builder.UseModule<FirstFailingShutdownModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });

        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Equal(2, failure.Flatten().InnerExceptions.Count);
        Assert.Equal(["second:shutdown", "first:shutdown"], ShutdownRecorder.Calls);
        Assert.Equal(1, hostedProbe.StopCount);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task ModuleContextsUseApplicationServiceScopeAndDisposeItOnStop()
    {
        ScopedModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ScopedModule>();
        builder.ConfigureServices(services => services.AddScoped<ScopedProbe>());

        await using var host = builder.Build();
        await host.StartAsync();

        Assert.NotNull(ScopedModule.InitializedInstance);
        Assert.False(ScopedModule.InitializedInstance.IsDisposed);

        await host.StopAsync();

        Assert.Same(ScopedModule.InitializedInstance, ScopedModule.ShutdownInstance);
        Assert.True(ScopedModule.InitializedInstance.IsDisposed);
    }

    [Fact]
    public async Task CancelingConcurrentStopWaitDoesNotCancelCleanupTransaction()
    {
        BlockingShutdownModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<BlockingShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using var host = builder.Build();
        await host.StartAsync();

        var stop = host.StopAsync();
        await BlockingShutdownModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StopAsync(cancellation.Token));

        BlockingShutdownModule.Release.SetResult();
        await stop;
        Assert.True(BlockingShutdownModule.Completed);
    }

    [Fact]
    public async Task ConcurrentStopCallersShareOnePublishedTransaction()
    {
        BlockingShutdownModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<BlockingShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        await host.StartAsync();

        var firstStop = host.StopAsync();
        await BlockingShutdownModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondStop = host.StopAsync();

        Assert.Same(firstStop, secondStop);

        BlockingShutdownModule.Release.SetResult();
        await Task.WhenAll(firstStop, secondStop);
        Assert.True(BlockingShutdownModule.Completed);
        var stopped = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStopped);
        Assert.False(string.IsNullOrWhiteSpace(stopped.Context["operationId"]));
    }

    [Fact]
    public async Task StopBeforeStartDisposesFiveModulesInReverseOrderForSixtyFourCallers()
    {
        CreatedStopRecorder.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        AddCreatedStopModules(builder);
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        var stops = Enumerable.Range(0, 64)
            .Select(_ => host.StopAsync())
            .ToArray();

        Assert.All(stops, stop => Assert.Same(stops[0], stop));
        await Task.WhenAll(stops);

        Assert.Equal(
            ["CreatedStopModule5", "CreatedStopModule4", "CreatedStopModule3", "CreatedStopModule2", "CreatedStopModule1"],
            CreatedStopRecorder.DisposeCalls);
        Assert.Equal(0, CreatedStopRecorder.ShutdownCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        Assert.Single(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostStopped);

        await host.DisposeAsync();
        Assert.Equal(5, CreatedStopRecorder.DisposeCalls.Count);
    }

    [Fact]
    public async Task StopBeforeStartCallerCancellationAndConcurrentDisposeDoNotCancelCleanup()
    {
        CreatedStopRecorder.Reset();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<CreatedStopModule1>();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, async (_, next, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                await next();
            }));
        var host = builder.Build();

        var stop = host.StopAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StopAsync(cancellation.Token));
        var dispose = host.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);

        release.TrySetResult();
        await Task.WhenAll(stop, dispose);

        Assert.Equal(["CreatedStopModule1"], CreatedStopRecorder.DisposeCalls);
        Assert.Equal(0, CreatedStopRecorder.ShutdownCount);
        Assert.Equal(LifecycleScopeState.Disposed, host.HostScope.State);
    }

    [Fact]
    public async Task StopBeforeStartAggregatesModuleDisposeFailuresAndContinuesCleanup()
    {
        CreatedStopRecorder.Reset("CreatedStopModule4", "CreatedStopModule2");
        var builder = ApplicationHostTestBuilder.Create();
        AddCreatedStopModules(builder);
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Equal(2, failure.Flatten().InnerExceptions.Count);
        Assert.Equal(
            ["CreatedStopModule5", "CreatedStopModule4", "CreatedStopModule3", "CreatedStopModule2", "CreatedStopModule1"],
            CreatedStopRecorder.DisposeCalls);
        Assert.Equal(0, CreatedStopRecorder.ShutdownCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        var stopped = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStopped);
        var stopFailed = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStopFailed);
        Assert.Equal(stopped.Context["operationId"], stopFailed.Context["operationId"]);

        var disposeFailure = await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
        Assert.Equal(2, disposeFailure.Flatten().InnerExceptions.Count);
        Assert.Equal(5, CreatedStopRecorder.DisposeCalls.Count);
    }

    [Fact]
    public async Task RecursiveStopFromStopMiddlewareFailsFastAndStillCleansUp()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next, _) =>
            {
                host!.StopAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            exception => exception is InvalidOperationException &&
                         exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal));
        Assert.Equal(1, hostedProbe.StopCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task RecursiveStopFromApplicationStoppingCallbackFailsFast()
    {
        var builder = ApplicationHostTestBuilder.Create();
        await using var host = builder.Build();
        await host.StartAsync();
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        Exception? recursiveFailure = null;
        using var registration = applicationLifetime.ApplicationStopping.Register(() =>
            recursiveFailure = Record.Exception(() => host.StopAsync().GetAwaiter().GetResult()));

        await host.StopAsync();

        var failure = Assert.IsType<InvalidOperationException>(recursiveFailure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
    }

    [Fact]
    public async Task RecursiveStartFromStartMiddlewareFailsFastAndRollsBack()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStart, (_, next, _) =>
            {
                host!.StartAsync();
                return next();
            }));
        host = builder.Build();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RecursiveDisposeFromStopMiddlewareFailsFastAndStillCleansUp()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next, _) =>
            {
                host!.DisposeAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            exception => exception is InvalidOperationException &&
                         exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal));
        Assert.Equal(1, hostedProbe.StopCount);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task RootProviderDoesNotExposeModuleLifecycleController()
    {
        ReentrantShutdownModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ReentrantShutdownModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        await host.StopAsync();

        Assert.False(ReentrantShutdownModule.ControllerWasResolvable);
        Assert.True(ReentrantShutdownModule.WasDisposed);
        Assert.Equal(1, hostedProbe.StopCount);
        await host.DisposeAsync();
    }

    [Fact]
    public async Task PublicModuleRegistryDoesNotLeakDisposalCapability()
    {
        ReentrantRegistryDisposeModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ReentrantRegistryDisposeModule>();
        await using var host = builder.Build();
        await host.StartAsync();

        await host.StopAsync();

        Assert.False(ReentrantRegistryDisposeModule.DisposalCapabilityWasExposed);
        Assert.Equal(1, ReentrantRegistryDisposeModule.DisposeCount);
    }

    [Fact]
    public async Task StopMiddlewareFailureDoesNotSkipRequiredCleanup()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use<StopFailureMiddleware>(LifecycleStages.ApplicationStop, static (_, _, _) =>
                ValueTask.FromException(new InvalidOperationException("stop middleware failed"))));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Equal(1, hostedProbe.StopCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        var middlewareFailure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        var hostFailure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStopFailed);
        Assert.Equal(LifecycleStages.ApplicationStop, middlewareFailure.Stage);
        Assert.Equal(
            typeof(StopFailureMiddleware).FullName,
            middlewareFailure.Context["middlewareType"]);
        Assert.Equal(
            middlewareFailure.Context["operationId"],
            hostFailure.Context["operationId"]);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task ShutdownTimeoutCancelsCooperativeModuleAndStillDisposesIt()
    {
        TimedShutdownModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<TimedShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(50));
        var host = builder.Build();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception is TimeoutException);
        Assert.True(TimedShutdownModule.WasDisposed);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    private static void AddCreatedStopModules(IApplicationHostBuilder builder)
    {
        builder.UseModule<CreatedStopModule1>();
        builder.UseModule<CreatedStopModule2>();
        builder.UseModule<CreatedStopModule3>();
        builder.UseModule<CreatedStopModule4>();
        builder.UseModule<CreatedStopModule5>();
    }

    [DependsOn(typeof(FoundationModule))]
    private sealed class FailingApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            RollbackRecorder.Record("application:init");
            throw new InvalidOperationException("module initialization failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            RollbackRecorder.Record("application:shutdown");
        }
    }

    private sealed class FoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            RollbackRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            RollbackRecorder.Record("foundation:shutdown");
        }
    }

    private sealed class RollbackFailingFoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            StartupRollbackFailureRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            StartupRollbackFailureRecorder.Record("foundation:shutdown");
            throw new InvalidOperationException("foundation rollback failed");
        }
    }

    [DependsOn(typeof(RollbackFailingFoundationModule))]
    private sealed class RollbackFailingApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            StartupRollbackFailureRecorder.Record("application:init");
            throw new InvalidOperationException("startup initialization failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            StartupRollbackFailureRecorder.Record("application:shutdown");
            throw new InvalidOperationException("application rollback failed");
        }
    }

    private sealed class CancelableRollbackFailureModule : ModuleBase
    {
        public static TaskCompletionSource Entered { get; private set; } = CreateCompletionSource();

        public static void Reset() => Entered = CreateCompletionSource();

        public override async ValueTask OnApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            throw new InvalidOperationException("canceled startup rollback failed");
        }

        private static TaskCompletionSource CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FirstFailingShutdownModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownRecorder.Record("first:shutdown");
            throw new InvalidOperationException("first shutdown failed");
        }
    }

    [DependsOn(typeof(FirstFailingShutdownModule))]
    private sealed class SecondFailingShutdownModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownRecorder.Record("second:shutdown");
            throw new InvalidOperationException("second shutdown failed");
        }
    }

    private sealed class ScopedModule : ModuleBase
    {
        public static ScopedProbe? InitializedInstance { get; private set; }

        public static ScopedProbe? ShutdownInstance { get; private set; }

        public static void Reset()
        {
            InitializedInstance = null;
            ShutdownInstance = null;
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            InitializedInstance = context.Services.GetRequiredService<ScopedProbe>();
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownInstance = context.Services.GetRequiredService<ScopedProbe>();
        }
    }

    private sealed class ScopedProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class HostedStopProbe : IHostedService
    {
        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingShutdownModule : ModuleBase
    {
        public static TaskCompletionSource Entered { get; private set; } = CreateCompletionSource();

        public static TaskCompletionSource Release { get; private set; } = CreateCompletionSource();

        public static bool Completed { get; private set; }

        public static void Reset()
        {
            Entered = CreateCompletionSource();
            Release = CreateCompletionSource();
            Completed = false;
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Completed = true;
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class TimedShutdownModule : ModuleBase, IDisposable
    {
        public static bool WasDisposed { get; private set; }

        public static void Reset() => WasDisposed = false;

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void Dispose() => WasDisposed = true;
    }

    private sealed class StartupFailureMiddleware;

    private sealed class RollbackProbeMiddleware;

    private sealed class StopFailureMiddleware;

    private sealed class ReentrantShutdownModule : ModuleBase, IDisposable
    {
        public static bool ControllerWasResolvable { get; private set; }

        public static bool WasDisposed { get; private set; }

        public static void Reset()
        {
            ControllerWasResolvable = false;
            WasDisposed = false;
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken)
        {
            ControllerWasResolvable = context.Services.GetService<IModuleLifecycleController>() is not null;
            await ValueTask.CompletedTask;
        }

        public void Dispose() => WasDisposed = true;
    }

    private sealed class ReentrantRegistryDisposeModule : ModuleBase, IDisposable
    {
        public static bool DisposalCapabilityWasExposed { get; private set; }

        public static int DisposeCount { get; private set; }

        public static void Reset()
        {
            DisposalCapabilityWasExposed = false;
            DisposeCount = 0;
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            DisposalCapabilityWasExposed =
                context.Services.GetRequiredService<IModuleRegistry>() is IAsyncDisposable;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private abstract class CreatedStopModuleBase : ModuleBase, IDisposable
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            CreatedStopRecorder.RecordShutdown();
        }

        public void Dispose()
        {
            CreatedStopRecorder.RecordDispose(GetType().Name);
        }
    }

    private sealed class CreatedStopModule1 : CreatedStopModuleBase;

    [DependsOn(typeof(CreatedStopModule1))]
    private sealed class CreatedStopModule2 : CreatedStopModuleBase;

    [DependsOn(typeof(CreatedStopModule2))]
    private sealed class CreatedStopModule3 : CreatedStopModuleBase;

    [DependsOn(typeof(CreatedStopModule3))]
    private sealed class CreatedStopModule4 : CreatedStopModuleBase;

    [DependsOn(typeof(CreatedStopModule4))]
    private sealed class CreatedStopModule5 : CreatedStopModuleBase;

    private static class CreatedStopRecorder
    {
        private static readonly List<string> RecordedDisposeCalls = [];
        private static readonly HashSet<string> FailingModules = new(StringComparer.Ordinal);
        private static int _shutdownCount;

        public static IReadOnlyList<string> DisposeCalls => RecordedDisposeCalls;

        public static int ShutdownCount => _shutdownCount;

        public static void Reset(params string[] failingModules)
        {
            RecordedDisposeCalls.Clear();
            FailingModules.Clear();
            foreach (var module in failingModules)
            {
                FailingModules.Add(module);
            }
            _shutdownCount = 0;
        }

        public static void RecordShutdown()
        {
            _shutdownCount++;
        }

        public static void RecordDispose(string module)
        {
            RecordedDisposeCalls.Add(module);
            if (FailingModules.Contains(module))
            {
                throw new InvalidOperationException($"{module} dispose failed");
            }
        }
    }

    private static class RollbackRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }

    private static class StartupRollbackFailureRecorder
    {
        private static readonly List<string> RecordedCalls = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static void Reset() => RecordedCalls.Clear();

        public static void Record(string call) => RecordedCalls.Add(call);
    }

    private static class ShutdownRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }
}
