using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

internal sealed class ModuleRegistry : IModuleLifecycleController
{
    private static readonly TimeSpan DefaultBuildCleanupTimeout = TimeSpan.FromSeconds(30);
    private readonly IReadOnlyList<ModuleEntry> _orderedEntries;
    private readonly object _syncRoot = new();
    private Task? _activePhaseTask;
    private Task? _configureContributionsTask;
    private Task? _configureServicesTask;
    private Task? _initializeTask;
    private ModuleRegistryState _state = ModuleRegistryState.Created;
    private Task? _terminalTask;

    private ModuleRegistry(IReadOnlyList<ModuleEntry> orderedEntries)
    {
        _orderedEntries = orderedEntries;
        Modules = Array.AsReadOnly(orderedEntries.Select(entry => entry.Descriptor).ToArray());
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    internal static ModuleRegistry Create(
        ValidatedModuleGraph graph,
        IHostDiagnostics? diagnostics = null,
        TimeSpan? buildCleanupTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var cleanupTimeout = buildCleanupTimeout ?? DefaultBuildCleanupTimeout;
        if (cleanupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buildCleanupTimeout),
                cleanupTimeout,
                "Build cleanup timeout must be greater than zero.");
        }

        var entries = new List<ModuleEntry>(graph.OrderedRegistrations.Count);

        try
        {
            foreach (var registration in graph.OrderedRegistrations)
            {
                var descriptor = registration.Descriptor
                    ?? throw new InvalidOperationException(
                        $"Module '{registration.ModuleType.FullName}' does not have a resolved descriptor.");
                var module = registration.Factory()
                    ?? throw new InvalidOperationException(
                        $"Module factory for '{descriptor.ModuleType.FullName}' returned null.");

                entries.Add(new ModuleEntry(descriptor, module));

                if (!descriptor.ModuleType.IsInstanceOfType(module))
                {
                    throw new InvalidOperationException(
                        $"Module factory for '{descriptor.ModuleType.FullName}' returned '{module.GetType().FullName}'.");
                }
            }

            return new ModuleRegistry(Array.AsReadOnly(entries.ToArray()));
        }
        catch (Exception exception)
        {
            var cleanupFailures = DisposeEntriesAfterBuildFailure(
                entries,
                diagnostics,
                new BuildCleanupDeadline(cleanupTimeout));
            if (cleanupFailures.Count > 0)
            {
                throw new AggregateException(
                    "Module registry creation failed and one or more module instances failed to clean up.",
                    new[] { exception }.Concat(cleanupFailures));
            }

            throw;
        }
    }

    internal static ModuleRegistry CreateForTesting(
        IReadOnlyList<Type> moduleTypes,
        IHostDiagnostics? diagnostics = null,
        TimeSpan? buildCleanupTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(moduleTypes);

        var registrations = moduleTypes
            .Select(moduleType => new ModuleRegistration(
                ModuleDescriptorFactory.CreateFromAttributes(moduleType),
                () => (IModule)Activator.CreateInstance(moduleType)!))
            .ToArray();

        return Create(
            ModuleGraphValidator.Validate(registrations),
            diagnostics,
            buildCleanupTimeout);
    }

    public void ConfigureServices(
        IApplicationContext applicationContext,
        IServiceCollection services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        RunConfigureServicesTransaction(
            applicationContext,
            services,
            TryGetDiagnostics(services),
            generatedServices: null,
            cancellationToken);
    }

    public void ConfigureServices(
        IApplicationContext applicationContext,
        IServiceCollection services,
        IHostDiagnostics diagnostics,
        Action<IServiceCollection>? generatedServices = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(diagnostics);

        RunConfigureServicesTransaction(
            applicationContext,
            services,
            diagnostics,
            generatedServices,
            cancellationToken);
    }

    private void RunConfigureServicesTransaction(
        IApplicationContext applicationContext,
        IServiceCollection services,
        IHostDiagnostics? diagnostics,
        Action<IServiceCollection>? generatedServices,
        CancellationToken cancellationToken)
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.ConfigureServices);

        DeferredLifecycleOperation? operation = null;
        Task configureServicesTask;

        lock (_syncRoot)
        {
            ThrowIfTerminatingOrDisposed();

            if (_configureServicesTask is not null)
            {
                if (!_configureServicesTask.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "Module service configuration is synchronous and does not support concurrent callers.");
                }

                configureServicesTask = _configureServicesTask;
            }
            else
            {
                EnsureCanEnterPhase(ModuleRegistryState.Created, "configure services");

                operation = new DeferredLifecycleOperation();
                _configureServicesTask = operation.Task;
                _activePhaseTask = operation.Task;
                _state = ModuleRegistryState.ConfiguringServices;
                configureServicesTask = operation.Task;
            }
        }

        if (operation is not null)
        {
            operation.Start(
                this,
                LifecycleOperationKind.ConfigureServices,
                () => RunPhaseAsync(
                    configureServicesTask,
                    ModuleRegistryState.ConfiguringServices,
                    ModuleRegistryState.ServicesConfigured,
                    () => ConfigureServicesCoreAsync(
                        applicationContext,
                        services,
                        diagnostics,
                        generatedServices,
                        cancellationToken)));
        }

        configureServicesTask.GetAwaiter().GetResult();
    }

    private Task ConfigureServicesCoreAsync(
        IApplicationContext applicationContext,
        IServiceCollection services,
        IHostDiagnostics? diagnostics,
        Action<IServiceCollection>? generatedServices,
        CancellationToken cancellationToken)
    {
        var context = new ServiceConfigurationContext(applicationContext, services);

        try
        {
            ExecuteConfigurationStage(
                context,
                diagnostics,
                "PreConfigureServices",
                static (module, moduleContext, token) =>
                    module.PreConfigureServices(moduleContext),
                cancellationToken);

            generatedServices?.Invoke(services);

            ExecuteConfigurationStage(
                context,
                diagnostics,
                "ConfigureServices",
                static (module, moduleContext, token) =>
                    module.ConfigureServices(moduleContext),
                cancellationToken);

            ExecuteConfigurationStage(
                context,
                diagnostics,
                "PostConfigureServices",
                static (module, moduleContext, token) =>
                    module.PostConfigureServices(moduleContext),
                cancellationToken);

        }
        finally
        {
            context.Services.Freeze();
        }

        return Task.CompletedTask;
    }

    public ValueTask ConfigureContributionsAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.ConfigureContributions);

        DeferredLifecycleOperation? operation = null;
        Task configureContributionsTask;

        lock (_syncRoot)
        {
            ThrowIfTerminatingOrDisposed();

            if (_configureContributionsTask is not null)
            {
                return new ValueTask(_configureContributionsTask);
            }

            EnsureCanEnterPhase(ModuleRegistryState.ServicesConfigured, "configure contributions");

            operation = new DeferredLifecycleOperation();
            _configureContributionsTask = operation.Task;
            _activePhaseTask = operation.Task;
            _state = ModuleRegistryState.ConfiguringContributions;
            configureContributionsTask = operation.Task;
        }

        operation.Start(
            this,
            LifecycleOperationKind.ConfigureContributions,
            () => RunPhaseAsync(
                configureContributionsTask,
                ModuleRegistryState.ConfiguringContributions,
                ModuleRegistryState.ContributionsConfigured,
                () => ConfigureContributionsCoreAsync(
                    applicationContext,
                    services,
                    cancellationToken)));

        return new ValueTask(configureContributionsTask);
    }

    private async Task ConfigureContributionsCoreAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var context = new ContributionConfigurationContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        foreach (var entry in _orderedEntries)
        {
            entry.RuntimeEntered = true;
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "ConfigureContributions",
                token => entry.Module.ConfigureContributionsAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

    }

    public ValueTask InitializeAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        LifecycleScope applicationScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(applicationScope);
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Initialize);

        DeferredLifecycleOperation? operation = null;
        Task initializeTask;

        lock (_syncRoot)
        {
            ThrowIfTerminatingOrDisposed();

            if (_initializeTask is not null)
            {
                return new ValueTask(_initializeTask);
            }

            EnsureCanEnterPhase(ModuleRegistryState.ContributionsConfigured, "initialize");

            operation = new DeferredLifecycleOperation();
            _initializeTask = operation.Task;
            _activePhaseTask = operation.Task;
            _state = ModuleRegistryState.Initializing;
            initializeTask = operation.Task;
        }

        operation.Start(
            this,
            LifecycleOperationKind.Initialize,
            () => RunPhaseAsync(
                initializeTask,
                ModuleRegistryState.Initializing,
                ModuleRegistryState.Initialized,
                () => InitializeCoreAsync(
                    applicationContext,
                    services,
                    applicationScope,
                    cancellationToken)));

        return new ValueTask(initializeTask);
    }

    private async Task InitializeCoreAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        LifecycleScope applicationScope,
        CancellationToken cancellationToken)
    {
        using var effectiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            applicationScope.CancellationToken);
        var effectiveCancellationToken = effectiveCancellation.Token;
        var context = new ApplicationInitializationContext(
            applicationContext,
            services,
            applicationScope);
        var diagnostics = services.GetService<IHostDiagnostics>();

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnPreApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnPreApplicationInitializationAsync(moduleContext, token),
            effectiveCancellationToken).ConfigureAwait(false);

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnApplicationInitializationAsync(moduleContext, token),
            effectiveCancellationToken).ConfigureAwait(false);

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnPostApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnPostApplicationInitializationAsync(moduleContext, token),
            effectiveCancellationToken).ConfigureAwait(false);

    }

    public ValueTask ShutdownAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Shutdown);

        DeferredLifecycleOperation? operation = null;
        Task? activePhaseTask;
        Task terminalTask;

        lock (_syncRoot)
        {
            if (_terminalTask is not null)
            {
                return new ValueTask(_terminalTask);
            }

            operation = new DeferredLifecycleOperation();
            _terminalTask = operation.Task;
            activePhaseTask = _activePhaseTask;
            _state = ModuleRegistryState.Terminating;
            terminalTask = operation.Task;
        }

        operation.Start(
            this,
            LifecycleOperationKind.Shutdown,
            () => RunTerminalAsync(
                activePhaseTask,
                () => ShutdownCoreAsync(applicationContext, services, cancellationToken)));

        return new ValueTask(terminalTask);
    }

    public ValueTask DisposeAsync() => DisposeAsyncCore(diagnostics: null);

    internal ValueTask DisposeAsync(IHostDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return DisposeAsyncCore(diagnostics);
    }

    internal void DisposeAfterBuildFailure(
        IHostDiagnostics diagnostics,
        BuildCleanupDeadline cleanupDeadline)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(cleanupDeadline);
        var failures = new List<Exception>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];
            if (entry.Disposed)
            {
                continue;
            }

            entry.Disposed = true;
            try
            {
                switch (entry.Module)
                {
                    case IAsyncDisposable asyncDisposable:
                        cleanupDeadline.Run(
                            asyncDisposable.DisposeAsync,
                            $"Module '{entry.Descriptor.ModuleType.FullName}' DisposeAsync");
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                WriteModuleFailure(diagnostics, entry, "Dispose", exception);
                AddFailure(failures, exception);
            }
        }

        lock (_syncRoot)
        {
            _state = ModuleRegistryState.Disposed;
        }

        ThrowIfFailures(failures, "One or more module instances failed to dispose during build rollback.");
    }

    private ValueTask DisposeAsyncCore(IHostDiagnostics? diagnostics)
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Dispose);

        DeferredLifecycleOperation? operation = null;
        Task? activePhaseTask;
        Task terminalTask;

        lock (_syncRoot)
        {
            if (_terminalTask is not null)
            {
                return new ValueTask(_terminalTask);
            }

            operation = new DeferredLifecycleOperation();
            _terminalTask = operation.Task;
            activePhaseTask = _activePhaseTask;
            _state = ModuleRegistryState.Terminating;
            terminalTask = operation.Task;
        }

        operation.Start(
            this,
            LifecycleOperationKind.Dispose,
            () => RunTerminalAsync(
                activePhaseTask,
                () => DisposeModulesCoreAsync(diagnostics)));

        return new ValueTask(terminalTask);
    }

    private async Task RunPhaseAsync(
        Task phaseTask,
        ModuleRegistryState executingState,
        ModuleRegistryState completedState,
        Func<Task> execute)
    {
        try
        {
            await execute().ConfigureAwait(false);

            lock (_syncRoot)
            {
                if (_state == executingState)
                {
                    _state = completedState;
                }
            }
        }
        catch
        {
            lock (_syncRoot)
            {
                if (_state == executingState)
                {
                    _state = ModuleRegistryState.Faulted;
                }
            }

            throw;
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activePhaseTask, phaseTask))
                {
                    _activePhaseTask = null;
                }
            }
        }
    }

    private async Task RunTerminalAsync(Task? activePhaseTask, Func<Task> terminate)
    {
        if (activePhaseTask is not null)
        {
            try
            {
                await activePhaseTask.ConfigureAwait(false);
            }
            catch
            {
                // The phase caller observes its own failure. Terminal cleanup still proceeds.
            }
        }

        try
        {
            await terminate().ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                _state = ModuleRegistryState.Disposed;
                _activePhaseTask = null;
            }
        }
    }

    private async Task ShutdownCoreAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        var context = new ApplicationShutdownContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];

            if (!entry.RuntimeEntered || entry.ShutdownAttempted)
            {
                continue;
            }

            entry.ShutdownAttempted = true;

            try
            {
                await InvokeModuleAsync(
                    entry,
                    diagnostics,
                    "OnApplicationShutdown",
                    token => entry.Module.OnApplicationShutdownAsync(context, token),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        try
        {
            await DisposeModulesCoreAsync(diagnostics).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        ThrowIfFailures(failures, "One or more modules failed to shut down.");
    }

    private async Task DisposeModulesCoreAsync(IHostDiagnostics? diagnostics)
    {
        var failures = new List<Exception>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];

            if (entry.Disposed)
            {
                continue;
            }

            entry.Disposed = true;

            try
            {
                switch (entry.Module)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                WriteModuleFailure(diagnostics, entry, "Dispose", exception);
                AddFailure(failures, exception);
            }
        }

        ThrowIfFailures(failures, "One or more module instances failed to dispose.");
    }

    private void ExecuteConfigurationStage(
        ServiceConfigurationContext context,
        IHostDiagnostics? diagnostics,
        string stage,
        Action<IModule, ServiceConfigurationContext, CancellationToken> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var entry in _orderedEntries)
        {
            InvokeModule(
                entry,
                diagnostics,
                stage,
                token => invoke(entry.Module, context, token),
                cancellationToken);
        }
    }

    private async ValueTask ExecuteInitializationStageAsync(
        ApplicationInitializationContext context,
        IHostDiagnostics? diagnostics,
        string stage,
        Func<IModule, ApplicationInitializationContext, CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var entry in _orderedEntries)
        {
            entry.RuntimeEntered = true;
            await InvokeModuleAsync(
                entry,
                diagnostics,
                stage,
                token => invoke(entry.Module, context, token),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IHostDiagnostics? TryGetDiagnostics(IServiceCollection services)
    {
        return services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IHostDiagnostics))
            ?.ImplementationInstance as IHostDiagnostics;
    }

    private static async ValueTask InvokeModuleAsync(
        ModuleEntry entry,
        IHostDiagnostics? diagnostics,
        string stage,
        Func<CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await invoke(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteModuleFailure(diagnostics, entry, stage, exception);
            throw;
        }
    }

    private static void InvokeModule(
        ModuleEntry entry,
        IHostDiagnostics? diagnostics,
        string stage,
        Action<CancellationToken> invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            invoke(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteModuleFailure(diagnostics, entry, stage, exception);
            throw;
        }
    }

    private static void WriteModuleFailure(
        IHostDiagnostics? diagnostics,
        ModuleEntry entry,
        string stage,
        Exception exception)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.ModuleLifecycleFailed,
                "Module lifecycle stage failed.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["moduleId"] = entry.Descriptor.Name,
                    ["moduleType"] = entry.Descriptor.ModuleType.FullName,
                    ["stage"] = stage,
                    ["exceptionType"] = exception.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics must not replace the original module failure.
        }
    }

    private void EnsureCanEnterPhase(ModuleRegistryState expectedState, string operation)
    {
        if (_state != expectedState)
        {
            throw new InvalidOperationException(
                $"Module registry cannot {operation} from state '{_state}'.");
        }
    }

    private void ThrowIfTerminatingOrDisposed()
    {
        if (_terminalTask is not null ||
            _state is ModuleRegistryState.Terminating or ModuleRegistryState.Disposed)
        {
            throw new ObjectDisposedException(nameof(ModuleRegistry));
        }
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                failures.Add(innerException);
            }

            return;
        }

        failures.Add(exception);
    }

    private static void ThrowIfFailures(IReadOnlyCollection<Exception> failures, string message)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException(message, failures);
        }
    }

    private static IReadOnlyList<Exception> DisposeEntriesAfterBuildFailure(
        IReadOnlyList<ModuleEntry> entries,
        IHostDiagnostics? diagnostics,
        BuildCleanupDeadline cleanupDeadline)
    {
        var failures = new List<Exception>();

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];

            try
            {
                switch (entry.Module)
                {
                    case IAsyncDisposable asyncDisposable:
                        cleanupDeadline.Run(
                            asyncDisposable.DisposeAsync,
                            $"Module '{entry.Descriptor.ModuleType.FullName}' DisposeAsync");
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                WriteModuleFailure(diagnostics, entry, "ConstructionRollbackDispose", exception);
                WriteBuildCleanupFailure(
                    diagnostics,
                    entry,
                    exception,
                    cleanupDeadline.Timeout);
            }
        }

        return failures;
    }

    private static void WriteBuildCleanupFailure(
        IHostDiagnostics? diagnostics,
        ModuleEntry entry,
        Exception exception,
        TimeSpan cleanupTimeout)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            var context = new Dictionary<string, string?>
            {
                ["buildStage"] = "ModuleGraph",
                ["resourceKind"] = "Module",
                ["moduleId"] = entry.Descriptor.Name,
                ["moduleType"] = entry.Descriptor.ModuleType.FullName,
                ["exceptionType"] = exception.GetType().FullName,
                ["details"] = exception.Message,
            };
            if (exception is AsyncCleanupTimeoutException timeoutException)
            {
                context["cleanupTimeout"] = cleanupTimeout.ToString();
                context["remainingWaitTimeout"] = timeoutException.WaitTimeout.ToString();
                context["cleanupStarted"] = timeoutException.CleanupStarted.ToString();
                context["cleanupMayStillBeRunning"] = timeoutException.CleanupStarted.ToString();
            }

            diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.HostBuildCleanupFailed,
                "A module failed to clean up after module registry creation failed.",
                HostDiagnosticSeverity.Error)
            {
                Context = context,
            });
        }
        catch
        {
            // Diagnostics must not replace the original construction or cleanup failures.
        }
    }

    private sealed class ModuleEntry(ModuleDescriptor descriptor, IModule module)
    {
        public ModuleDescriptor Descriptor { get; } = descriptor;

        public IModule Module { get; } = module;

        public bool RuntimeEntered { get; set; }

        public bool ShutdownAttempted { get; set; }

        public bool Disposed { get; set; }
    }

    private enum ModuleRegistryState
    {
        Created,
        ConfiguringServices,
        ServicesConfigured,
        ConfiguringContributions,
        ContributionsConfigured,
        Initializing,
        Initialized,
        Terminating,
        Faulted,
        Disposed,
    }
}
