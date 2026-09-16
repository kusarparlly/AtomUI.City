using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents module test host.
/// </summary>
public sealed class ModuleTestHost : IDisposable, IAsyncDisposable
{
    private readonly TestHost _host;
    private readonly ServiceCollection _services = [];
    private readonly LifecycleScope _applicationScope =
        LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "test-application");
    private ServiceProvider? _serviceProvider;
    private bool _disposed;
    private bool _initialized;
    private bool _shutdown;

    internal ModuleTestHost(TestHost host, IReadOnlyList<ModuleTestRecord> modules)
    {
        _host = host;
        Modules = Array.AsReadOnly(modules.ToArray());
    }

    /// <summary>
    /// Gets modules.
    /// </summary>
    public IReadOnlyList<ModuleTestRecord> Modules { get; }

    /// <summary>
    /// Gets host.
    /// </summary>
    public TestHost Host => _host;

    /// <summary>
    /// Executes the create builder operation.
    /// </summary>
    public static ModuleTestHostBuilder CreateBuilder()
    {
        return new ModuleTestHostBuilder();
    }

    /// <summary>
    /// Executes the initialize async operation.
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var module in Modules)
        {
            InvokeModuleStage(
                module,
                "PreConfigureServices",
                "AUCTEST301",
                () => module.Module.PreConfigureServices(
                    CreateServiceConfigurationContext()),
                cancellationToken);
        }

        foreach (var module in Modules)
        {
            InvokeModuleStage(
                module,
                "ConfigureServices",
                "AUCTEST301",
                () => module.Module.ConfigureServices(
                    CreateServiceConfigurationContext()),
                cancellationToken);
        }

        foreach (var module in Modules)
        {
            InvokeModuleStage(
                module,
                "PostConfigureServices",
                "AUCTEST301",
                () => module.Module.PostConfigureServices(
                    CreateServiceConfigurationContext()),
                cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _serviceProvider = _services.BuildServiceProvider();

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "ConfigureContributions",
                "AUCTEST301",
                () => module.Module.ConfigureContributionsAsync(
                    CreateContributionConfigurationContext(),
                    cancellationToken)).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnPreApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnPreApplicationInitializationAsync(
                    CreateApplicationInitializationContext(),
                    cancellationToken)).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnApplicationInitializationAsync(
                    CreateApplicationInitializationContext(),
                    cancellationToken)).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnPostApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnPostApplicationInitializationAsync(
                    CreateApplicationInitializationContext(),
                    cancellationToken)).ConfigureAwait(false);
        }

        _initialized = true;
    }

    /// <summary>
    /// Executes the shutdown async operation.
    /// </summary>
    public async ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_shutdown)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _shutdown = true;

        await _applicationScope.StopAsync().ConfigureAwait(false);

        if (_initialized)
        {
            for (var index = Modules.Count - 1; index >= 0; index--)
            {
                var module = Modules[index];
                await InvokeModuleStageAsync(
                    module,
                    "OnApplicationShutdown",
                    "AUCTEST302",
                    () => module.Module.OnApplicationShutdownAsync(
                        CreateApplicationShutdownContext(),
                        cancellationToken)).ConfigureAwait(false);
            }
        }

        await _host.StopAsync().ConfigureAwait(false);
        await DisposeServiceProviderAsync().ConfigureAwait(false);
        await _applicationScope.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ShutdownAsync().AsTask().GetAwaiter().GetResult();
        _host.Dispose();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await ShutdownAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    private ServiceConfigurationContext CreateServiceConfigurationContext()
    {
        return new ServiceConfigurationContext(_host.ApplicationContext, _services);
    }

    private ContributionConfigurationContext CreateContributionConfigurationContext()
    {
        return new ContributionConfigurationContext(_host.ApplicationContext, GetServiceProvider());
    }

    private ApplicationInitializationContext CreateApplicationInitializationContext()
    {
        return new ApplicationInitializationContext(
            _host.ApplicationContext,
            GetServiceProvider(),
            _applicationScope);
    }

    private ApplicationShutdownContext CreateApplicationShutdownContext()
    {
        return new ApplicationShutdownContext(_host.ApplicationContext, GetServiceProvider());
    }

    private IServiceProvider GetServiceProvider()
    {
        return _serviceProvider ?? throw new InvalidOperationException("Module test host has not been initialized.");
    }

    private async ValueTask InvokeModuleStageAsync(
        ModuleTestRecord module,
        string stage,
        string diagnosticCode,
        Func<ValueTask> invoke)
    {
        try
        {
            await invoke().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _host.Diagnostics.Add(
                diagnosticCode,
                $"Module '{module.Name}' ({module.Module.GetType().FullName}) failed during {stage}: {exception.GetType().FullName}.");

            throw;
        }
    }

    private void InvokeModuleStage(
        ModuleTestRecord module,
        string stage,
        string diagnosticCode,
        Action invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _host.Diagnostics.Add(
                diagnosticCode,
                $"Module '{module.Name}' ({module.Module.GetType().FullName}) failed during {stage}: {exception.GetType().FullName}.");

            throw;
        }
    }

    private async ValueTask DisposeServiceProviderAsync()
    {
        if (_serviceProvider is null)
        {
            return;
        }

        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        _serviceProvider = null;
    }
}
