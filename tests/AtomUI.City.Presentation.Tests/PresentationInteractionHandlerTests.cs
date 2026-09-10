using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationInteractionHandlerTests
{
    [Fact]
    public void ServiceCollectionRegistersInteractionHandlerRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUiDispatcher>(new RecordingDispatcher());

        services.AddPresentationInteractionHandlers();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IInteractionHandlerRegistry>();

        Assert.Same(provider.GetRequiredService<InteractionHandlerRegistry>(), registry);
    }

    [Fact]
    public async Task RegistryRunsHandlerOnUiDispatcherAndRecordsHandledDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new RecordingDispatcher();
        var registry = new InteractionHandlerRegistry(dispatcher, diagnostics);
        var handlerWasOnDispatcher = false;
        registry.Register<ConfirmRequest, bool>(
            (context, _) =>
            {
                handlerWasOnDispatcher = dispatcher.IsOnDispatcher;
                return ValueTask.FromResult(context.Request.Message == "Delete?");
            });

        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.Completed, result.Status);
        Assert.True(result.Value);
        Assert.True(handlerWasOnDispatcher);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.InteractionHandled &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains(typeof(ConfirmRequest).FullName!, StringComparison.Ordinal) &&
                record.Context["requestType"] == typeof(ConfirmRequest).FullName &&
                record.Context["resultType"] == typeof(bool).FullName &&
                record.Context["status"] == nameof(InteractionResultStatus.Completed));
    }

    [Fact]
    public async Task RegistryReturnsNotHandledAndRecordsDiagnosticsWhenHandlerIsMissing()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher(), diagnostics);

        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.NotHandled, result.Status);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.InteractionNotHandled &&
                record.Severity == HostDiagnosticSeverity.Warning &&
                record.Message.Contains(typeof(ConfirmRequest).FullName!, StringComparison.Ordinal) &&
                record.Context["requestType"] == typeof(ConfirmRequest).FullName &&
                record.Context["resultType"] == typeof(bool).FullName &&
                record.Context["status"] == nameof(InteractionResultStatus.NotHandled));
    }

    [Fact]
    public async Task RegistryReturnsFailedAndRecordsDiagnosticsWhenHandlerThrows()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher(), diagnostics);
        registry.Register<ConfirmRequest, bool>(
            (_, _) => throw new InvalidOperationException("interaction failed"));

        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.InteractionFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Message.Contains("interaction failed", StringComparison.Ordinal) &&
                record.Context["requestType"] == typeof(ConfirmRequest).FullName &&
                record.Context["resultType"] == typeof(bool).FullName &&
                record.Context["status"] == nameof(InteractionResultStatus.Failed) &&
                record.Context["error"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task RegistryReturnsCanceledWithoutCallingHandlerWhenTokenIsPreCanceled()
    {
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        var handlerCalled = false;
        using var cancellation = new CancellationTokenSource();
        registry.Register<ConfirmRequest, bool>(
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult(true);
            });

        await cancellation.CancelAsync();
        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"),
            cancellation.Token);

        Assert.Equal(InteractionResultStatus.Canceled, result.Status);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task RegistryRemovesHandlerWhenActivationScopeIsDisposed()
    {
        var scope = new ActivationScope();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        registry.Register<ConfirmRequest, bool>(
            (_, _) => ValueTask.FromResult(true),
            scope);

        scope.Dispose();
        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.NotHandled, result.Status);
    }

    [Fact]
    public async Task RegistryReturnsCanceledWhenActivationScopeStopsPendingHandler()
    {
        var scope = new ActivationScope();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.Register<ConfirmRequest, bool>(
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return true;
            },
            scope);

        var request = registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?")).AsTask();
        await started.Task;

        scope.Dispose();
        var result = await request;

        Assert.Equal(InteractionResultStatus.Canceled, result.Status);
    }

    [Fact]
    public async Task RegistryRevokesHandlersByPluginId()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher(), diagnostics);
        registry.Register<ConfirmRequest, bool>(
            (_, _) => ValueTask.FromResult(true),
            new InteractionHandlerRegistrationOptions
            {
                PluginId = "com.company.sales",
                ContributionId = "sales.confirmation",
            });

        var revoked = registry.RevokePlugin("com.company.sales");
        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(1, revoked);
        Assert.Equal(InteractionResultStatus.NotHandled, result.Status);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.InteractionHandlerRevoked &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains("com.company.sales", StringComparison.Ordinal) &&
                record.Message.Contains("sales.confirmation", StringComparison.Ordinal) &&
                record.Context["pluginId"] == "com.company.sales" &&
                record.Context["contributionId"] == "sales.confirmation");
    }

    [Fact]
    public async Task RegistrySelectsNearestMatchingScope()
    {
        using var activation = new ActivationScope();
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        registry.Register<ConfirmRequest, string>((_, _) => ValueTask.FromResult("presentation"));
        registry.Register<ConfirmRequest, string>(
            (_, _) => ValueTask.FromResult("window"),
            new InteractionHandlerRegistrationOptions
            {
                Scope = InteractionHandlerScope.Window,
                WindowId = "main",
            });
        registry.Register<ConfirmRequest, string>(
            (_, _) => ValueTask.FromResult("route"),
            new InteractionHandlerRegistrationOptions
            {
                Scope = InteractionHandlerScope.Route,
                WindowId = "main",
                RouteId = "orders",
            });
        registry.Register<ConfirmRequest, string>(
            (_, _) => ValueTask.FromResult("activation"),
            new InteractionHandlerRegistrationOptions
            {
                Scope = InteractionHandlerScope.Activation,
                ActivationScope = activation,
            });

        var route = await registry.HandleAsync<ConfirmRequest, string>(
            new ConfirmRequest("route"),
            new InteractionDispatchContext("main", "orders", IsModal: false));
        var nearest = await registry.HandleAsync<ConfirmRequest, string>(
            new ConfirmRequest("activation"),
            new InteractionDispatchContext("main", "orders", activation, IsModal: false));
        var otherWindow = await registry.HandleAsync<ConfirmRequest, string>(
            new ConfirmRequest("global"),
            new InteractionDispatchContext("secondary", IsModal: false));

        Assert.Equal("route", route.Value);
        Assert.Equal("activation", nearest.Value);
        Assert.Equal("presentation", otherWindow.Value);
    }

    [Fact]
    public void RegistryRejectsIncompleteScopedRegistration()
    {
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());

        Assert.Throws<ArgumentException>(
            () => registry.Register<ConfirmRequest, bool>(
                (_, _) => ValueTask.FromResult(true),
                new InteractionHandlerRegistrationOptions { Scope = InteractionHandlerScope.Window }));
        Assert.Throws<ArgumentException>(
            () => registry.Register<ConfirmRequest, bool>(
                (_, _) => ValueTask.FromResult(true),
                new InteractionHandlerRegistrationOptions { Scope = InteractionHandlerScope.Route }));
        Assert.Throws<ArgumentException>(
            () => registry.Register<ConfirmRequest, bool>(
                (_, _) => ValueTask.FromResult(true),
                new InteractionHandlerRegistrationOptions { Scope = InteractionHandlerScope.Activation }));
    }

    [Fact]
    public async Task ModalRequestsAreFifoPerWindowAndIndependentAcrossWindows()
    {
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new List<int>();
        var startsGate = new object();
        registry.Register<int, int>(
            async (context, cancellationToken) =>
            {
                lock (startsGate)
                {
                    starts.Add(context.Request);
                }

                if (context.Request == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }

                return context.Request;
            });

        var first = registry.HandleAsync<int, int>(
            1,
            new InteractionDispatchContext(WindowId: "main")).AsTask();
        await firstStarted.Task;
        var queued = registry.HandleAsync<int, int>(
            2,
            new InteractionDispatchContext(WindowId: "main")).AsTask();
        var otherWindow = registry.HandleAsync<int, int>(
            3,
            new InteractionDispatchContext(WindowId: "secondary")).AsTask();

        Assert.Equal(3, (await otherWindow).Value);
        Assert.False(queued.IsCompleted);
        releaseFirst.TrySetResult();
        await Task.WhenAll(first, queued);
        Assert.Equal([1, 3, 2], starts);
    }

    [Fact]
    public async Task RegistryRevokesHandlersByContributionId()
    {
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        registry.Register<ConfirmRequest, bool>(
            (_, _) => ValueTask.FromResult(false),
            new InteractionHandlerRegistrationOptions
            {
                PluginId = "com.company.sales",
                ContributionId = "sales.confirmation",
            });
        registry.Register<ConfirmRequest, bool>(
            (_, _) => ValueTask.FromResult(true),
            new InteractionHandlerRegistrationOptions
            {
                PluginId = "com.company.support",
                ContributionId = "support.confirmation",
            });

        var revoked = registry.RevokeContribution("support.confirmation");
        var result = await registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?"));

        Assert.Equal(1, revoked);
        Assert.Equal(InteractionResultStatus.Completed, result.Status);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task RevokingPluginCancelsPendingInteraction()
    {
        var registry = new InteractionHandlerRegistry(new RecordingDispatcher());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.Register<ConfirmRequest, bool>(
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return true;
            },
            new InteractionHandlerRegistrationOptions
            {
                PluginId = "com.company.sales",
                ContributionId = "sales.confirmation",
            });

        var request = registry.HandleAsync<ConfirmRequest, bool>(
            new ConfirmRequest("Delete?")).AsTask();
        await started.Task;

        registry.RevokePlugin("com.company.sales");
        var result = await request;

        Assert.Equal(InteractionResultStatus.Canceled, result.Status);
    }

    private readonly record struct ConfirmRequest(string Message);

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public bool IsOnDispatcher { get; private set; }

        public bool CheckAccess()
        {
            return IsOnDispatcher;
        }

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            return RunOnDispatcherAsync(
                _ =>
                {
                    callback();
                    return ValueTask.CompletedTask;
                },
                cancellationToken);
        }

        public async ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            T? result = default;
            await RunOnDispatcherAsync(
                _ =>
                {
                    result = callback();
                    return ValueTask.CompletedTask;
                },
                cancellationToken);

            return result!;
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            return RunOnDispatcherAsync(callback, cancellationToken);
        }

        private async ValueTask RunOnDispatcherAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOnDispatcher = true;

            try
            {
                await callback(cancellationToken);
            }
            finally
            {
                IsOnDispatcher = false;
            }
        }
    }
}
