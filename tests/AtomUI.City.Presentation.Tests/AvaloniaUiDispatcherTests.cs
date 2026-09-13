using System.Globalization;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Presentation;
using AtomUI.City.Core.Threading;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class AvaloniaUiDispatcherTests
{
    [Fact]
    public void ServiceCollectionRegistersAvaloniaUiDispatcherAsUiDispatcher()
    {
        var services = new ServiceCollection();

        services.AddAvaloniaUiDispatcher();

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IUiDispatcher>();

        Assert.IsType<AvaloniaUiDispatcher>(dispatcher);
    }

    [Fact]
    public void ServiceCollectionDoesNotReplaceExistingUiDispatcher()
    {
        var existingDispatcher = new InlineUiDispatcher();
        var services = new ServiceCollection();

        services.AddSingleton<IUiDispatcher>(existingDispatcher);
        services.AddAvaloniaUiDispatcher();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existingDispatcher, provider.GetRequiredService<IUiDispatcher>());
    }

    [Fact]
    public void ServiceCollectionReplacesCoreUnavailableDispatcherFallback()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUiDispatcher, UnavailableUiDispatcher>();

        services.AddAvaloniaUiDispatcher();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AvaloniaUiDispatcher>(provider.GetRequiredService<IUiDispatcher>());
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUiDispatcher) &&
                          descriptor.ImplementationType == typeof(UnavailableUiDispatcher));
    }

    [Fact]
    public void ServiceCollectionPreservesCustomDispatcherAlongsideCoreFallback()
    {
        var existingDispatcher = new InlineUiDispatcher();
        var services = new ServiceCollection();
        services.AddSingleton<IUiDispatcher, UnavailableUiDispatcher>();
        services.AddSingleton<IUiDispatcher>(existingDispatcher);

        services.AddAvaloniaUiDispatcher();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existingDispatcher, provider.GetRequiredService<IUiDispatcher>());
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUiDispatcher) &&
                          descriptor.ImplementationType == typeof(UnavailableUiDispatcher));
    }

    [Fact]
    public async Task ServiceCollectionDispatcherUsesRegisteredPresentationRuntime()
    {
        var services = new ServiceCollection();

        services.AddPresentationRuntime();
        services.AddAvaloniaUiDispatcher();

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IUiDispatcher>();

        var exception = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => { }).AsTask());

        Assert.Equal(PresentationError.RuntimeNotReady, exception.Error);
    }

    [Fact]
    public void CheckAccessDelegatesToAvaloniaDispatcher()
    {
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);

        Assert.Equal(avaloniaDispatcher.CheckAccess(), dispatcher.CheckAccess());
    }

    [Fact]
    public async Task InvokeAsyncRunsCallbacksWhenDispatcherOwnsCurrentThread()
    {
        var dispatcher = new AvaloniaUiDispatcher(Dispatcher.CurrentDispatcher);
        var wasCalled = false;

        await dispatcher.InvokeAsync(() => wasCalled = true);
        var value = await dispatcher.InvokeAsync(() => 42);

        Assert.True(wasCalled);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task PostAsyncRunsCallbackWhenDispatcherOwnsCurrentThread()
    {
        var dispatcher = new AvaloniaUiDispatcher(Dispatcher.CurrentDispatcher);
        var callCount = 0;

        await dispatcher.PostAsync(_ =>
        {
            callCount++;
            return ValueTask.CompletedTask;
        });

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void PostAsyncUsesAvaloniaInvokeAsyncForQueuedCallbacks()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AtomUI.City.Presentation",
                "AvaloniaUiDispatcher.cs"));
        var postAsyncBodyStart = source.IndexOf("public ValueTask PostAsync", StringComparison.Ordinal);
        var postAsyncBodyEnd = source.IndexOf(
            "private DispatcherOperationContext CreateOperationContext",
            StringComparison.Ordinal);
        var postAsyncBody = source[postAsyncBodyStart..postAsyncBodyEnd];

        Assert.Contains("_dispatcher.InvokeAsync", postAsyncBody);
        Assert.DoesNotContain("_dispatcher.Post", postAsyncBody);
    }

    [Fact]
    public async Task InvokeAsyncMarshalsBackgroundCallbackToDispatcherThread()
    {
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);
        var dispatcherThreadId = Environment.CurrentManagedThreadId;
        var callbackThreadId = -1;

        var operation = Task.Run(
            () => dispatcher
                .InvokeAsync(() => callbackThreadId = Environment.CurrentManagedThreadId)
                .AsTask());

        PumpDispatcherUntilComplete(avaloniaDispatcher, operation);
        await operation;

        Assert.Equal(dispatcherThreadId, callbackThreadId);
    }

    [Fact]
    public async Task PostAsyncHonorsCancellationBeforeQueuedBackgroundCallbackRuns()
    {
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);
        using var cancellation = new CancellationTokenSource();
        using var queued = new ManualResetEventSlim();
        var wasCalled = false;

        var operation = Task.Run(
            async () =>
            {
                var queuedOperation = dispatcher
                    .PostAsync(
                        _ =>
                        {
                            wasCalled = true;
                            return ValueTask.CompletedTask;
                        },
                        cancellation.Token)
                    .AsTask();

                queued.Set();
                await queuedOperation;
            });

        Assert.True(queued.Wait(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();
        PumpDispatcherUntilComplete(avaloniaDispatcher, operation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task PostAsyncCompletesCanceledWhenQueuedBackgroundCallbackIsCanceledBeforePump()
    {
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);
        using var cancellation = new CancellationTokenSource();
        using var queued = new ManualResetEventSlim();
        var wasCalled = false;

        var operation = Task.Run(
            () =>
            {
                var queuedOperation = dispatcher
                    .PostAsync(
                        _ =>
                        {
                            wasCalled = true;
                            return ValueTask.CompletedTask;
                        },
                        cancellation.Token)
                    .AsTask();

                queued.Set();
                return queuedOperation;
            });

        Assert.True(queued.Wait(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(wasCalled);

        PumpDispatcherUntilComplete(avaloniaDispatcher, Task.CompletedTask);
    }

    [Fact]
    public async Task OperationsHonorPreCanceledToken()
    {
        var dispatcher = new AvaloniaUiDispatcher(Dispatcher.CurrentDispatcher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var wasCalled = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(() => wasCalled = true, cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(() => 42, cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.PostAsync(
                _ =>
                {
                    wasCalled = true;
                    return ValueTask.CompletedTask;
                },
                cancellation.Token).AsTask());

        Assert.False(wasCalled);
    }

    [Fact]
    public async Task OperationsRejectWhenPresentationRuntimeIsNotReady()
    {
        var runtime = new PresentationRuntime();
        var dispatcher = new AvaloniaUiDispatcher(Dispatcher.CurrentDispatcher, runtime);
        var wasCalled = false;

        var invokeException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => wasCalled = true).AsTask());
        var resultException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => 42).AsTask());
        var postException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.PostAsync(
                _ =>
                {
                    wasCalled = true;
                    return ValueTask.CompletedTask;
                }).AsTask());

        Assert.Equal(PresentationError.RuntimeNotReady, invokeException.Error);
        Assert.Equal(PresentationError.RuntimeNotReady, resultException.Error);
        Assert.Equal(PresentationError.RuntimeNotReady, postException.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task OperationsRejectWhenPresentationRuntimeIsStopped()
    {
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime();
        await runtime.StartAsync(application);
        await runtime.StopAsync();
        var dispatcher = new AvaloniaUiDispatcher(Dispatcher.CurrentDispatcher, runtime);
        var wasCalled = false;

        var invokeException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => wasCalled = true).AsTask());
        var resultException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => 42).AsTask());
        var postException = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.PostAsync(
                _ =>
                {
                    wasCalled = true;
                    return ValueTask.CompletedTask;
                }).AsTask());

        Assert.Equal(PresentationError.RuntimeStopping, invokeException.Error);
        Assert.Equal(PresentationError.RuntimeStopping, resultException.Error);
        Assert.Equal(PresentationError.RuntimeStopping, postException.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task InvokeAsyncMapsUnavailableAvaloniaDispatcherToPresentationError()
    {
        var avaloniaDispatcher = CreateShutdownDispatcher();
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);

        var exception = await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => { }).AsTask());

        Assert.Equal(PresentationError.DispatcherUnavailable, exception.Error);
    }

    [Fact]
    public async Task RuntimeRejectedOperationsRecordDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime();
        await runtime.StartAsync(application);
        await runtime.StopAsync();
        var dispatcher = new AvaloniaUiDispatcher(
            Dispatcher.CurrentDispatcher,
            runtime,
            diagnostics);

        await Assert.ThrowsAsync<PresentationException>(
            () => dispatcher.InvokeAsync(() => { }).AsTask());

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.DispatcherOperationRejected &&
                record.ScopeId == "presentation" &&
                record.Severity == HostDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task CallbackFailuresRecordDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new AvaloniaUiDispatcher(
            Dispatcher.CurrentDispatcher,
            runtime: null,
            diagnostics);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.InvokeAsync(() => throw new InvalidOperationException("callback failed")).AsTask());

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.DispatcherCallbackFailed &&
                record.Severity == HostDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task BackgroundCallbackFailuresRecordDispatcherContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var dispatcherThreadId = Environment.CurrentManagedThreadId;
        var dispatcher = new AvaloniaUiDispatcher(
            avaloniaDispatcher,
            runtime: null,
            diagnostics);

        var operation = Task.Run(
            () => dispatcher
                .InvokeAsync(() => throw new InvalidOperationException("background callback failed"))
                .AsTask());

        PumpDispatcherUntilComplete(avaloniaDispatcher, operation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);

        var record = Assert.Single(
            diagnostics.Records,
            static record => record.Code == PresentationDiagnosticIds.DispatcherCallbackFailed);

        Assert.Equal("InvokeAsync", record.Context["targetAction"]);
        Assert.False(string.IsNullOrWhiteSpace(record.Context["operationId"]));
        Assert.Equal(
            dispatcherThreadId.ToString(CultureInfo.InvariantCulture),
            record.Context["dispatcherThreadId"]);
        Assert.NotEqual(record.Context["dispatcherThreadId"], record.Context["callingThreadId"]);
    }

    private static void PumpDispatcherUntilComplete(
        Dispatcher dispatcher,
        Task operation)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);

        while (!operation.IsCompleted && DateTimeOffset.UtcNow < timeout)
        {
            dispatcher.RunJobs(DispatcherPriority.Default);
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
        }
    }

    private static Dispatcher CreateShutdownDispatcher()
    {
        Dispatcher? dispatcher = null;
        Exception? exception = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(
            () =>
            {
                try
                {
                    dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.InvokeShutdown();
                }
                catch (Exception caught)
                {
                    exception = caught;
                }
                finally
                {
                    ready.Set();
                }
            })
        {
            IsBackground = true,
            Name = "AtomUI.City.Presentation.Tests.ShutdownDispatcher",
        };

        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        thread.Join(TimeSpan.FromSeconds(5));

        if (exception is not null)
        {
            throw exception;
        }

        return dispatcher ?? throw new InvalidOperationException("Shutdown dispatcher was not created.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            return true;
        }

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            callback();

            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            return callback(cancellationToken);
        }
    }
}
