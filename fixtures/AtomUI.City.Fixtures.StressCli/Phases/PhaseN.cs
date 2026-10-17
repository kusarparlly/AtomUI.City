using System.Collections.Concurrent;
using System.Globalization;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Fixtures.StressCli.Routing;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>可复现的 Localization 跨模块混沌工作负载。</summary>
public static class PhaseN
{
    private const int DynamicTextCount = 96;

    public static async Task RunAsync(
        StressExecutionOptions options,
        CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        var trace = new StressOperationTrace();
        var counters = new ChaosCounters();
        var unobservedExceptions = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> unobservedHandler = (_, args) =>
        {
            Interlocked.Increment(ref unobservedExceptions);
            trace.Record(-1, "unobserved", args.Exception.GetBaseException().Message);
            args.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += unobservedHandler;

        try
        {
            await using var host = StressHost.CreateBuilder().Build();
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            var services = host.Services;
            var localization = services.GetRequiredService<ILocalizationService>();
            var registry = services.GetRequiredService<LanguagePackageRegistry>();
            var provider = services.GetRequiredService<StressLanguagePackageProvider>();
            var bridge = services.GetRequiredService<StressPresentationLocalizationBridge>();
            var diagnostics = (InMemoryLocalizationDiagnostics)services.GetRequiredService<ILocalizationDiagnostics>();
            var eventBus = services.GetRequiredService<IEventBus>();
            var stateRegistry = (ApplicationStateRegistry)services.GetRequiredService<IStateRegistry>();
            var stateWriter = services.GetRequiredService<IApplicationStateWriter>();

            PhaseD.RegisterCatalog(stateRegistry);
            stateRegistry.Add(StateDefinition.Create(
                new StateKey<int>("fixtures.viewmodel.settings"),
                0,
                access: StateAccessPolicy.HostWrite));

            var initialSwitch = await localization.SetCultureAsync("fr-FR", cancellationToken).ConfigureAwait(false);
            if (!initialSwitch.Succeeded)
            {
                throw new InvalidOperationException(initialSwitch.Error!.Message);
            }

            var scopeLeases = ActivateExtendedScopes(localization);
            var textProbes = await CreateTextProbesAsync(localization, cancellationToken).ConfigureAwait(false);
            var textCallbacks = 0;
            var textConsistencyFailures = 0;
            foreach (var probe in textProbes)
            {
                probe.Text.Changed += (_, args) =>
                {
                    Interlocked.Increment(ref textCallbacks);
                    var expected = StressLocalizationCatalog.GetExtendedValue(
                        probe.PackageId,
                        args.Culture.Name,
                        probe.ResourceIndex);
                    if (args.IsMissing
                        || !string.Equals(args.Value, expected, StringComparison.Ordinal)
                        || args.Revision != probe.Text.Revision)
                    {
                        Interlocked.Increment(ref textConsistencyFailures);
                    }
                };
            }

            var eventRoot = services.GetRequiredService<LifecycleScope>();
            var eventOwner = eventRoot.CreateChild(LifecycleScopeKind.Subscription, "localization-chaos-events");
            var observedEvents = 0;
            eventBus.Subscribe<SettingsChanged>(
                eventOwner,
                (context, _) =>
                {
                    if (context.Event.Key.StartsWith("chaos:", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref observedEvents);
                    }

                    return ValueTask.CompletedTask;
                });

            var viewModelCount = Math.Min(options.Workers, 16);
            var viewModels = new SettingsViewModel[viewModelCount];
            for (var index = 0; index < viewModels.Length; index++)
            {
                viewModels[index] = new SettingsViewModel(
                    eventBus,
                    eventRoot,
                    services.GetRequiredService<IApplicationState>(),
                    stateWriter,
                    services.GetService<IHostDiagnostics>());
                await viewModels[index]
                    .ActivateAsync(new ActivationScope(), cancellationToken)
                    .ConfigureAwait(false);
            }

            var operationsPerWorker = options.Operations / options.Workers;
            var remainder = options.Operations % options.Workers;
            var workers = Enumerable.Range(0, options.Workers)
                .Select(worker => RunWorkerAsync(
                    worker,
                    operationsPerWorker + (worker < remainder ? 1 : 0),
                    options.Seed,
                    services,
                    localization,
                    registry,
                    provider,
                    eventBus,
                    viewModels,
                    textProbes,
                    counters,
                    trace,
                    cancellationToken))
                .ToArray();
            await Task.WhenAll(workers).ConfigureAwait(false);

            await WaitForAsync(
                () => observedEvents == counters.PublishedEvents
                    && viewModels.All(viewModel => viewModel.CountOf("event") == counters.PublishedEvents),
                TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);

            var convergenceSwitch = await localization.SetCultureAsync("fr-FR", cancellationToken).ConfigureAwait(false);
            var convergedTexts = textProbes.Count(probe =>
            {
                var expectedCulture = StressLocalizationCatalog.ExtendedLeafContainsKey("fr-FR", probe.ResourceIndex)
                    ? "fr-FR"
                    : "fr";
                return probe.Text.Culture.Name == expectedCulture
                    && probe.Text.Value == StressLocalizationCatalog.GetExtendedValue(
                        probe.PackageId,
                        expectedCulture,
                        probe.ResourceIndex)
                    && probe.Text.Revision == localization.CultureRevision
                    && !probe.Text.IsMissing;
            });

            var callbacksBeforeRelease = textCallbacks;
            var eventsBeforeRelease = observedEvents;
            foreach (var probe in textProbes)
            {
                probe.Text.Dispose();
            }

            foreach (var viewModel in viewModels)
            {
                await viewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
                viewModel.Dispose();
            }

            eventOwner.Dispose();
            for (var index = scopeLeases.Count - 1; index >= 0; index--)
            {
                scopeLeases[index].Dispose();
            }

            await localization.SetCultureAsync("de-DE", cancellationToken).ConfigureAwait(false);
            await eventBus.PublishAsync(
                new SettingsChanged("chaos:after-release"),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var records = diagnostics.Records;
            var executionOk = counters.CompletedOperations == options.Operations
                && counters.UnexpectedFailures == 0;
            var lookupOk = counters.LookupOperations > 0
                && counters.LookupFailures == 0
                && counters.MessageFailures == 0;
            var mutationOk = counters.SuccessfulSwitches > 0
                && counters.UnexpectedSwitchFailures == 0
                && bridge.MaximumConcurrentApplies == 1;
            var crossModuleOk = counters.NavigationOperations > 0
                && counters.NavigationFailures == 0
                && counters.PublishedEvents > 0
                && counters.PublishFailures == 0
                && observedEvents == counters.PublishedEvents
                && viewModels.All(viewModel => viewModel.CountOf("event") == counters.PublishedEvents)
                && counters.StateWrites > 0;
            var faultOk = counters.ExpectedCancellations > 0
                && counters.ExpectedProviderFailures > 0
                && counters.InjectedProviderReturns > 0
                && counters.InjectedProviderThrows > 0
                && records.Count(record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed)
                    >= counters.ExpectedProviderFailures
                && records.All(record => !string.IsNullOrWhiteSpace(record.OperationId));
            var convergenceOk = convergenceSwitch.Succeeded
                && convergedTexts == DynamicTextCount
                && textConsistencyFailures == 0
                && textCallbacks == callbacksBeforeRelease
                && observedEvents == eventsBeforeRelease
                && provider.ActiveLoadCount == 0
                && bridge.ActiveApplyCount == 0
                && unobservedExceptions == 0;

            if (!executionOk || !lookupOk || !mutationOk || !crossModuleOk || !faultOk || !convergenceOk)
            {
                trace.Print(Console.Error);
            }

            FixtureState.Report.Record(
                "N01-localization-chaos-execution",
                "全部 seeded chaos 操作在 watchdog 内完成且无未分类异常",
                executionOk,
                $"seed={options.Seed} completed={counters.CompletedOperations}/{options.Operations} failures={counters.UnexpectedFailures}");
            FixtureState.Report.Record(
                "N02-localization-chaos-lookup",
                "并发 lookup 和格式化结果始终属于实际命中的 culture/package",
                lookupOk,
                $"lookups={counters.LookupOperations} messages={counters.MessageOperations}");
            FixtureState.Report.Record(
                "N03-localization-chaos-mutation",
                "并发 culture mutation 串行提交且 Presentation bridge 不并行重入",
                mutationOk,
                $"switches={counters.SuccessfulSwitches} classifiedFailures={counters.ClassifiedSwitchFailures} maxBridge={bridge.MaximumConcurrentApplies}");
            FixtureState.Report.Record(
                "N04-localization-chaos-cross-module",
                "Router、EventBus、State 和 MVVM 在 Localization 压力下保持闭环",
                crossModuleOk,
                $"routes={counters.NavigationOperations} events={counters.PublishedEvents} state={counters.StateWrites}");
            FixtureState.Report.Record(
                "N05-localization-chaos-faults",
                "取消和 Provider 返回/抛出故障均被分类并产生完整诊断",
                faultOk,
                $"cancelled={counters.ExpectedCancellations} providerFailures={counters.ExpectedProviderFailures} " +
                $"returned={counters.InjectedProviderReturns} threw={counters.InjectedProviderThrows} diagnostics={records.Count}");
            FixtureState.Report.Record(
                "N06-localization-chaos-convergence",
                "96 个动态文案最终同 revision 收敛，释放后零回调、零在途工作",
                convergenceOk,
                $"texts={convergedTexts}/{DynamicTextCount} callbacks={callbacksBeforeRelease} unobserved={unobservedExceptions}");

            await host.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= unobservedHandler;
        }
    }

    private static async Task RunWorkerAsync(
        int worker,
        int operationCount,
        int seed,
        IServiceProvider services,
        ILocalizationService localization,
        LanguagePackageRegistry registry,
        StressLanguagePackageProvider provider,
        IEventBus eventBus,
        IReadOnlyList<SettingsViewModel> viewModels,
        IReadOnlyList<TextProbe> textProbes,
        ChaosCounters counters,
        StressOperationTrace trace,
        CancellationToken cancellationToken)
    {
        var random = new Random(unchecked(seed + worker * 104_729));
        using var routeServices = services.CreateScope();
        var router = routeServices.ServiceProvider.GetRequiredService<IRouter>();
        for (var index = 0; index < operationCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operationId = $"{worker}.{index}";
            var choice = random.Next(100);
            try
            {
                if (choice < 25)
                {
                    await RunLookupAsync(localization, random, counters, cancellationToken).ConfigureAwait(false);
                }
                else if (choice < 35)
                {
                    await RunMessageAsync(localization, random, counters, cancellationToken).ConfigureAwait(false);
                }
                else if (choice < 45)
                {
                    var culture = StressLocalizationCatalog.ExtendedLeafCultureNames[random.Next(
                        StressLocalizationCatalog.ExtendedLeafCultureNames.Count)];
                    var result = await localization.SetCultureAsync(culture, cancellationToken).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        Interlocked.Increment(ref counters.SuccessfulSwitches);
                    }
                    else if (result.Error?.Kind == LocalizationErrorKind.PackageLoadFailed)
                    {
                        Interlocked.Increment(ref counters.ClassifiedSwitchFailures);
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.UnexpectedSwitchFailures);
                    }
                }
                else if (choice < 55)
                {
                    await RunNavigationAsync(router, random, index, counters, cancellationToken).ConfigureAwait(false);
                }
                else if (choice < 65)
                {
                    var result = await eventBus.PublishAsync(
                        new SettingsChanged($"chaos:{operationId}"),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref counters.PublishedEvents);
                    if (result.FailedCount != 0)
                    {
                        Interlocked.Increment(ref counters.PublishFailures);
                    }
                }
                else if (choice < 73)
                {
                    viewModels[random.Next(viewModels.Count)].SeedState(unchecked(worker * 1_000_000 + index + 1));
                    Interlocked.Increment(ref counters.StateWrites);
                }
                else if (choice < 81)
                {
                    await textProbes[random.Next(textProbes.Count)].Text.RefreshAsync(cancellationToken)
                        .ConfigureAwait(false);
                    Interlocked.Increment(ref counters.TextRefreshes);
                }
                else if (choice < 89)
                {
                    await RunDynamicScopeAsync(
                        worker,
                        index,
                        localization,
                        registry,
                        provider,
                        injectFailure: false,
                        counters,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (choice < 94)
                {
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    try
                    {
                        await localization.GetStringAsync("Common.Save", cancellation.Token).ConfigureAwait(false);
                        Interlocked.Increment(ref counters.UnexpectedFailures);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref counters.ExpectedCancellations);
                    }
                }
                else
                {
                    await RunDynamicScopeAsync(
                        worker,
                        index,
                        localization,
                        registry,
                        provider,
                        injectFailure: true,
                        counters,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref counters.UnexpectedFailures);
                trace.Record(worker, choice.ToString(CultureInfo.InvariantCulture),
                    $"id={operationId} {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                Interlocked.Increment(ref counters.CompletedOperations);
            }
        }
    }

    private static async Task RunLookupAsync(
        ILocalizationService localization,
        Random random,
        ChaosCounters counters,
        CancellationToken cancellationToken)
    {
        var packageId = StressLocalizationCatalog.ExtendedPackageIds[random.Next(
            StressLocalizationCatalog.ExtendedPackageIds.Count)];
        var resourceIndex = random.Next(28);
        var result = await localization.GetStringAsync(
            StressLocalizationCatalog.GetExtendedKey(packageId, resourceIndex),
            StressLocalizationCatalog.CreateContext(packageId),
            cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref counters.LookupOperations);
        var expected = StressLocalizationCatalog.GetExtendedValue(packageId, result.Culture.Name, resourceIndex);
        if (result.IsMissing || !string.Equals(result.Value, expected, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref counters.LookupFailures);
        }
    }

    private static async Task RunMessageAsync(
        ILocalizationService localization,
        Random random,
        ChaosCounters counters,
        CancellationToken cancellationToken)
    {
        var packageId = StressLocalizationCatalog.ExtendedPackageIds[random.Next(
            StressLocalizationCatalog.ExtendedPackageIds.Count)];
        var result = await localization.GetMessageAsync(
            StressLocalizationCatalog.GetExtendedKey(packageId, 27),
            [12345.67m, new DateTime(2026, 9, 8)],
            StressLocalizationCatalog.CreateContext(packageId),
            cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref counters.MessageOperations);
        if (result.IsMissing
            || result.IsFormatFailed
            || !result.Value.StartsWith($"[{result.Culture.Name}|{packageId}]", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref counters.MessageFailures);
        }
    }

    private static async Task RunNavigationAsync(
        IRouter router,
        Random random,
        int operation,
        ChaosCounters counters,
        CancellationToken cancellationToken)
    {
        NavigationResult result = random.Next(4) switch
        {
            0 => await router.NavigateAsync(FixtureRoutes.Orders(), cancellationToken: cancellationToken).ConfigureAwait(false),
            1 => await router.NavigateAsync(FixtureRoutes.Payments(), cancellationToken: cancellationToken).ConfigureAwait(false),
            2 => await router.NavigateAsync(FixtureRoutes.Reports(), cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => await router.NavigateAsync(
                FixtureRoutes.Search(),
                new SearchRouteParameters($"chaos-{operation}"),
                cancellationToken: cancellationToken).ConfigureAwait(false),
        };
        Interlocked.Increment(ref counters.NavigationOperations);
        if (result.Status != NavigationResultStatus.Success)
        {
            Interlocked.Increment(ref counters.NavigationFailures);
        }
    }

    private static async Task RunDynamicScopeAsync(
        int worker,
        int operation,
        ILocalizationService localization,
        LanguagePackageRegistry registry,
        StressLanguagePackageProvider provider,
        bool injectFailure,
        ChaosCounters counters,
        CancellationToken cancellationToken)
    {
        var packageId = $"Chaos.Dynamic.{worker}.{operation}";
        var scopeId = $"fixtures.plugin.chaos.{worker}.{operation}";
        var contributionId = $"fixtures.chaos.localization.{worker}.{operation}";
        var key = $"Chaos.Dynamic.Key.{worker}.{operation}";
        var descriptors = StressLocalizationCatalog.ExtendedLeafCultureNames
            .Select(culture => new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(culture),
                ResourceScope.Plugin)
            {
                ScopeId = scopeId,
                ProviderKind = LanguagePackageProviderKind.InMemory,
                ContributionId = contributionId,
                InMemoryResources = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [key] = $"[{culture}|{packageId}] dynamic",
                },
            })
            .ToArray();
        var registration = registry.RegisterRange(descriptors, $"owner.{worker}.{operation}");
        if (!registration.Succeeded)
        {
            throw new InvalidOperationException(registration.Error!.Message);
        }

        using var lease = localization.ActivateScope(new LocalizationLookupContext(pluginId: scopeId));
        if (injectFailure)
        {
            if ((worker + operation) % 2 == 0)
            {
                provider.FailNext(packageId);
                Interlocked.Increment(ref counters.InjectedProviderReturns);
            }
            else
            {
                provider.ThrowNext(packageId);
                Interlocked.Increment(ref counters.InjectedProviderThrows);
            }
        }

        var result = await localization.GetStringAsync(
            key,
            new LocalizationLookupContext(pluginId: scopeId),
            cancellationToken).ConfigureAwait(false);
        if (injectFailure)
        {
            Interlocked.Increment(ref counters.ExpectedProviderFailures);
            if (result.IsMissing)
            {
                Interlocked.Increment(ref counters.FaultedLookupMisses);
            }
            else
            {
                // A concurrent culture switch may consume the one-shot provider fault.
                Interlocked.Increment(ref counters.FaultsConsumedByMutation);
            }
        }
        else if (result.IsMissing
            || !result.Value.StartsWith($"[{result.Culture.Name}|{packageId}]", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref counters.LookupFailures);
        }

        await localization.RevokePackagesByContributionIdAsync(contributionId, cancellationToken)
            .ConfigureAwait(false);
        Interlocked.Increment(ref counters.DynamicScopeOperations);
    }

    private static IReadOnlyList<ILocalizationScopeLease> ActivateExtendedScopes(
        ILocalizationService localization)
    {
        var leases = new List<ILocalizationScopeLease>();
        foreach (var packageId in StressLocalizationCatalog.ExtendedPackageIds)
        {
            var context = StressLocalizationCatalog.CreateContext(packageId);
            if (context.ModuleId is null
                && context.PluginId is null
                && context.RouteId is null
                && context.WindowId is null)
            {
                continue;
            }

            leases.Add(localization.ActivateScope(context));
        }

        return leases;
    }

    private static async Task<IReadOnlyList<TextProbe>> CreateTextProbesAsync(
        ILocalizationService localization,
        CancellationToken cancellationToken)
    {
        var probes = new List<TextProbe>(DynamicTextCount);
        foreach (var packageId in StressLocalizationCatalog.ExtendedPackageIds)
        {
            for (var index = 0; index < 10; index++)
            {
                probes.Add(await CreateProbeAsync(localization, packageId, index, cancellationToken).ConfigureAwait(false));
            }
        }

        var firstPackage = StressLocalizationCatalog.ExtendedPackageIds[0];
        for (var index = 10; probes.Count < DynamicTextCount; index++)
        {
            probes.Add(await CreateProbeAsync(localization, firstPackage, index, cancellationToken).ConfigureAwait(false));
        }

        return Array.AsReadOnly(probes.ToArray());
    }

    private static async Task<TextProbe> CreateProbeAsync(
        ILocalizationService localization,
        string packageId,
        int resourceIndex,
        CancellationToken cancellationToken)
    {
        var text = await localization.CreateTextAsync(
            StressLocalizationCatalog.GetExtendedKey(packageId, resourceIndex),
            StressLocalizationCatalog.CreateContext(packageId),
            cancellationToken).ConfigureAwait(false);
        return new TextProbe(packageId, resourceIndex, text);
    }

    private static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        while (!condition() && DateTime.UtcNow - started < timeout)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record TextProbe(string PackageId, int ResourceIndex, ILocalizedText Text);

    private sealed class ChaosCounters
    {
        public int CompletedOperations;
        public int UnexpectedFailures;
        public int LookupOperations;
        public int LookupFailures;
        public int MessageOperations;
        public int MessageFailures;
        public int SuccessfulSwitches;
        public int ClassifiedSwitchFailures;
        public int UnexpectedSwitchFailures;
        public int NavigationOperations;
        public int NavigationFailures;
        public int PublishedEvents;
        public int PublishFailures;
        public int StateWrites;
        public int TextRefreshes;
        public int DynamicScopeOperations;
        public int ExpectedCancellations;
        public int ExpectedProviderFailures;
        public int InjectedProviderReturns;
        public int InjectedProviderThrows;
        public int FaultedLookupMisses;
        public int FaultsConsumedByMutation;
    }
}
