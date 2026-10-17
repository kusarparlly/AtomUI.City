using System.Text.Json;
using System.Collections.Concurrent;
using AtomUI.City.Core.CyclicModules;
using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Core.HeadlessApp;

internal static class HeadlessTestScenarios
{
    public static async Task<int> RunAsync(string scenario)
    {
        try
        {
            object result = scenario switch
            {
                "lifecycle" => await RunLifecycleAsync(),
                "startup-failure" => await RunStartupFailureAsync(),
                "startup-rollback-failure" => await RunStartupRollbackFailureAsync(),
                "shutdown-failure" => await RunShutdownFailureAsync(),
                "lifecycle-diagnostics" => await RunLifecycleDiagnosticsAsync(),
                "run-cancellation" => await RunCancellationAsync(),
                "concurrent-stop" => await RunConcurrentStopAsync(),
                "reentrant-stop" => await RunReentrantStopAsync(),
                "service-ordering" => await RunServiceOrderingAsync(),
                "application-context" => await RunApplicationContextAsync(),
                "generated-modules" => await RunGeneratedModulesAsync(),
                "stop-before-start" => await RunStopBeforeStartAsync(),
                "configuration-freeze" => await RunConfigurationFreezeAsync(),
                "scope-dispose-race" => await RunLifecycleScopeDisposeRaceAsync(),
                "module-registry-race" => await RunModuleRegistryRaceAsync(),
                "module-registry-ownership" => await RunModuleRegistryOwnershipAsync(),
                "module-graph-cycle" => RunModuleGraphCycle(),
                "diagnostics-contract" => await RunDiagnosticsContractAsync(),
                "public-boundaries" => RunPublicBoundaries(),
                "sync-service-configuration" => await RunSynchronousServiceConfigurationAsync(),
                "build-cleanup-failure" => RunBuildCleanupFailure(),
                "build-async-cleanup-context" => RunBuildAsyncCleanupContext(),
                "build-generic-host-async-cleanup" => RunGenericHostAsyncCleanup(),
                "build-async-cleanup-timeout" => RunBuildAsyncCleanupTimeout(),
                "lifecycle-next-ownership" => await RunLifecycleNextOwnershipAsync(),
                _ => new ScenarioFailureResult(scenario, false, null, "unknown scenario"),
            };

            var json = result switch
            {
                GeneratedModulesScenarioResult generatedModules => JsonSerializer.Serialize(
                    generatedModules,
                    HeadlessJsonSerializerContext.Default.GeneratedModulesScenarioResult),
                StopBeforeStartScenarioResult stopBeforeStart => JsonSerializer.Serialize(
                    stopBeforeStart,
                    HeadlessJsonSerializerContext.Default.StopBeforeStartScenarioResult),
                ConfigurationFreezeScenarioResult configurationFreeze => JsonSerializer.Serialize(
                    configurationFreeze,
                    HeadlessJsonSerializerContext.Default.ConfigurationFreezeScenarioResult),
                LifecycleScopeDisposeRaceScenarioResult scopeDisposeRace => JsonSerializer.Serialize(
                    scopeDisposeRace,
                    HeadlessJsonSerializerContext.Default.LifecycleScopeDisposeRaceScenarioResult),
                ModuleRegistryRaceScenarioResult moduleRegistryRace => JsonSerializer.Serialize(
                    moduleRegistryRace,
                    HeadlessJsonSerializerContext.Default.ModuleRegistryRaceScenarioResult),
                ModuleRegistryOwnershipScenarioResult moduleRegistryOwnership => JsonSerializer.Serialize(
                    moduleRegistryOwnership,
                    HeadlessJsonSerializerContext.Default.ModuleRegistryOwnershipScenarioResult),
                ModuleGraphCycleScenarioResult moduleGraphCycle => JsonSerializer.Serialize(
                    moduleGraphCycle,
                    HeadlessJsonSerializerContext.Default.ModuleGraphCycleScenarioResult),
                DiagnosticsContractScenarioResult diagnosticsContract => JsonSerializer.Serialize(
                    diagnosticsContract,
                    HeadlessJsonSerializerContext.Default.DiagnosticsContractScenarioResult),
                PublicBoundariesScenarioResult publicBoundaries => JsonSerializer.Serialize(
                    publicBoundaries,
                    HeadlessJsonSerializerContext.Default.PublicBoundariesScenarioResult),
                SynchronousServiceConfigurationScenarioResult synchronousServices => JsonSerializer.Serialize(
                    synchronousServices,
                    HeadlessJsonSerializerContext.Default.SynchronousServiceConfigurationScenarioResult),
                BuildCleanupFailureScenarioResult buildCleanupFailure => JsonSerializer.Serialize(
                    buildCleanupFailure,
                    HeadlessJsonSerializerContext.Default.BuildCleanupFailureScenarioResult),
                BuildAsyncCleanupContextScenarioResult buildAsyncCleanupContext => JsonSerializer.Serialize(
                    buildAsyncCleanupContext,
                    HeadlessJsonSerializerContext.Default.BuildAsyncCleanupContextScenarioResult),
                GenericHostAsyncCleanupScenarioResult genericHostAsyncCleanup => JsonSerializer.Serialize(
                    genericHostAsyncCleanup,
                    HeadlessJsonSerializerContext.Default.GenericHostAsyncCleanupScenarioResult),
                BuildAsyncCleanupTimeoutScenarioResult asyncCleanupTimeout => JsonSerializer.Serialize(
                    asyncCleanupTimeout,
                    HeadlessJsonSerializerContext.Default.BuildAsyncCleanupTimeoutScenarioResult),
                LifecycleNextOwnershipScenarioResult nextOwnership => JsonSerializer.Serialize(
                    nextOwnership,
                    HeadlessJsonSerializerContext.Default.LifecycleNextOwnershipScenarioResult),
                LifecycleScenarioResult lifecycle => JsonSerializer.Serialize(
                    lifecycle,
                    HeadlessJsonSerializerContext.Default.LifecycleScenarioResult),
                StartupFailureScenarioResult startupFailure => JsonSerializer.Serialize(
                    startupFailure,
                    HeadlessJsonSerializerContext.Default.StartupFailureScenarioResult),
                StartupRollbackFailureScenarioResult startupRollbackFailure => JsonSerializer.Serialize(
                    startupRollbackFailure,
                    HeadlessJsonSerializerContext.Default.StartupRollbackFailureScenarioResult),
                ShutdownFailureScenarioResult shutdownFailure => JsonSerializer.Serialize(
                    shutdownFailure,
                    HeadlessJsonSerializerContext.Default.ShutdownFailureScenarioResult),
                LifecycleDiagnosticsScenarioResult lifecycleDiagnostics => JsonSerializer.Serialize(
                    lifecycleDiagnostics,
                    HeadlessJsonSerializerContext.Default.LifecycleDiagnosticsScenarioResult),
                RunCancellationScenarioResult runCancellation => JsonSerializer.Serialize(
                    runCancellation,
                    HeadlessJsonSerializerContext.Default.RunCancellationScenarioResult),
                ConcurrentStopScenarioResult concurrentStop => JsonSerializer.Serialize(
                    concurrentStop,
                    HeadlessJsonSerializerContext.Default.ConcurrentStopScenarioResult),
                ReentrantStopScenarioResult reentrantStop => JsonSerializer.Serialize(
                    reentrantStop,
                    HeadlessJsonSerializerContext.Default.ReentrantStopScenarioResult),
                ServiceOrderingScenarioResult serviceOrdering => JsonSerializer.Serialize(
                    serviceOrdering,
                    HeadlessJsonSerializerContext.Default.ServiceOrderingScenarioResult),
                ApplicationContextScenarioResult applicationContext => JsonSerializer.Serialize(
                    applicationContext,
                    HeadlessJsonSerializerContext.Default.ApplicationContextScenarioResult),
                ScenarioFailureResult failure => JsonSerializer.Serialize(
                    failure,
                    HeadlessJsonSerializerContext.Default.ScenarioFailureResult),
                _ => throw new InvalidOperationException(
                    $"Scenario '{scenario}' returned an unsupported result type."),
            };
            Console.WriteLine(json);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new ScenarioFailureResult(
                    scenario,
                    false,
                    exception.GetType().FullName,
                    exception.Message),
                HeadlessJsonSerializerContext.Default.ScenarioFailureResult));
            return 1;
        }
    }

    private static PublicBoundariesScenarioResult RunPublicBoundaries()
    {
        var unknownStageAreaRejected = Rejects<ArgumentOutOfRangeException>(() =>
            new LifecycleStage((LifecycleStageArea)int.MaxValue, "Start"));
        var unknownRootScopeKindRejected = Rejects<ArgumentOutOfRangeException>(() =>
            LifecycleScope.CreateRoot((LifecycleScopeKind)int.MaxValue, "invalid-root"));
        using var root = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "boundary-host");
        var unknownChildScopeKindRejected = Rejects<ArgumentOutOfRangeException>(() =>
            root.CreateChild((LifecycleScopeKind)int.MaxValue, "invalid-child"));
        var invalidChildWasNotAttached = root.Children.Count == 0;
        var unknownServiceLifetimeRejected = Rejects<ArgumentOutOfRangeException>(() =>
            new ServiceAttribute((ServiceLifetime)int.MaxValue));
        var nullScopedServiceTypeRejected = Rejects<ArgumentException>(() =>
            new ScopedServiceAttribute(typeof(IDisposable), null!));
        var nullExposedServiceTypeRejected = Rejects<ArgumentException>(() =>
            new ExposeServicesAttribute(typeof(IDisposable), null!));
        var emptyServiceTypeListsRemainValid =
            new ScopedServiceAttribute().ServiceTypes.Length == 0 &&
            new ExposeServicesAttribute().ServiceTypes.Length == 0;
        var nullModuleDependencyRejected = Rejects<ArgumentException>(() =>
            new ModuleDescriptor(
                "BoundaryModule",
                typeof(HeadlessFoundationModule),
                version: null,
                description: null,
                [null!]));
        var defaultLifecycleContextStageRejected = Rejects<ArgumentException>(() =>
            new LifecycleContext(default));
        var defaultLifecycleMiddlewareStageRejected = Rejects<ArgumentException>(() =>
            new LifecyclePipelineBuilder().Use(default, static (_, _, _) => default));
        var defaultDiagnosticStageRejected = Rejects<ArgumentException>(() =>
            new HostDiagnosticRecord(
                "BOUNDARY001",
                "Invalid lifecycle stage.",
                HostDiagnosticSeverity.Error,
                Stage: (LifecycleStage?)default(LifecycleStage)));
        var originalFirstStage = LifecycleStages.All[0];
        var stageList = LifecycleStages.All as IList<LifecycleStage>;
        var globalStageTableImmutable =
            LifecycleStages.All is not LifecycleStage[] &&
            stageList is not null &&
            Rejects<NotSupportedException>(() => stageList[0] = LifecycleStages.ErrorHandle) &&
            LifecycleStages.All[0] == originalFirstStage;
        var success = unknownStageAreaRejected &&
            unknownRootScopeKindRejected &&
            unknownChildScopeKindRejected &&
            invalidChildWasNotAttached &&
            unknownServiceLifetimeRejected &&
            nullScopedServiceTypeRejected &&
            nullExposedServiceTypeRejected &&
            emptyServiceTypeListsRemainValid &&
            nullModuleDependencyRejected &&
            defaultLifecycleContextStageRejected &&
            defaultLifecycleMiddlewareStageRejected &&
            defaultDiagnosticStageRejected &&
            globalStageTableImmutable;

        return new PublicBoundariesScenarioResult(
            "public-boundaries",
            success,
            unknownStageAreaRejected,
            unknownRootScopeKindRejected,
            unknownChildScopeKindRejected,
            invalidChildWasNotAttached,
            unknownServiceLifetimeRejected,
            nullScopedServiceTypeRejected,
            nullExposedServiceTypeRejected,
            emptyServiceTypeListsRemainValid,
            nullModuleDependencyRejected,
            defaultLifecycleContextStageRejected,
            defaultLifecycleMiddlewareStageRejected,
            defaultDiagnosticStageRejected,
            globalStageTableImmutable);
    }

    private static async Task<DiagnosticsContractScenarioResult> RunDiagnosticsContractAsync()
    {
        var invalidValueRejectionCount = 0;
        invalidValueRejectionCount += Rejects<ArgumentException>(() =>
            new HostDiagnosticRecord("", "Message", HostDiagnosticSeverity.Info)) ? 1 : 0;
        invalidValueRejectionCount += Rejects<ArgumentException>(() =>
            new HostDiagnosticRecord("TEST001", " ", HostDiagnosticSeverity.Info)) ? 1 : 0;
        invalidValueRejectionCount += Rejects<ArgumentOutOfRangeException>(() =>
            new HostDiagnosticRecord("TEST001", "Message", (HostDiagnosticSeverity)int.MaxValue)) ? 1 : 0;

        const int writerCount = 512;
        const int expectedAcceptedWriteCount = writerCount / 2;
        var concurrentDiagnostics = new InMemoryHostDiagnostics();
        using var start = new ManualResetEventSlim();
        using var acceptedWritesCompleted = new CountdownEvent(expectedAcceptedWriteCount);
        var completionPublished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptedWriteCount = 0;
        var rejectedWriteCount = 0;
        var writers = Enumerable.Range(0, writerCount)
            .Select(index => Task.Run(async () =>
            {
                start.Wait();

                if (index >= expectedAcceptedWriteCount)
                {
                    await completionPublished.Task;
                }

                try
                {
                    concurrentDiagnostics.Write(new HostDiagnosticRecord(
                        $"HEADLESS{index:D3}",
                        "Concurrent diagnostic",
                        HostDiagnosticSeverity.Info));
                    Interlocked.Increment(ref acceptedWriteCount);
                }
                catch (ObjectDisposedException)
                {
                    Interlocked.Increment(ref rejectedWriteCount);
                }
                finally
                {
                    if (index < expectedAcceptedWriteCount)
                    {
                        acceptedWritesCompleted.Signal();
                    }
                }
            }))
            .ToArray();
        var completers = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() =>
            {
                start.Wait();
                acceptedWritesCompleted.Wait();
                if (index % 2 == 0)
                {
                    concurrentDiagnostics.Complete();
                }
                else
                {
                    concurrentDiagnostics.Dispose();
                }

                completionPublished.TrySetResult();
            }))
            .ToArray();

        start.Set();
        await Task.WhenAll(writers.Concat(completers));
        var concurrentBoundaryPreserved =
            acceptedWriteCount + rejectedWriteCount == writerCount &&
            acceptedWriteCount == expectedAcceptedWriteCount &&
            rejectedWriteCount == writerCount - expectedAcceptedWriteCount &&
            concurrentDiagnostics.Records.Count == acceptedWriteCount;

        var host = CreateBuilder().Build();
        var hostDiagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        await host.DisposeAsync();
        var hostStoppedRecordRetained = hostDiagnostics.Records.Any(record =>
            record.Code == HostDiagnosticIds.HostStopped);
        var writeAfterHostDisposeRejected = Rejects<ObjectDisposedException>(() =>
            hostDiagnostics.Write(new HostDiagnosticRecord(
                "HEADLESS999",
                "After Host disposal",
                HostDiagnosticSeverity.Info)));
        var recordsReadableAfterHostDispose = hostDiagnostics.Records.Count > 0;
        var success = invalidValueRejectionCount == 3 &&
            concurrentBoundaryPreserved &&
            hostStoppedRecordRetained &&
            writeAfterHostDisposeRejected &&
            recordsReadableAfterHostDispose;

        return new DiagnosticsContractScenarioResult(
            "diagnostics-contract",
            success,
            invalidValueRejectionCount,
            writerCount,
            acceptedWriteCount,
            rejectedWriteCount,
            concurrentBoundaryPreserved,
            hostStoppedRecordRetained,
            writeAfterHostDisposeRejected,
            recordsReadableAfterHostDispose);
    }

    private static ModuleGraphCycleScenarioResult RunModuleGraphCycle()
    {
        CyclicModuleConstructionRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<SecondCyclicModule>();
        builder.UseModule<FirstCyclicModule>();
        string? error = null;

        try
        {
            using var host = builder.Build();
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
        }

        var completeCyclePathReported = error is not null &&
            error.Contains(typeof(FirstCyclicModule).FullName!, StringComparison.Ordinal) &&
            error.Contains(typeof(SecondCyclicModule).FullName!, StringComparison.Ordinal);
        var success = CyclicModuleConstructionRecorder.FirstCount == 0 &&
            CyclicModuleConstructionRecorder.SecondCount == 0 &&
            completeCyclePathReported;

        return new ModuleGraphCycleScenarioResult(
            "module-graph-cycle",
            success,
            CyclicModuleConstructionRecorder.FirstCount,
            CyclicModuleConstructionRecorder.SecondCount,
            completeCyclePathReported);
    }

    private static async Task<ModuleRegistryRaceScenarioResult> RunModuleRegistryRaceAsync()
    {
        const int concurrentCallerCount = 64;
        RegistryRaceRecorder.Reset(RegistryRaceGate.Shutdown);
        var shutdownFirstHost = CreateRegistryRaceHost();
        await using (shutdownFirstHost)
        {
            await shutdownFirstHost.StartAsync();
            var first = shutdownFirstHost.StopAsync();
            await RegistryRaceRecorder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var joiners = Enumerable.Range(0, concurrentCallerCount)
                .Select(_ => shutdownFirstHost.StopAsync())
                .ToArray();
            RegistryRaceRecorder.Release.TrySetResult();
            await Task.WhenAll(joiners.Append(first));
        }

        var shutdownFirstShutdownOrder = RegistryRaceRecorder.ShutdownOrder.ToArray();
        var shutdownFirstDisposeOrder = RegistryRaceRecorder.DisposeOrder.ToArray();
        var shutdownFirstOverlapDetected = RegistryRaceRecorder.OverlapDetected;

        RegistryRaceRecorder.Reset(RegistryRaceGate.Shutdown);
        var disposeFirstHost = CreateRegistryRaceHost();
        await disposeFirstHost.StartAsync();
        var firstDispose = disposeFirstHost.DisposeAsync().AsTask();
        await RegistryRaceRecorder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var disposeJoiners = Enumerable.Range(0, concurrentCallerCount)
            .Select(_ => disposeFirstHost.DisposeAsync().AsTask())
            .ToArray();
        RegistryRaceRecorder.Release.TrySetResult();
        await Task.WhenAll(disposeJoiners.Append(firstDispose));

        var disposeFirstShutdownCount = RegistryRaceRecorder.ShutdownOrder.Count;
        var disposeFirstDisposeOrder = RegistryRaceRecorder.DisposeOrder.ToArray();
        var disposeFirstOverlapDetected = RegistryRaceRecorder.OverlapDetected;
        string[] expectedOrder = ["five", "four", "three", "two", "one"];
        var success = shutdownFirstShutdownOrder.SequenceEqual(expectedOrder) &&
            shutdownFirstDisposeOrder.SequenceEqual(expectedOrder) &&
            !shutdownFirstOverlapDetected &&
            disposeFirstShutdownCount == expectedOrder.Length &&
            disposeFirstDisposeOrder.SequenceEqual(expectedOrder) &&
            !disposeFirstOverlapDetected;

        return new ModuleRegistryRaceScenarioResult(
            "module-registry-race",
            success,
            5,
            concurrentCallerCount,
            shutdownFirstShutdownOrder,
            shutdownFirstDisposeOrder,
            shutdownFirstOverlapDetected,
            disposeFirstShutdownCount,
            disposeFirstDisposeOrder,
            disposeFirstOverlapDetected);
    }

    private static async Task<ModuleRegistryOwnershipScenarioResult> RunModuleRegistryOwnershipAsync()
    {
        var builder = CreateBuilder();
        builder.UseModule<HeadlessFoundationModule>();
        await using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IModuleRegistry>();
        var publicMethods = typeof(IModuleRegistry)
            .GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var readOnlySurface = publicMethods.SequenceEqual(["get_Modules"]);
        var disposalCapabilityHidden = registry is not IAsyncDisposable && registry is not IDisposable;
        var implementationHidden = !registry.GetType().IsVisible;
        var moduleMetadataAvailable = registry.Modules.Any(module =>
            module.ModuleType == typeof(HeadlessFoundationModule));

        await host.StartAsync();
        await host.StopAsync();

        return new ModuleRegistryOwnershipScenarioResult(
            "module-registry-ownership",
            readOnlySurface && disposalCapabilityHidden && implementationHidden && moduleMetadataAvailable,
            publicMethods,
            disposalCapabilityHidden,
            implementationHidden,
            moduleMetadataAvailable);
    }

    private static IApplicationHost CreateRegistryRaceHost()
    {
        var builder = CreateBuilder();
        builder.UseModule<RegistryRaceFive>();
        builder.UseModule<RegistryRaceThree>();
        builder.UseModule<RegistryRaceOne>();
        builder.UseModule<RegistryRaceFour>();
        builder.UseModule<RegistryRaceTwo>();
        return builder.Build();
    }

    private static async Task<LifecycleScopeDisposeRaceScenarioResult> RunLifecycleScopeDisposeRaceAsync()
    {
        const int childCount = 64;
        var diagnostics = new InMemoryHostDiagnostics();
        var root = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "headless-host", diagnostics);
        var children = Enumerable.Range(0, childCount)
            .Select(index => root.CreateChild(
                LifecycleScopeKind.Operation,
                $"operation-{index}"))
            .ToArray();
        foreach (var child in children)
        {
            child.CreateChild(LifecycleScopeKind.Operation, $"{child.Id}-leaf");
        }

        var synchronousDisposeCount = 0;
        var asynchronousDisposeCount = 0;
        var registrations = children
            .Select((child, index) => root.CancellationToken.Register(() =>
            {
                if (index % 2 == 0)
                {
                    child.Dispose();
                    Interlocked.Increment(ref synchronousDisposeCount);
                }
                else
                {
                    child.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    Interlocked.Increment(ref asynchronousDisposeCount);
                }
            }))
            .ToArray();

        try
        {
            await root.StopAsync();
        }
        finally
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }
        }

        var rootStateAfterStop = root.State.ToString();
        var disposedChildCount = children.Count(child =>
            child.State == LifecycleScopeState.Disposed);
        var remainingChildCount = root.Children.Count;
        var cleanupFailureCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.LifecycleScopeCleanupFailed);
        var publicStopRejected = false;
        try
        {
            await children[0].StopAsync();
        }
        catch (ObjectDisposedException)
        {
            publicStopRejected = true;
        }

        await root.DisposeAsync();
        var rootStateAfterDispose = root.State.ToString();
        var success = synchronousDisposeCount == childCount / 2 &&
            asynchronousDisposeCount == childCount / 2 &&
            disposedChildCount == childCount &&
            remainingChildCount == 0 &&
            cleanupFailureCount == 0 &&
            rootStateAfterStop == LifecycleScopeState.Stopped.ToString() &&
            rootStateAfterDispose == LifecycleScopeState.Disposed.ToString() &&
            publicStopRejected;

        return new LifecycleScopeDisposeRaceScenarioResult(
            "scope-dispose-race",
            success,
            childCount,
            synchronousDisposeCount,
            asynchronousDisposeCount,
            disposedChildCount,
            remainingChildCount,
            cleanupFailureCount,
            rootStateAfterStop,
            rootStateAfterDispose,
            publicStopRejected);
    }

    private static async Task<ConfigurationFreezeScenarioResult> RunConfigurationFreezeAsync()
    {
        var builder = CreateBuilder();
        var sources = builder.Configuration.Sources;
        var properties = builder.Configuration.Properties;
        var configurationCollectionsWritableBeforeBuild =
            !sources.IsReadOnly &&
            !properties.IsReadOnly;
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Boxer:Mode"] = "Original",
                ["Boxer:Nested:Level"] = "One",
            });
        for (var index = 0; index < 64; index++)
        {
            builder.Configuration[$"Matrix:Owner{index}:Mode"] = "Original";
        }

        var section = builder.Configuration.GetSection("Boxer");
        var mode = section.GetSection("Mode");
        var nested = AssertSingle(section.GetChildren(), "Nested");
        var managerChild = AssertSingle(builder.Configuration.GetChildren(), "Boxer");
        var lazyChildren = section.GetChildren();
        var capturedSections = builder.Configuration
            .GetSection("Matrix")
            .GetChildren()
            .Select(owner => owner.GetSection("Mode"))
            .ToArray();
        var root = builder.Configuration.Build();
        var rootSection = root.GetSection("Boxer");
        var rootChild = AssertSingle(root.GetChildren(), "Boxer");
        var providers = root.Providers.ToArray();

        await using var host = builder.Build();
        var lazyMode = AssertSingle(lazyChildren, "Mode");
        var configurationCollectionsReadOnlyAfterBuild =
            sources.IsReadOnly &&
            properties.IsReadOnly;

        var sectionIndexerRejected = RejectsFrozenMutation(() => section["Mode"] = "Changed");
        var sectionValueRejected = RejectsFrozenMutation(() => section.Value = "Changed");
        var nestedSectionRejected = RejectsFrozenMutation(() => nested["Level"] = "Changed");
        var managerChildRejected = RejectsFrozenMutation(() => managerChild["Mode"] = "Changed");
        var lazyChildRejected = RejectsFrozenMutation(() => lazyMode.Value = "Changed");
        var rootIndexerRejected = RejectsFrozenMutation(() => root["Boxer:Mode"] = "Changed");
        var rootSectionRejected = RejectsFrozenMutation(() => rootSection["Mode"] = "Changed");
        var rootChildRejected = RejectsFrozenMutation(() => rootChild["Mode"] = "Changed");
        var rootReloadRejected = RejectsFrozenMutation(root.Reload);
        var providerSetRejected = providers.All(provider =>
            RejectsFrozenMutation(() => provider.Set("Boxer:Mode", "Changed")));
        var providerLoadRejected = providers.All(provider =>
            RejectsFrozenMutation(provider.Load));
        var rejectedSectionWriteCount = capturedSections.Count(captured =>
            RejectsFrozenMutation(() => captured.Value = "Changed"));
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var originalValuesPreserved = configuration["Boxer:Mode"] == "Original" &&
            configuration["Boxer:Nested:Level"] == "One" &&
            capturedSections.All(captured => captured.Value == "Original") &&
            mode.Value == "Original";
        var success = sectionIndexerRejected &&
            configurationCollectionsWritableBeforeBuild &&
            configurationCollectionsReadOnlyAfterBuild &&
            sectionValueRejected &&
            nestedSectionRejected &&
            managerChildRejected &&
            lazyChildRejected &&
            rootIndexerRejected &&
            rootSectionRejected &&
            rootChildRejected &&
            rootReloadRejected &&
            providerSetRejected &&
            providerLoadRejected &&
            capturedSections.Length == 64 &&
            rejectedSectionWriteCount == capturedSections.Length &&
            originalValuesPreserved;

        return new ConfigurationFreezeScenarioResult(
            "configuration-freeze",
            success,
            configurationCollectionsWritableBeforeBuild,
            configurationCollectionsReadOnlyAfterBuild,
            sectionIndexerRejected,
            sectionValueRejected,
            nestedSectionRejected,
            managerChildRejected,
            lazyChildRejected,
            rootIndexerRejected,
            rootSectionRejected,
            rootChildRejected,
            rootReloadRejected,
            providerSetRejected,
            providerLoadRejected,
            capturedSections.Length,
            rejectedSectionWriteCount,
            originalValuesPreserved);
    }

    private static async Task<StopBeforeStartScenarioResult> RunStopBeforeStartAsync()
    {
        StopBeforeStartHeadlessModule.Reset();
        var stopMiddlewareCount = 0;
        var builder = CreateBuilder();
        builder.UseModule<StopBeforeStartHeadlessModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, async (_, next, _) =>
            {
                stopMiddlewareCount++;
                await next();
            }));
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        var child = host.HostScope.CreateChild(LifecycleScopeKind.Operation, "startup-check");

        var stops = Enumerable.Range(0, 64)
            .Select(_ => host.StopAsync())
            .ToArray();
        var sharedTransaction = stops.All(stop => ReferenceEquals(stops[0], stop));
        await Task.WhenAll(stops);

        var hostScopeCanceled = host.HostScope.CancellationToken.IsCancellationRequested;
        var hostScopeStateAfterStop = host.HostScope.State.ToString();
        var childScopeStateAfterStop = child.State.ToString();
        var stoppedDiagnostics = diagnostics.Records
            .Where(record => record.Code == HostDiagnosticIds.HostStopped)
            .ToArray();
        var startRejected = false;
        try
        {
            await host.StartAsync();
        }
        catch (InvalidOperationException)
        {
            startRejected = true;
        }

        await host.DisposeAsync();
        var hostScopeStateAfterDispose = host.HostScope.State.ToString();
        var success = sharedTransaction &&
            hostScopeCanceled &&
            hostScopeStateAfterStop == LifecycleScopeState.Stopped.ToString() &&
            hostScopeStateAfterDispose == LifecycleScopeState.Disposed.ToString() &&
            childScopeStateAfterStop == LifecycleScopeState.Stopped.ToString() &&
            StopBeforeStartHeadlessModule.DisposeCount == 1 &&
            StopBeforeStartHeadlessModule.ShutdownCount == 0 &&
            hostedProbe.StartCount == 0 &&
            hostedProbe.StopCount == 0 &&
            stopMiddlewareCount == 1 &&
            stoppedDiagnostics.Length == 1 &&
            startRejected;

        return new StopBeforeStartScenarioResult(
            "stop-before-start",
            success,
            sharedTransaction,
            hostScopeCanceled,
            hostScopeStateAfterStop,
            hostScopeStateAfterDispose,
            childScopeStateAfterStop,
            StopBeforeStartHeadlessModule.DisposeCount,
            StopBeforeStartHeadlessModule.ShutdownCount,
            hostedProbe.StartCount,
            hostedProbe.StopCount,
            stopMiddlewareCount,
            stoppedDiagnostics.Length,
            stoppedDiagnostics.SingleOrDefault()?.Context["operationId"],
            startRejected);
    }

    private static IConfigurationSection AssertSingle(
        IEnumerable<IConfigurationSection> sections,
        string key)
    {
        return sections.Single(section => section.Key == key);
    }

    private static bool RejectsFrozenMutation(Action mutation)
    {
        try
        {
            mutation();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool Rejects<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static async Task<GeneratedModulesScenarioResult> RunGeneratedModulesAsync()
    {
        GeneratedHeadlessRecorder.Reset();
        var builder = CreateBuilder();
        var host = builder.Build();
        var loadedModules = host.Services
            .GetRequiredService<IModuleRegistry>()
            .Modules
            .Select(module => module.ModuleType.Name)
            .ToArray();
        var generatedReader = host.Services.GetRequiredService<IGeneratedServiceReader>();
        var generatedWriter = host.Services.GetRequiredService<IGeneratedServiceWriter>();
        var singletonMarkerFirst = host.Services.GetRequiredService<GeneratedSingletonMarkerService>();
        var singletonMarkerSecond = host.Services.GetRequiredService<GeneratedSingletonMarkerService>();
        var transientMarkerFirst = host.Services.GetRequiredService<GeneratedTransientMarkerService>();
        var transientMarkerSecond = host.Services.GetRequiredService<GeneratedTransientMarkerService>();
        var keyedFirst = host.Services.GetRequiredKeyedService<GeneratedKeyedService>("generated");
        var keyedSecond = host.Services.GetRequiredKeyedService<GeneratedKeyedService>("generated");
        bool scopedLifetime;
        using (var firstScope = host.Services.CreateScope())
        using (var secondScope = host.Services.CreateScope())
        {
            var scopedFirst = firstScope.ServiceProvider.GetRequiredService<GeneratedScopedService>();
            var scopedFirstAgain = firstScope.ServiceProvider.GetRequiredService<GeneratedScopedService>();
            var scopedSecond = secondScope.ServiceProvider.GetRequiredService<GeneratedScopedService>();
            scopedLifetime = ReferenceEquals(scopedFirst, scopedFirstAgain) &&
                !ReferenceEquals(scopedFirst, scopedSecond);
        }

        await host.StartAsync();
        await host.StopAsync();
        await host.DisposeAsync();

        return new GeneratedModulesScenarioResult(
            "generated-modules",
            true,
            GeneratedHeadlessRecorder.Calls.ToArray(),
            loadedModules,
            GeneratedUnusedModule.CreatedCount,
            ReferenceEquals(generatedReader, generatedWriter),
            ReferenceEquals(singletonMarkerFirst, singletonMarkerSecond) &&
            !ReferenceEquals(transientMarkerFirst, transientMarkerSecond) &&
            !ReferenceEquals(keyedFirst, keyedSecond) &&
            scopedLifetime);
    }

    private static async Task<LifecycleScenarioResult> RunLifecycleAsync()
    {
        HeadlessRecorder.Reset();
        ScopedHeadlessModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<HeadlessApplicationModule>();
        builder.UseModule<HeadlessFoundationModule>();
        builder.UseModule<ScopedHeadlessModule>();
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ScopedProbe>();
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
        {
            lifecycle.Use(LifecycleStages.ApplicationStart, async (_, next, _) =>
            {
                HeadlessRecorder.Record("application:start:before");
                await next();
                HeadlessRecorder.Record("application:start:after");
            });
            lifecycle.Use(LifecycleStages.ApplicationStop, async (_, next, _) =>
            {
                HeadlessRecorder.Record("application:stop:before");
                await next();
                HeadlessRecorder.Record("application:stop:after");
            });
        });

        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        await host.StartAsync();
        await host.StopAsync();
        await host.DisposeAsync();

        return new LifecycleScenarioResult(
            "lifecycle",
            true,
            HeadlessRecorder.Calls.ToArray(),
            hostedProbe.StartCount,
            hostedProbe.StopCount,
            ScopedHeadlessModule.Instance?.IsDisposed,
            host.HostScope.State.ToString(),
            host.ApplicationScope?.State.ToString(),
            diagnostics.Records.Select(record => record.Code).ToArray());
    }

    private static async Task<StartupFailureScenarioResult> RunStartupFailureAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<FailingStartupModule>();
        builder.UseModule<HeadlessFoundationModule>();
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        Exception? startupFailure = null;

        try
        {
            await host.StartAsync();
        }
        catch (Exception exception)
        {
            startupFailure = exception;
        }

        await host.StopAsync();
        await host.DisposeAsync();

        return new StartupFailureScenarioResult(
            "startup-failure",
            startupFailure is InvalidOperationException,
            startupFailure?.GetType().FullName,
            startupFailure?.Message,
            HeadlessRecorder.Calls.ToArray(),
            host.HostScope.State.ToString(),
            diagnostics.Records.Select(record => record.Code).ToArray());
    }

    private static async Task<StartupRollbackFailureScenarioResult> RunStartupRollbackFailureAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<HeadlessRollbackFailingApplicationModule>();
        builder.UseModule<HeadlessRollbackFailingFoundationModule>();
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        AggregateException? startupFailure = null;

        try
        {
            await host.StartAsync();
        }
        catch (AggregateException exception)
        {
            startupFailure = exception;
        }

        await host.StopAsync();

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Dispose joins the already faulted ModuleRegistry terminal transaction.
        }

        var failureMessages = startupFailure?.InnerExceptions
            .Select(exception => exception.Message)
            .ToArray() ?? [];
        var rollbackDiagnosticCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.HostStopFailed &&
            record.Context.TryGetValue("operation", out var operation) &&
            operation == "startupRollback");
        var success = failureMessages.SequenceEqual(
                [
                    "headless startup initialization failed",
                    "headless application rollback failed",
                    "headless foundation rollback failed",
                ]) &&
            HeadlessRecorder.Calls.SequenceEqual(
                ["rollback-foundation:init", "rollback-application:init", "rollback-application:shutdown", "rollback-foundation:shutdown"]) &&
            rollbackDiagnosticCount == 2;

        return new StartupRollbackFailureScenarioResult(
            "startup-rollback-failure",
            success,
            failureMessages,
            HeadlessRecorder.Calls.ToArray(),
            rollbackDiagnosticCount,
            host.HostScope.State.ToString());
    }

    private static async Task<ShutdownFailureScenarioResult> RunShutdownFailureAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<SecondFailingStopModule>();
        builder.UseModule<FirstFailingStopModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        await host.StartAsync();
        AggregateException? stopFailure = null;

        try
        {
            await host.StopAsync();
        }
        catch (AggregateException exception)
        {
            stopFailure = exception.Flatten();
        }

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // The repeated dispose observes the same completed cleanup transaction.
        }

        return new ShutdownFailureScenarioResult(
            "shutdown-failure",
            stopFailure?.InnerExceptions.Count == 2,
            stopFailure?.InnerExceptions.Count,
            HeadlessRecorder.Calls.ToArray(),
            hostedProbe.StopCount,
            host.HostScope.State.ToString(),
            diagnostics.Records.Select(record => record.Code).ToArray());
    }

    private static async Task<LifecycleDiagnosticsScenarioResult> RunLifecycleDiagnosticsAsync()
    {
        var builder = CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use<HeadlessStopFailureMiddleware>(
                LifecycleStages.ApplicationStop,
                static (_, _, _) => ValueTask.FromException(
                    new InvalidOperationException("headless lifecycle middleware failed"))));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        await host.StartAsync();
        AggregateException? stopFailure = null;

        try
        {
            await host.StopAsync();
        }
        catch (AggregateException exception)
        {
            stopFailure = exception.Flatten();
        }

        var hostScopeStateAfterStop = host.HostScope.State.ToString();
        var middlewareFailure = diagnostics.Records.Single(record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        var hostFailure = diagnostics.Records.Single(record =>
            record.Code == HostDiagnosticIds.HostStopFailed);

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Disposal observes the same failed stop transaction after final cleanup.
        }

        var operationId = middlewareFailure.Context["operationId"];
        var summaryOperationId = hostFailure.Context["operationId"];

        return new LifecycleDiagnosticsScenarioResult(
            "lifecycle-diagnostics",
            stopFailure?.InnerExceptions.Any(exception =>
                exception is InvalidOperationException &&
                exception.Message == "headless lifecycle middleware failed") == true &&
            hostedProbe.StopCount == 1 &&
            hostScopeStateAfterStop == LifecycleScopeState.Stopped.ToString() &&
            !string.IsNullOrWhiteSpace(operationId) &&
            operationId == summaryOperationId,
            middlewareFailure.Stage?.Key,
            middlewareFailure.Context["middlewareType"],
            operationId,
            summaryOperationId,
            middlewareFailure.Context["exceptionType"],
            hostedProbe.StopCount,
            hostScopeStateAfterStop);
    }

    private static async Task<RunCancellationScenarioResult> RunCancellationAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<CancellationModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var canceled = false;

        try
        {
            await host.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        await host.DisposeAsync();

        return new RunCancellationScenarioResult(
            "run-cancellation",
            canceled && hostedProbe.StopCount == 1,
            canceled,
            HeadlessRecorder.Calls.ToArray(),
            hostedProbe.StopCount,
            host.HostScope.State.ToString());
    }

    private static async Task<ConcurrentStopScenarioResult> RunConcurrentStopAsync()
    {
        ConcurrentStopModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<ConcurrentStopModule>();
        var host = builder.Build();
        await host.StartAsync();

        var firstStop = host.StopAsync();
        await ConcurrentStopModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondStop = host.StopAsync();
        var sharedTransaction = ReferenceEquals(firstStop, secondStop);

        ConcurrentStopModule.Release.SetResult();
        await Task.WhenAll(firstStop, secondStop);
        await host.DisposeAsync();

        return new ConcurrentStopScenarioResult(
            "concurrent-stop",
            sharedTransaction && ConcurrentStopModule.ShutdownCount == 1,
            sharedTransaction,
            ConcurrentStopModule.ShutdownCount,
            host.HostScope.State.ToString());
    }

    private static async Task<ReentrantStopScenarioResult> RunReentrantStopAsync()
    {
        IApplicationHost? host = null;
        var builder = CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next, _) =>
            {
                host!.StopAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        await host.StartAsync();
        AggregateException? stopFailure = null;

        try
        {
            await host.StopAsync();
        }
        catch (AggregateException exception)
        {
            stopFailure = exception.Flatten();
        }

        var hostScopeStateAfterStop = host.HostScope.State.ToString();

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Disposal observes the failed stop transaction after completing final cleanup.
        }

        var recursiveFailure = stopFailure?.InnerExceptions.Any(exception =>
            exception is InvalidOperationException &&
            exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal)) == true;

        return new ReentrantStopScenarioResult(
            "reentrant-stop",
            recursiveFailure &&
            hostedProbe.StopCount == 1 &&
            hostScopeStateAfterStop == LifecycleScopeState.Stopped.ToString(),
            recursiveFailure,
            hostedProbe.StopCount,
            hostScopeStateAfterStop,
            diagnostics.Records.Select(record => record.Code).ToArray());
    }

    private static async Task<ServiceOrderingScenarioResult> RunServiceOrderingAsync()
    {
        HeadlessServiceOrderingModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<HeadlessServiceOrderingModule>();
        builder.ConfigureServices(services =>
        {
            HeadlessServiceOrderingModule.Record("user:first");
            services.AddSingleton<IHeadlessOrderedService, UserHeadlessOrderedService>();
        });
        builder.ConfigureServices(_ => HeadlessServiceOrderingModule.Record("user:second"));
        var deferred = HeadlessServiceOrderingModule.Calls.Count == 0;
        var host = builder.Build();
        var resolvedType = host.Services.GetRequiredService<IHeadlessOrderedService>().GetType().Name;
        var registeredTypes = host.Services
            .GetServices<IHeadlessOrderedService>()
            .Select(service => service.GetType().Name)
            .ToArray();
        await host.DisposeAsync();

        var expectedCalls = new[]
        {
            "module:pre",
            "module:configure",
            "module:post",
            "user:first",
            "user:second",
        };

        return new ServiceOrderingScenarioResult(
            "service-ordering",
            deferred &&
            resolvedType == nameof(UserHeadlessOrderedService) &&
            HeadlessServiceOrderingModule.Calls.SequenceEqual(expectedCalls),
            deferred,
            resolvedType,
            registeredTypes,
            HeadlessServiceOrderingModule.Calls.ToArray());
    }

    private static async Task<ApplicationContextScenarioResult> RunApplicationContextAsync()
    {
        var host = CreateBuilder().Build();
        var context = host.Context;
        var expectedAppDataPath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            context.ApplicationName));
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        await host.StartAsync();
        await host.StopAsync();
        await host.DisposeAsync();

        return new ApplicationContextScenarioResult(
            "application-context",
            context.ApplicationId == "AtomUI.City.Core.HeadlessApp" &&
            context.ApplicationInstanceId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(context.ApplicationVersion) &&
            Path.IsPathFullyQualified(context.ContentRootPath) &&
            string.Equals(
                Path.TrimEndingDirectorySeparator(expectedAppDataPath),
                context.AppDataPath,
                pathComparison),
            context.ApplicationId,
            context.ApplicationInstanceId,
            context.ApplicationName,
            context.ApplicationVersion,
            context.EnvironmentName,
            context.ContentRootPath,
            context.AppDataPath,
            context.StartupArguments.Count);
    }

    private static IApplicationHostBuilder CreateBuilder()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Core.HeadlessApp";
            options.ApplicationName = "AtomUI.City.Core.HeadlessApp";
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });
        builder.ConfigureServices(services =>
            services.AddLogging(logging => logging.ClearProviders()));
        return builder;
    }

    private static async Task<LifecycleNextOwnershipScenarioResult>
        RunLifecycleNextOwnershipAsync()
    {
        var builder = CreateBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<LifecycleNextHostedProbe>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<LifecycleNextHostedProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use<LifecycleNextOwnershipMiddleware>(
                LifecycleStages.ApplicationStart,
                static (context, next, cancellationToken) =>
                {
                    _ = next();
                    return ValueTask.CompletedTask;
                }));
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<LifecycleNextHostedProbe>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var start = host.StartAsync();
        await hostedProbe.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var startStayedPending = !start.IsCompleted;
        hostedProbe.ReleaseStart.TrySetResult();
        var fireAndForgetRejected = false;

        try
        {
            await start;
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("must await or return next", StringComparison.Ordinal))
        {
            fireAndForgetRejected = true;
        }

        var hostStartedDiagnosticCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.HostStarted);
        var middlewareFailureCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        await host.StopAsync();
        await host.DisposeAsync();

        var stopBuilder = CreateBuilder();
        stopBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<LifecycleNextStopProbe>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<LifecycleNextStopProbe>());
        });
        stopBuilder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use<LifecycleNextOwnershipMiddleware>(
                LifecycleStages.ApplicationStop,
                static (context, next, cancellationToken) =>
                {
                    _ = next();
                    return ValueTask.CompletedTask;
                }));
        var stopHost = stopBuilder.Build();
        var stopProbe = stopHost.Services.GetRequiredService<LifecycleNextStopProbe>();
        await stopHost.StartAsync();
        var stop = stopHost.StopAsync();
        await stopProbe.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stopStayedPending = !stop.IsCompleted;
        stopProbe.ReleaseStop.TrySetResult();
        var stopFireAndForgetRejected = false;

        try
        {
            await stop;
        }
        catch (AggregateException exception) when (exception
            .Flatten()
            .InnerExceptions
            .Any(failure => failure.Message.Contains(
                "must await or return next",
                StringComparison.Ordinal)))
        {
            stopFireAndForgetRejected = true;
        }

        try
        {
            await stopHost.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Dispose joins the already failed Stop transaction after completing disposal.
        }

        LifecycleNext? savedNext = null;
        var savedTerminalCount = 0;
        var savedPipeline = new LifecyclePipelineBuilder()
            .Use((_, next, _) =>
            {
                savedNext = next;
                return ValueTask.CompletedTask;
            })
            .Build((_, _) =>
            {
                Interlocked.Increment(ref savedTerminalCount);
                return ValueTask.CompletedTask;
            });
        await savedPipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));
        var savedNextRejected = false;

        try
        {
            await savedNext!();
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("after its invocation has completed", StringComparison.Ordinal))
        {
            savedNextRejected = true;
        }

        const int concurrentCallerCount = 64;
        var concurrentTerminalCount = 0;
        var rejectedConcurrentCalls = 0;
        using var concurrentStart = new ManualResetEventSlim();
        var concurrentPipeline = new LifecyclePipelineBuilder()
            .Use(async (_, next, _) =>
            {
                var callers = Enumerable.Range(0, concurrentCallerCount)
                    .Select(_ => Task.Run(async () =>
                    {
                        concurrentStart.Wait();

                        try
                        {
                            await next();
                        }
                        catch (InvalidOperationException exception) when (
                            exception.Message.Contains("only once", StringComparison.Ordinal))
                        {
                            Interlocked.Increment(ref rejectedConcurrentCalls);
                        }
                    }))
                    .ToArray();
                concurrentStart.Set();
                await Task.WhenAll(callers);
            })
            .Build((_, _) =>
            {
                Interlocked.Increment(ref concurrentTerminalCount);
                return ValueTask.CompletedTask;
            });
        await concurrentPipeline.ExecuteAsync(
            new LifecycleContext(LifecycleStages.ApplicationStart));

        var directTerminalCount = 0;
        var directPipeline = new LifecyclePipelineBuilder()
            .Use(static (_, next, _) => next())
            .Build((_, _) =>
            {
                Interlocked.Increment(ref directTerminalCount);
                return ValueTask.CompletedTask;
            });
        await directPipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        var success = startStayedPending &&
            fireAndForgetRejected &&
            hostedProbe.StartCount == 1 &&
            hostedProbe.StopCount == 1 &&
            stopStayedPending &&
            stopFireAndForgetRejected &&
            stopProbe.StopCount == 1 &&
            hostStartedDiagnosticCount == 0 &&
            middlewareFailureCount == 1 &&
            savedNextRejected &&
            savedTerminalCount == 0 &&
            rejectedConcurrentCalls == concurrentCallerCount - 1 &&
            concurrentTerminalCount == 1 &&
            directTerminalCount == 1;

        return new LifecycleNextOwnershipScenarioResult(
            "lifecycle-next-ownership",
            success,
            startStayedPending,
            fireAndForgetRejected,
            hostedProbe.StartCount,
            hostedProbe.StopCount,
            stopStayedPending,
            stopFireAndForgetRejected,
            stopProbe.StopCount,
            hostStartedDiagnosticCount,
            middlewareFailureCount,
            savedNextRejected,
            savedTerminalCount,
            concurrentCallerCount,
            rejectedConcurrentCalls,
            concurrentTerminalCount,
            directTerminalCount);
    }

    private static async Task<SynchronousServiceConfigurationScenarioResult>
        RunSynchronousServiceConfigurationAsync()
    {
        SynchronousServiceConfigurationModule.Reset();
        var synchronizationContext = new NonPumpingSynchronizationContext();
        var previousSynchronizationContext = SynchronizationContext.Current;
        IApplicationHost? host = null;

        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            var builder = CreateBuilder();
            builder.UseModule<SynchronousServiceConfigurationModule>();
            host = builder.Build();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
        }

        var initializationCountBeforeStart = SynchronousServiceConfigurationModule.InitializationCount;
        await using (host)
        {
            await host.StartAsync();
            await host.StopAsync();
        }

        var success = SynchronousServiceConfigurationModule.ConfigurationCount == 1 &&
            initializationCountBeforeStart == 0 &&
            SynchronousServiceConfigurationModule.InitializationCount == 1 &&
            synchronizationContext.PostCount == 0;

        return new SynchronousServiceConfigurationScenarioResult(
            "sync-service-configuration",
            success,
            SynchronousServiceConfigurationModule.ConfigurationCount,
            initializationCountBeforeStart,
            SynchronousServiceConfigurationModule.InitializationCount,
            synchronizationContext.PostCount);
    }

    private static BuildCleanupFailureScenarioResult RunBuildCleanupFailure()
    {
        BuildCleanupRecorder.Reset();
        var builder = CreateBuilder();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.UseModule<BuildCleanupFoundationModule>();
        builder.UseModule<BuildCleanupSuccessfulModule>();
        builder.UseModule<BuildCleanupFailingModule>();
        AggregateException? failure = null;

        try
        {
            using var host = builder.Build();
        }
        catch (AggregateException exception)
        {
            failure = exception;
        }

        var messages = failure?.InnerExceptions.Select(exception => exception.Message).ToArray() ?? [];
        var cleanupDiagnosticCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed);
        var moduleDisposeDiagnosticCount = diagnostics.Records.Count(record =>
            record.Code == HostDiagnosticIds.ModuleLifecycleFailed &&
            record.Context.TryGetValue("stage", out var stage) &&
            stage == "Dispose");
        var success = messages.SequenceEqual(
                ["headless build failed", "headless failing module cleanup failed", "headless foundation cleanup failed"]) &&
            BuildCleanupRecorder.FailingDisposeCount == 1 &&
            BuildCleanupRecorder.SuccessfulDisposeCount == 1 &&
            BuildCleanupRecorder.FoundationDisposeCount == 1 &&
            cleanupDiagnosticCount == 2 &&
            moduleDisposeDiagnosticCount == 2;

        return new BuildCleanupFailureScenarioResult(
            "build-cleanup-failure",
            success,
            messages,
            BuildCleanupRecorder.FailingDisposeCount,
            BuildCleanupRecorder.SuccessfulDisposeCount,
            BuildCleanupRecorder.FoundationDisposeCount,
            cleanupDiagnosticCount,
            moduleDisposeDiagnosticCount);
    }

    private static BuildAsyncCleanupContextScenarioResult RunBuildAsyncCleanupContext()
    {
        AsyncBuildFailureCleanupModule.Reset();
        AsyncConstructionCleanupModule.Reset();

        var configurationFailure = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = CreateBuilder();
            builder.UseModule<AsyncBuildFailureCleanupModule>();
            using var host = builder.Build();
        });
        var constructionFailure = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = CreateBuilder();
            builder.UseModule<AsyncConstructionCleanupModule>();
            builder.UseModule<AsyncCleanupFailingConstructorModule>();
            using var host = builder.Build();
        });
        var success = configurationFailure.Completed &&
            configurationFailure.Error == "headless async build failed" &&
            configurationFailure.SynchronizationContextPostCount == 0 &&
            AsyncBuildFailureCleanupModule.DisposeCount == 1 &&
            constructionFailure.Completed &&
            constructionFailure.Error == "headless async cleanup construction failed" &&
            constructionFailure.SynchronizationContextPostCount == 0 &&
            AsyncConstructionCleanupModule.DisposeCount == 1;

        return new BuildAsyncCleanupContextScenarioResult(
            "build-async-cleanup-context",
            success,
            configurationFailure.Completed,
            configurationFailure.ErrorType,
            configurationFailure.Error,
            configurationFailure.SynchronizationContextPostCount,
            AsyncBuildFailureCleanupModule.DisposeCount,
            constructionFailure.Completed,
            constructionFailure.ErrorType,
            constructionFailure.Error,
            constructionFailure.SynchronizationContextPostCount,
            AsyncConstructionCleanupModule.DisposeCount);
    }

    private static GenericHostAsyncCleanupScenarioResult RunGenericHostAsyncCleanup()
    {
        AsyncOnlyGenericHostCleanupService.Reset();
        IHostDiagnostics? diagnostics = null;
        var buildFailure = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = CreateBuilder();
            diagnostics = builder.GetBuildDiagnostics();
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<AsyncOnlyGenericHostCleanupService>();
                services.AddSingleton<IHostDiagnostics>(provider =>
                {
                    _ = provider.GetRequiredService<AsyncOnlyGenericHostCleanupService>();
                    throw new InvalidOperationException("headless generic host build failed");
                });
            });
            using var host = builder.Build();
        });
        var cleanupDiagnosticCount = diagnostics?.Records.Count(record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed) ?? 0;
        var success = buildFailure.Completed &&
            buildFailure.ErrorType == typeof(InvalidOperationException).FullName &&
            buildFailure.Error == "headless generic host build failed" &&
            buildFailure.SynchronizationContextPostCount == 0 &&
            AsyncOnlyGenericHostCleanupService.DisposeCount == 1 &&
            AsyncOnlyGenericHostCleanupService.DisposeCompleted &&
            cleanupDiagnosticCount == 0;

        return new GenericHostAsyncCleanupScenarioResult(
            "build-generic-host-async-cleanup",
            success,
            buildFailure.Completed,
            buildFailure.ErrorType,
            buildFailure.Error,
            buildFailure.SynchronizationContextPostCount,
            AsyncOnlyGenericHostCleanupService.DisposeCount,
            AsyncOnlyGenericHostCleanupService.DisposeCompleted,
            cleanupDiagnosticCount);
    }

    private static BuildAsyncCleanupTimeoutScenarioResult RunBuildAsyncCleanupTimeout()
    {
        HangingGenericHostCleanupService.Reset();
        IHostDiagnostics? diagnostics = null;
        var buildFailure = RunBuildOnNonPumpingSynchronizationContext(() =>
        {
            var builder = CreateBuilder();
            diagnostics = builder.GetBuildDiagnostics();
            builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(100));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<HangingGenericHostCleanupService>();
                services.AddSingleton<IHostDiagnostics>(provider =>
                {
                    _ = provider.GetRequiredService<HangingGenericHostCleanupService>();
                    throw new InvalidOperationException("headless bounded cleanup build failed");
                });
            });
            using var host = builder.Build();
        });
        var incompleteAtTimeout = !HangingGenericHostCleanupService.DisposeCompleted;
        var timeoutDiagnostic = diagnostics?.Records.FirstOrDefault(record =>
            record.Code == HostDiagnosticIds.HostBuildCleanupFailed &&
            record.Context.TryGetValue("resourceKind", out var resourceKind) &&
            resourceKind == "GenericHost");

        HangingGenericHostCleanupService.Release();
        var completedAfterRelease = SpinWait.SpinUntil(
            () => HangingGenericHostCleanupService.DisposeCompleted,
            TimeSpan.FromSeconds(5));
        var success = buildFailure.Completed &&
            buildFailure.ErrorType == typeof(AggregateException).FullName &&
            buildFailure.SynchronizationContextPostCount == 0 &&
            HangingGenericHostCleanupService.DisposeCount == 1 &&
            incompleteAtTimeout &&
            completedAfterRelease &&
            timeoutDiagnostic is not null &&
            timeoutDiagnostic.Context["cleanupTimeout"] == TimeSpan.FromMilliseconds(100).ToString() &&
            timeoutDiagnostic.Context["cleanupStarted"] == bool.TrueString &&
            timeoutDiagnostic.Context["cleanupMayStillBeRunning"] == bool.TrueString;

        return new BuildAsyncCleanupTimeoutScenarioResult(
            "build-async-cleanup-timeout",
            success,
            buildFailure.Completed,
            buildFailure.ErrorType,
            buildFailure.SynchronizationContextPostCount,
            HangingGenericHostCleanupService.DisposeCount,
            incompleteAtTimeout,
            completedAfterRelease,
            timeoutDiagnostic?.Context["cleanupTimeout"],
            timeoutDiagnostic?.Context["cleanupMayStillBeRunning"]);
    }

    private static BuildThreadResult RunBuildOnNonPumpingSynchronizationContext(Action build)
    {
        Exception? failure = null;
        var synchronizationContext = new NonPumpingSynchronizationContext();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

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
        var rootFailure = failure?.GetBaseException();
        return new BuildThreadResult(
            completed,
            rootFailure?.GetType().FullName,
            rootFailure?.Message,
            synchronizationContext.PostCount);
    }

    private sealed class SynchronousServiceConfigurationModule : ModuleBase
    {
        public static int ConfigurationCount { get; private set; }

        public static int InitializationCount { get; private set; }

        public static void Reset()
        {
            ConfigurationCount = 0;
            InitializationCount = 0;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            ConfigurationCount++;
            context.Services.AddSingleton<SynchronousServiceConfigurationProbe>();
        }

        public override async ValueTask OnApplicationInitializationAsync(
            ApplicationInitializationContext context,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            _ = context.Services.GetRequiredService<SynchronousServiceConfigurationProbe>();
            InitializationCount++;
        }
    }

    private sealed class SynchronousServiceConfigurationProbe;

    private static class BuildCleanupRecorder
    {
        public static int FailingDisposeCount { get; set; }

        public static int SuccessfulDisposeCount { get; set; }

        public static int FoundationDisposeCount { get; set; }

        public static void Reset()
        {
            FailingDisposeCount = 0;
            SuccessfulDisposeCount = 0;
            FoundationDisposeCount = 0;
        }
    }

    private sealed class BuildCleanupFoundationModule : ModuleBase, IDisposable
    {
        public void Dispose()
        {
            BuildCleanupRecorder.FoundationDisposeCount++;
            throw new InvalidOperationException("headless foundation cleanup failed");
        }
    }

    [DependsOn(typeof(BuildCleanupFoundationModule))]
    private sealed class BuildCleanupSuccessfulModule : ModuleBase, IDisposable
    {
        public void Dispose() => BuildCleanupRecorder.SuccessfulDisposeCount++;
    }

    [DependsOn(typeof(BuildCleanupSuccessfulModule))]
    private sealed class BuildCleanupFailingModule : ModuleBase, IDisposable
    {
        public override void ConfigureServices(ServiceConfigurationContext context) =>
            throw new InvalidOperationException("headless build failed");

        public void Dispose()
        {
            BuildCleanupRecorder.FailingDisposeCount++;
            throw new InvalidOperationException("headless failing module cleanup failed");
        }
    }

    private sealed class AsyncBuildFailureCleanupModule : ModuleBase, IAsyncDisposable
    {
        private static int _disposeCount;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static void Reset() => Volatile.Write(ref _disposeCount, 0);

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            throw new InvalidOperationException("headless async build failed");

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
            throw new InvalidOperationException("headless async cleanup construction failed");
    }

    private sealed class AsyncOnlyGenericHostCleanupService : IAsyncDisposable
    {
        private static int _disposeCount;
        private static int _disposeCompleted;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static bool DisposeCompleted => Volatile.Read(ref _disposeCompleted) != 0;

        public static void Reset()
        {
            Volatile.Write(ref _disposeCount, 0);
            Volatile.Write(ref _disposeCompleted, 0);
        }

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            await Task.Yield();
            Volatile.Write(ref _disposeCompleted, 1);
        }
    }

    private sealed class HangingGenericHostCleanupService : IAsyncDisposable
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

    private sealed record BuildThreadResult(
        bool Completed,
        string? ErrorType,
        string? Error,
        int SynchronizationContextPostCount);

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state) =>
            Interlocked.Increment(ref _postCount);
    }

    private sealed class HeadlessFoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("foundation:shutdown");
        }
    }

    [DependsOn(typeof(HeadlessFoundationModule))]
    private sealed class HeadlessApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("application:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("application:shutdown");
        }
    }

    [DependsOn(typeof(HeadlessFoundationModule))]
    private sealed class FailingStartupModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("failing:init");
            throw new InvalidOperationException("headless startup failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("failing:shutdown");
        }
    }

    private sealed class HeadlessRollbackFailingFoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("rollback-foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("rollback-foundation:shutdown");
            throw new InvalidOperationException("headless foundation rollback failed");
        }
    }

    [DependsOn(typeof(HeadlessRollbackFailingFoundationModule))]
    private sealed class HeadlessRollbackFailingApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("rollback-application:init");
            throw new InvalidOperationException("headless startup initialization failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("rollback-application:shutdown");
            throw new InvalidOperationException("headless application rollback failed");
        }
    }

    private sealed class FirstFailingStopModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("first:shutdown");
            throw new InvalidOperationException("first stop failed");
        }
    }

    [DependsOn(typeof(FirstFailingStopModule))]
    private sealed class SecondFailingStopModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("second:shutdown");
            throw new InvalidOperationException("second stop failed");
        }
    }

    private sealed class ScopedHeadlessModule : ModuleBase
    {
        public static ScopedProbe? Instance { get; private set; }

        public static void Reset() => Instance = null;

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            Instance = context.Services.GetRequiredService<ScopedProbe>();
        }
    }

    private sealed class CancellationModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("cancellation:shutdown");
        }
    }

    private sealed class ConcurrentStopModule : ModuleBase
    {
        public static TaskCompletionSource Entered { get; private set; } = CreateCompletionSource();

        public static TaskCompletionSource Release { get; private set; } = CreateCompletionSource();

        public static int ShutdownCount { get; private set; }

        public static void Reset()
        {
            Entered = CreateCompletionSource();
            Release = CreateCompletionSource();
            ShutdownCount = 0;
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken)
        {
            ShutdownCount++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class StopBeforeStartHeadlessModule : ModuleBase, IDisposable
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

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class HeadlessStopFailureMiddleware;

    private interface IHeadlessOrderedService;

    private sealed class ModuleHeadlessOrderedService : IHeadlessOrderedService;

    private sealed class UserHeadlessOrderedService : IHeadlessOrderedService;

    private sealed class HeadlessServiceOrderingModule : ModuleBase
    {
        private static readonly List<string> RecordedCalls = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static void Reset() => RecordedCalls.Clear();

        public static void Record(string call) => RecordedCalls.Add(call);

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:pre");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:configure");
            context.Services.AddSingleton<IHeadlessOrderedService, ModuleHeadlessOrderedService>();
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:post");
        }
    }

    private sealed class ScopedProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class HostedProbe : IHostedService
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

    private sealed class LifecycleNextHostedProbe : IHostedService
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

    private sealed class LifecycleNextStopProbe : IHostedService
    {
        public TaskCompletionSource StopEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStop { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            StopEntered.TrySetResult();
            await ReleaseStop.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class LifecycleNextOwnershipMiddleware;

    private enum RegistryRaceGate
    {
        Shutdown,
        Dispose,
    }

    private static class RegistryRaceRecorder
    {
        private static int _active;
        private static RegistryRaceGate _gate;

        public static ConcurrentQueue<string> ShutdownOrder { get; } = new();

        public static ConcurrentQueue<string> DisposeOrder { get; } = new();

        public static TaskCompletionSource Entered { get; private set; } = NewCompletionSource();

        public static TaskCompletionSource Release { get; private set; } = NewCompletionSource();

        public static bool OverlapDetected { get; private set; }

        public static void Reset(RegistryRaceGate gate)
        {
            _gate = gate;
            _active = 0;
            OverlapDetected = false;
            ShutdownOrder.Clear();
            DisposeOrder.Clear();
            Entered = NewCompletionSource();
            Release = NewCompletionSource();
        }

        public static async ValueTask ShutdownAsync(string name)
        {
            Enter();
            try
            {
                ShutdownOrder.Enqueue(name);
                if (_gate == RegistryRaceGate.Shutdown && name == "five")
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
                if (_gate == RegistryRaceGate.Dispose && name == "five")
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

        private static TaskCompletionSource NewCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private abstract class RegistryRaceModule(string name) : ModuleBase, IAsyncDisposable
    {
        public override ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken) => RegistryRaceRecorder.ShutdownAsync(name);

        public ValueTask DisposeAsync() => RegistryRaceRecorder.DisposeAsync(name);
    }

    private sealed class RegistryRaceOne() : RegistryRaceModule("one");

    [DependsOn(typeof(RegistryRaceOne))]
    private sealed class RegistryRaceTwo() : RegistryRaceModule("two");

    [DependsOn(typeof(RegistryRaceTwo))]
    private sealed class RegistryRaceThree() : RegistryRaceModule("three");

    [DependsOn(typeof(RegistryRaceThree))]
    private sealed class RegistryRaceFour() : RegistryRaceModule("four");

    [DependsOn(typeof(RegistryRaceFour))]
    private sealed class RegistryRaceFive() : RegistryRaceModule("five");

    private static class HeadlessRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }
}
