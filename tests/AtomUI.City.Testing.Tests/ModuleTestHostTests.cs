using AtomUI.City.Core.Modularity;
using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class ModuleTestHostTests
{
    [Fact]
    public async Task InitializeAsyncRunsModuleConfigurationStagesInRegistrationOrder()
    {
        var calls = new List<string>();
        await using var host = ModuleTestHost
            .CreateBuilder()
            .UseModule("First", new RecordingModule("First", calls))
            .UseModule("Second", new RecordingModule("Second", calls))
            .Build();

        await host.InitializeAsync();

        Assert.Equal(
            [
                "First:PreConfigureServices",
                "Second:PreConfigureServices",
                "First:ConfigureServices",
                "Second:ConfigureServices",
                "First:PostConfigureServices",
                "Second:PostConfigureServices",
                "First:ConfigureContributions",
                "Second:ConfigureContributions",
                "First:OnPreApplicationInitialization",
                "Second:OnPreApplicationInitialization",
                "First:OnApplicationInitialization",
                "Second:OnApplicationInitialization",
                "First:OnPostApplicationInitialization",
                "Second:OnPostApplicationInitialization",
            ],
            calls);
    }

    [Fact]
    public async Task InitializeAsyncRunsModulesInDependencyOrder()
    {
        var calls = new List<string>();
        await using var host = ModuleTestHost
            .CreateBuilder()
            .UseModule("App", new AppModule(calls))
            .UseModule("Core", new CoreModule(calls))
            .Build();

        await host.InitializeAsync();

        Assert.True(calls.IndexOf("Core:ConfigureServices") < calls.IndexOf("App:ConfigureServices"));
        Assert.True(calls.IndexOf("Core:OnApplicationInitialization") < calls.IndexOf("App:OnApplicationInitialization"));
    }

    [Fact]
    public void BuildFreezesModuleTestHostBuilder()
    {
        var builder = ModuleTestHost.CreateBuilder()
            .UseModule("Core", new CoreModule([]));

        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.UseModule("Other", new CoreModule([])));
        Assert.Throws<InvalidOperationException>(() => builder.UseHostProperty("next", "value"));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void BuildFailsWhenRequiredDependencyIsMissing()
    {
        var builder = ModuleTestHost.CreateBuilder()
            .UseModule("App", new DependsOnMissingModule());

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(DependsOnMissingModule), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MissingModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsyncRecordsDiagnosticsWhenModuleStageFails()
    {
        await using var host = ModuleTestHost.CreateBuilder()
            .UseModule("Failing", new FailingInitializationModule())
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.InitializeAsync());

        Assert.True(host.Host.Diagnostics.Contains("AUCTEST301"));
    }

    [Fact]
    public async Task ShutdownAsyncRecordsDiagnosticsWhenModuleStageFails()
    {
        await using var host = ModuleTestHost.CreateBuilder()
            .UseModule("Failing", new FailingShutdownModule())
            .Build();

        await host.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.ShutdownAsync());

        Assert.True(host.Host.Diagnostics.Contains("AUCTEST302"));
    }

    [Fact]
    public async Task InitializeAsyncPassesCancellationTokenToModules()
    {
        var module = new CancellationObservingModule();
        using var cancellation = new CancellationTokenSource();
        await using var host = ModuleTestHost.CreateBuilder()
            .UseModule("Cancellable", module)
            .Build();

        await host.InitializeAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, module.ObservedToken);
    }

    [Fact]
    public async Task InitializeAsyncObservesCanceledTokenWithoutRecordingFailureDiagnostic()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var host = ModuleTestHost.CreateBuilder()
            .UseModule("Cancellable", new CancellationObservingModule())
            .Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await host.InitializeAsync(cancellation.Token));

        Assert.False(host.Host.Diagnostics.Contains("AUCTEST301"));
    }

    [Fact]
    public async Task ShutdownAsyncRunsModulesInReverseRegistrationOrder()
    {
        var calls = new List<string>();
        await using var host = ModuleTestHost
            .CreateBuilder()
            .UseModule("First", new RecordingModule("First", calls))
            .UseModule("Second", new RecordingModule("Second", calls))
            .Build();

        await host.InitializeAsync();
        await host.ShutdownAsync();

        Assert.Equal(
            [
                "Second:OnApplicationShutdown",
                "First:OnApplicationShutdown",
            ],
            calls.TakeLast(2));
    }

    [Fact]
    public void ModulesExposeStableTestRecords()
    {
        using var host = ModuleTestHost
            .CreateBuilder()
            .UseModule("Sample", new RecordingModule("Sample", []))
            .Build();

        var record = Assert.Single(host.Modules);

        Assert.Equal("Sample", record.Name);
        Assert.Equal(typeof(RecordingModule), record.Module.GetType());
    }

    [Fact]
    public void ModulesRejectExternalMutation()
    {
        using var host = ModuleTestHost
            .CreateBuilder()
            .UseModule("Sample", new RecordingModule("Sample", []))
            .Build();

        var modules = Assert.IsAssignableFrom<IList<ModuleTestRecord>>(host.Modules);

        Assert.Throws<NotSupportedException>(() => modules[0] = new ModuleTestRecord("Other", new RecordingModule("Other", [])));
        Assert.Equal("Sample", host.Modules[0].Name);
    }

    private class RecordingModule : ModuleBase
    {
        private readonly List<string> _calls;
        private readonly string _name;

        public RecordingModule(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            _calls.Add($"{_name}:PreConfigureServices");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            _calls.Add($"{_name}:ConfigureServices");
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            _calls.Add($"{_name}:PostConfigureServices");
        }

        public override void ConfigureContributions(ContributionConfigurationContext context)
        {
            _calls.Add($"{_name}:ConfigureContributions");
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            _calls.Add($"{_name}:OnPreApplicationInitialization");
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            _calls.Add($"{_name}:OnApplicationInitialization");
        }

        public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
        {
            _calls.Add($"{_name}:OnPostApplicationInitialization");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            _calls.Add($"{_name}:OnApplicationShutdown");
        }
    }

    private sealed class CoreModule(List<string> calls) : RecordingModule("Core", calls);

    [DependsOn(typeof(CoreModule))]
    private sealed class AppModule(List<string> calls) : RecordingModule("App", calls);

    [DependsOn(typeof(MissingModule))]
    private sealed class DependsOnMissingModule : ModuleBase;

    private sealed class MissingModule : ModuleBase;

    private sealed class FailingInitializationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            throw new InvalidOperationException("module initialization failed");
        }
    }

    private sealed class FailingShutdownModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            throw new InvalidOperationException("module shutdown failed");
        }
    }

    private sealed class CancellationObservingModule : ModuleBase
    {
        public CancellationToken ObservedToken { get; private set; }

        public override ValueTask OnApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
