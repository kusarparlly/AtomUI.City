using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostRuntimeTests
{
    [Fact]
    public async Task StartAndStopAsyncAreIdempotentForHostedServices()
    {
        var builder = ApplicationHostTestBuilder.Create();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<RuntimeProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<RuntimeProbe>());
        });

        await using var host = builder.Build();
        var probe = host.Services.GetRequiredService<RuntimeProbe>();

        await host.StartAsync();
        await host.StartAsync();
        await host.StopAsync();
        await host.StopAsync();

        Assert.Equal(1, probe.StartCount);
        Assert.Equal(1, probe.StopCount);
    }

    [Fact]
    public async Task ApplicationContextIsRegisteredOnlyByItsInterface()
    {
        var builder = ApplicationHostTestBuilder.Create();

        await using var host = builder.Build();

        Assert.Same(host.Context, host.Services.GetRequiredService<IApplicationContext>());
        Assert.Null(host.Services.GetService<ApplicationContext>());
    }

    [Fact]
    public async Task StartAfterStopIsRejectedAndDisposeStillSucceeds()
    {
        var host = ApplicationHostTestBuilder.Create().Build();

        await host.StartAsync();
        await host.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        await host.DisposeAsync();
    }

    [Fact]
    public async Task StopBeforeStartPermanentlyClosesAndCleansBuildResources()
    {
        UnstartedDisposableModule.Reset();
        var stopMiddlewareCount = 0;
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<UnstartedDisposableModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<RuntimeProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<RuntimeProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, async (_, next, _) =>
            {
                Interlocked.Increment(ref stopMiddlewareCount);
                await next();
            }));

        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<RuntimeProbe>();
        var child = host.HostScope.CreateChild(LifecycleScopeKind.Operation, "startup-check");

        await host.StopAsync();

        Assert.True(host.HostScope.CancellationToken.IsCancellationRequested);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        Assert.Equal(LifecycleScopeState.Stopped, child.State);
        Assert.Equal(1, stopMiddlewareCount);
        Assert.Equal(1, UnstartedDisposableModule.DisposeCount);
        Assert.Equal(0, UnstartedDisposableModule.ShutdownCount);
        Assert.Equal(0, hostedProbe.StartCount);
        Assert.Equal(0, hostedProbe.StopCount);
        var stopped = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStopped);
        Assert.Matches("^[0-9a-f]{32}$", stopped.Context["operationId"]!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        await host.DisposeAsync();
        Assert.Equal(1, UnstartedDisposableModule.DisposeCount);
    }

    [Fact]
    public async Task ApplicationContextRemainsReadableAfterHostDisposal()
    {
        var host = ApplicationHostTestBuilder.Create().Build();
        var context = host.Context;
        var applicationInstanceId = context.ApplicationInstanceId;

        await host.DisposeAsync();

        Assert.Equal(ApplicationHostTestBuilder.ApplicationId, context.ApplicationId);
        Assert.Equal(applicationInstanceId, context.ApplicationInstanceId);
        Assert.False(string.IsNullOrWhiteSpace(context.ApplicationVersion));
        Assert.True(Path.IsPathFullyQualified(context.AppDataPath));
    }

    [Fact]
    public async Task StartupFailureIsRecordedAndHostCanBeDisposed()
    {
        var builder = ApplicationHostTestBuilder.Create();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IHostedService, FailingHostedService>();
        });

        var host = builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var failure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStartFailed);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.Context["exceptionType"]);
        Assert.Equal("start", failure.Context["operation"]);
        Assert.Matches("^[0-9a-f]{32}$", failure.Context["operationId"]!);
        Assert.DoesNotContain(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task FireAndForgetStartMiddlewareIsDrainedRejectedAndRolledBack()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<BlockingStartProbe>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<BlockingStartProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use<FireAndForgetStartMiddleware>(
                LifecycleStages.ApplicationStart,
                static (context, next, cancellationToken) =>
                {
                    _ = next();
                    return ValueTask.CompletedTask;
                }));
        var host = builder.Build();
        var probe = host.Services.GetRequiredService<BlockingStartProbe>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        var start = host.StartAsync();
        await probe.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(start.IsCompleted);

        probe.ReleaseStart.TrySetResult();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => start);

        Assert.Contains("must await or return next", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, probe.StartCount);
        Assert.Equal(1, probe.StopCount);
        Assert.DoesNotContain(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStarted);
        Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStartFailed);
        var middlewareFailure = Assert.Single(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        Assert.Equal(
            typeof(FireAndForgetStartMiddleware).FullName,
            middlewareFailure.Context["middlewareType"]);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    private sealed class RuntimeProbe : IHostedService
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FailingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("startup failed");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingStartProbe : IHostedService
    {
        public TaskCompletionSource StartEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStart { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            StartEntered.TrySetResult();
            await ReleaseStart.Task.WaitAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FireAndForgetStartMiddleware;

    private sealed class UnstartedDisposableModule : ModuleBase, IAsyncDisposable
    {
        public static int DisposeCount { get; private set; }

        public static int ShutdownCount { get; private set; }

        public static void Reset()
        {
            DisposeCount = 0;
            ShutdownCount = 0;
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownCount++;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
