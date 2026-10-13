using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationRuntimeTests
{
    [Fact]
    public async Task StartAsyncCreatesPresentationScopeUnderApplicationScope()
    {
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime();

        await runtime.StartAsync(application);

        Assert.True(runtime.IsReady);
        Assert.Equal(PresentationRuntimeState.Ready, runtime.State);
        Assert.NotNull(runtime.PresentationScope);
        Assert.Equal(LifecycleScopeKind.Presentation, runtime.PresentationScope.Kind);
        Assert.Same(application, runtime.PresentationScope.Parent);
        Assert.Equal("presentation", runtime.PresentationScope.Id);
    }

    [Fact]
    public void CreateWindowScopeRequiresReadyRuntime()
    {
        var runtime = new PresentationRuntime();

        var exception = Assert.Throws<PresentationException>(() => runtime.CreateWindowScope("main"));

        Assert.Equal(PresentationError.RuntimeNotReady, exception.Error);
    }

    [Fact]
    public async Task CreateWindowScopeCreatesChildUnderPresentationScope()
    {
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime();

        await runtime.StartAsync(application);
        var window = runtime.CreateWindowScope("main");
        var presentationScope = runtime.PresentationScope!;

        Assert.Equal(LifecycleScopeKind.Window, window.Kind);
        Assert.Equal("main", window.Id);
        Assert.Same(presentationScope, window.Parent);
        Assert.Contains(window, presentationScope.Children);
    }

    [Fact]
    public async Task StopAsyncStopsPresentationScopeAndRejectsNewWindowScopes()
    {
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime();

        await runtime.StartAsync(application);
        var window = runtime.CreateWindowScope("main");
        await runtime.StopAsync();

        var exception = Assert.Throws<PresentationException>(() => runtime.CreateWindowScope("secondary"));
        var presentationScope = runtime.PresentationScope!;

        Assert.False(runtime.IsReady);
        Assert.Equal(PresentationRuntimeState.Stopped, runtime.State);
        Assert.Equal(LifecycleScopeState.Stopped, presentationScope.State);
        Assert.Equal(LifecycleScopeState.Stopped, window.State);
        Assert.Equal(PresentationError.RuntimeStopping, exception.Error);
    }

    [Fact]
    public async Task StartAsyncRecordsRuntimeReadyDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime(diagnostics);

        await runtime.StartAsync(application);

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.RuntimeReady &&
                record.ScopeId == "presentation" &&
                record.Severity == HostDiagnosticSeverity.Info);
    }

    [Fact]
    public async Task StopAsyncRecordsRuntimeStoppingDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
        var runtime = new PresentationRuntime(diagnostics);

        await runtime.StartAsync(application);
        await runtime.StopAsync();

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.RuntimeStopping &&
                record.ScopeId == "presentation" &&
                record.Severity == HostDiagnosticSeverity.Info);
    }

    [Fact]
    public async Task StopAsyncDisposesRuntimeViewModelCompositionBeforeScope()
    {
        await using var application = LifecycleScope.CreateRoot(
            LifecycleScopeKind.Application,
            "application");
        var runtime = new PresentationRuntime();
        var viewModel = new TestCompositionViewModel();

        await runtime.StartAsync(application);
        var lease = await runtime.ViewModels.AttachRootAsync(
            "shell",
            ViewModelLease.EntryOwned(viewModel));

        await runtime.StopAsync();

        Assert.True(lease.IsDisposed);
        Assert.True(viewModel.WasDeactivated);
        Assert.True(viewModel.IsDisposed);
    }

    [Fact]
    public async Task ServiceCollectionRegistersPresentationRuntime()
    {
        var services = new ServiceCollection();

        services.AddPresentationRuntime();

        await using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IPresentationRuntime>();

        Assert.IsType<PresentationRuntime>(runtime);
    }

    [Fact]
    public async Task ServiceCollectionRuntimeUsesRegisteredDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();

        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddPresentationRuntime();

        await using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IPresentationRuntime>();
        await using var application = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");

        await runtime.StartAsync(application);

        Assert.Contains(
            diagnostics.Records,
            record => record.Code == PresentationDiagnosticIds.RuntimeReady);
    }

    private sealed class TestCompositionViewModel : IActivatable, IAsyncDisposable
    {
        public bool WasDeactivated { get; private set; }

        public bool IsDisposed { get; private set; }

        public ValueTask ActivateAsync(IActivationScope scope) => ValueTask.CompletedTask;

        public ValueTask ActivateAsync(
            IActivationScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync()
        {
            WasDeactivated = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DeactivateAsync();
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
