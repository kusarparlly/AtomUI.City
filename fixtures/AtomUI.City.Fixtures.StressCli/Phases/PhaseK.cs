using System.Collections.Concurrent;
using System.Diagnostics;
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

/// <summary>Localization 高频联合仿真：culture、路由、事件、状态和动态文本。</summary>
public static class PhaseK
{
    private const int LookupsPerIteration = 16;
    private const int DynamicTextCount = 12;

    public static async Task RunAsync(StressExecutionOptions options, CancellationToken cancellationToken)
    {
        var iterations = options.SoakIterations;
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var services = host.Services;
        var localization = services.GetRequiredService<ILocalizationService>();
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

        var operationsContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.OperationsModuleId);
        var billingContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.BillingModuleId);
        var supportContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.SupportModuleId);
        var ordersContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.OrdersRouteId);
        var paymentsContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.PaymentsRouteId);
        var searchContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.SearchRouteId);
        var supportRouteContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.SupportRouteId);
        var reportsContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.ReportsRouteId);
        var mainWindowContext = new LocalizationLookupContext(windowId: StressLocalizationCatalog.MainWindowId);
        var exportWindowContext = new LocalizationLookupContext(windowId: StressLocalizationCatalog.ExportWindowId);
        var pluginContext = new LocalizationLookupContext(pluginId: StressLocalizationCatalog.SalesPluginId);

        var leases = new List<ILocalizationScopeLease>
        {
            localization.ActivateScope(operationsContext),
            localization.ActivateScope(billingContext),
            localization.ActivateScope(supportContext),
            localization.ActivateScope(ordersContext),
            localization.ActivateScope(paymentsContext),
            localization.ActivateScope(searchContext),
            localization.ActivateScope(supportRouteContext),
            localization.ActivateScope(reportsContext),
            localization.ActivateScope(mainWindowContext),
            localization.ActivateScope(pluginContext),
        };
        var exportWindowLease = localization.ActivateScope(exportWindowContext);

        var texts = new List<ILocalizedText>
        {
            await localization.CreateTextAsync("Common.Save", cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Menu.Orders", cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Presentation.Theme", cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Operations.Title", operationsContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Operations.LegacyHint", operationsContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Billing.Title", billingContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Support.Title", supportContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Orders.Title", ordersContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Payments.Title", paymentsContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Reports.Title", reportsContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("MainWindow.Title", mainWindowContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("SalesPlugin.Banner", pluginContext, cancellationToken).ConfigureAwait(false),
        };

        var dynamicChanges = new ConcurrentDictionary<ILocalizedText, int>();
        foreach (var text in texts)
        {
            dynamicChanges[text] = 0;
            text.Changed += (_, _) => dynamicChanges.AddOrUpdate(text, 1, static (_, count) => count + 1);
        }

        var cultureStateChanges = 0;
        using var cultureSubscription = localization.CultureState.OnChange(
            _ => Interlocked.Increment(ref cultureStateChanges));

        var eventOwnerRoot = services.GetRequiredService<LifecycleScope>();
        var eventOwner = eventOwnerRoot.CreateChild(LifecycleScopeKind.Subscription, "localization-soak-events");
        var cultureEvents = 0;
        eventBus.Subscribe<SettingsChanged>(
            eventOwner,
            (context, _) =>
            {
                if (context.Event.Key.StartsWith("culture:", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref cultureEvents);
                }

                return ValueTask.CompletedTask;
            });

        var settingsViewModel = new SettingsViewModel(
            eventBus,
            eventOwnerRoot,
            services.GetRequiredService<IApplicationState>(),
            stateWriter,
            services.GetService<IHostDiagnostics>());
        await settingsViewModel.ActivateAsync(new ActivationScope(), cancellationToken).ConfigureAwait(false);

        using var navigationServices = services.CreateScope();
        var router = navigationServices.ServiceProvider.GetRequiredService<IRouter>();
        var successfulSwitches = 0;
        var lookupCount = 0;
        var lookupFailures = 0;
        var navigationCount = 0;
        var navigationFailures = 0;
        var formattedCount = 0;
        var formatFailures = 0;
        var publishCount = 0;
        var publishFailures = 0;
        var scopeChurns = 0;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chinese = iteration % 2 == 0;
            var cultureName = chinese ? "zh-CN" : "en-US";
            var switchResult = await localization.SetCultureAsync(cultureName, cancellationToken).ConfigureAwait(false);
            if (switchResult.Succeeded && localization.CurrentCulture.Name == cultureName)
            {
                successfulSwitches++;
            }

            NavigationResult navigation;
            LocalizationLookupContext routeContext;
            string routeKey;
            string expectedRouteTitle;
            switch (iteration % 4)
            {
                case 0:
                    navigation = await router.NavigateAsync(FixtureRoutes.Orders(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    routeContext = ordersContext;
                    routeKey = "Orders.Title";
                    expectedRouteTitle = chinese ? "订单" : "Orders";
                    break;
                case 1:
                    navigation = await router.NavigateAsync(FixtureRoutes.Payments(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    routeContext = paymentsContext;
                    routeKey = "Payments.Title";
                    expectedRouteTitle = chinese ? "支付" : "Payments";
                    break;
                case 2:
                    navigation = await router.NavigateAsync(FixtureRoutes.Reports(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    routeContext = reportsContext;
                    routeKey = "Reports.Title";
                    expectedRouteTitle = chinese ? "报表" : "Reports";
                    break;
                default:
                    navigation = await router.NavigateAsync(
                            FixtureRoutes.Search(),
                            new SearchRouteParameters($"term-{iteration}"),
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    routeContext = searchContext;
                    routeKey = "Search.Title";
                    expectedRouteTitle = chinese ? "搜索" : "Search";
                    break;
            }

            navigationCount++;
            if (navigation.Status != NavigationResultStatus.Success)
            {
                navigationFailures++;
            }

            var lookupTasks = new List<Task<LocalizedString>>(LookupsPerIteration);
            AddLookups(lookupTasks, localization, "Common.Save", LocalizationLookupContext.Global, 4, cancellationToken);
            AddLookups(lookupTasks, localization, "Operations.Title", operationsContext, 3, cancellationToken);
            AddLookups(lookupTasks, localization, routeKey, routeContext, 3, cancellationToken);
            AddLookups(lookupTasks, localization, "MainWindow.Title", mainWindowContext, 3, cancellationToken);
            AddLookups(lookupTasks, localization, "SalesPlugin.Banner", pluginContext, 3, cancellationToken);
            var lookupResults = await Task.WhenAll(lookupTasks).ConfigureAwait(false);
            lookupCount += lookupResults.Length;

            var expectedSave = chinese ? "保存" : "Save";
            var expectedOperations = chinese ? "运营中心" : "Operations center";
            var expectedWindow = chinese ? "AtomUI City 运营控制台" : "AtomUI City Operations";
            var expectedPlugin = chinese ? "销售扩展已启用。" : "Sales extension is active.";
            var lookupOk = lookupResults.Take(4).All(result => result.Value == expectedSave)
                && lookupResults.Skip(4).Take(3).All(result => result.Value == expectedOperations)
                && lookupResults.Skip(7).Take(3).All(result => result.Value == expectedRouteTitle)
                && lookupResults.Skip(10).Take(3).All(result => result.Value == expectedWindow)
                && lookupResults.Skip(13).Take(3).All(result => result.Value == expectedPlugin);
            if (!lookupOk)
            {
                lookupFailures++;
            }

            var billingMessage = await localization.GetMessageAsync(
                "Billing.Amount",
                [1234.5m + iteration],
                billingContext,
                cancellationToken).ConfigureAwait(false);
            var operationsMessage = await localization.GetMessageAsync(
                "Operations.TaskCompleted",
                [$"operation-{iteration}", 25 + iteration],
                operationsContext,
                cancellationToken).ConfigureAwait(false);
            formattedCount += 2;
            if (billingMessage.IsMissing
                || billingMessage.IsFormatFailed
                || operationsMessage.IsMissing
                || operationsMessage.IsFormatFailed
                || (chinese && (!billingMessage.Value.StartsWith("金额：", StringComparison.Ordinal)
                    || !operationsMessage.Value.StartsWith("任务 ", StringComparison.Ordinal)))
                || (!chinese && (!billingMessage.Value.StartsWith("Amount: ", StringComparison.Ordinal)
                    || !operationsMessage.Value.StartsWith("Task ", StringComparison.Ordinal))))
            {
                formatFailures++;
            }

            settingsViewModel.SeedState(iteration + 1);
            var publishResult = await eventBus.PublishAsync(
                new SettingsChanged($"culture:{cultureName}:{iteration}"),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            publishCount++;
            if (publishResult.FailedCount != 0)
            {
                publishFailures++;
            }

            if ((iteration + 1) % 25 == 0)
            {
                exportWindowLease.Dispose();
                exportWindowLease = localization.ActivateScope(exportWindowContext);
                scopeChurns++;
            }
        }

        await WaitForAsync(
            () => settingsViewModel.CountOf("event") == iterations,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);

        var bridgeApplyBeforeRelease = bridge.ApplyCount;
        var changesBeforeRelease = dynamicChanges.Values.Sum();
        var cultureEventsBeforeRelease = cultureEvents;
        var viewModelEventsBeforeRelease = settingsViewModel.CountOf("event");
        var cultureStateChangesBeforeRelease = cultureStateChanges;

        foreach (var text in texts)
        {
            text.Dispose();
        }

        cultureSubscription.Dispose();
        eventOwner.Dispose();
        await settingsViewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
        settingsViewModel.Dispose();
        exportWindowLease.Dispose();
        for (var index = leases.Count - 1; index >= 0; index--)
        {
            leases[index].Dispose();
        }

        var postReleaseSwitch = await localization.SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
        await eventBus.PublishAsync(
            new SettingsChanged("culture:after-release"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var switchOk = successfulSwitches == iterations
            && bridgeApplyBeforeRelease == iterations
            && cultureStateChangesBeforeRelease >= iterations;
        var dynamicOk = texts.Count == DynamicTextCount
            && changesBeforeRelease == iterations * DynamicTextCount
            && dynamicChanges.Values.All(count => count == iterations)
            && texts[0].Value == "Save"
            && texts[4].Value == "Open the legacy operations panel.";
        var lookupOkFinal = lookupCount == iterations * LookupsPerIteration
            && lookupFailures == 0
            && provider.MaximumLoadCount == 1;
        var navigationOk = navigationCount == iterations && navigationFailures == 0;
        var eventAndViewModelOk = publishCount == iterations
            && publishFailures == 0
            && cultureEventsBeforeRelease == iterations
            && viewModelEventsBeforeRelease == iterations
            && settingsViewModel.CountOf("state-reaction") == iterations;
        var releaseOk = postReleaseSwitch.Succeeded
            && scopeChurns == iterations / 25
            && formattedCount == iterations * 2
            && formatFailures == 0
            && dynamicChanges.Values.Sum() == changesBeforeRelease
            && cultureEvents == cultureEventsBeforeRelease
            && settingsViewModel.CountOf("event") == viewModelEventsBeforeRelease
            && cultureStateChanges == cultureStateChangesBeforeRelease
            && bridge.ApplyCount == bridgeApplyBeforeRelease + 1
            && diagnostics.Records.All(record => record.Severity != LocalizationDiagnosticSeverity.Error);

        FixtureState.Report.Record(
            "K01-localization-switch-soak",
            $"{iterations} 次有效 culture commit 串行完成且 State 可观察",
            switchOk,
            $"switches={successfulSwitches} bridge={bridgeApplyBeforeRelease} stateChanges={cultureStateChangesBeforeRelease}");
        FixtureState.Report.Record(
            "K02-localization-dynamic-soak",
            $"12 个动态文案在 {iterations} 次切换中逐次、完整刷新",
            dynamicOk,
            $"changes={changesBeforeRelease}/{iterations * DynamicTextCount}");
        FixtureState.Report.Record(
            "K03-localization-lookup-soak",
            $"{iterations * LookupsPerIteration:N0} 次跨 scope 并发查找值一致且每包只加载一次",
            lookupOkFinal,
            $"lookups={lookupCount} failures={lookupFailures} uniqueLoads={provider.UniqueLoadCount} maxLoads={provider.MaximumLoadCount}");
        FixtureState.Report.Record(
            "K04-localization-routing-soak",
            $"{iterations} 次 Router 导航与 route localization context 保持一致",
            navigationOk,
            $"navigations={navigationCount} failures={navigationFailures}");
        FixtureState.Report.Record(
            "K05-localization-event-mvvm-soak",
            $"{iterations} 次 EventBus 通知、State reaction 与 MVVM activation 联合闭环",
            eventAndViewModelOk,
            $"events={cultureEventsBeforeRelease} vmEvents={viewModelEventsBeforeRelease} vmState={settingsViewModel.CountOf("state-reaction")}");
        FixtureState.Report.Record(
            "K06-localization-release-soak",
            $"{iterations * 2} 次格式化、{scopeChurns} 次 scope churn 后释放资源且不再回调",
            releaseOk,
            $"formatted={formattedCount} churns={scopeChurns} elapsedMs={stopwatch.ElapsedMilliseconds}");

        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddLookups(
        ICollection<Task<LocalizedString>> tasks,
        ILocalizationService localization,
        string key,
        LocalizationLookupContext context,
        int count,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < count; index++)
        {
            tasks.Add(localization.GetStringAsync(key, context, cancellationToken).AsTask());
        }
    }

    private static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (!condition() && Stopwatch.GetElapsedTime(startedAt) < timeout)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
}
