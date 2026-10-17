using System.Collections.Concurrent;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Routing;
using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Phase I：25 轮订单运营工作流与资源收束验证。</summary>
public static class PhaseI
{
    private const int Iterations = 25;

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var provider = host.Services;
        var eventBus = provider.GetRequiredService<IEventBus>();
        var stateRegistry = (ApplicationStateRegistry)provider.GetRequiredService<IStateRegistry>();
        var writer = provider.GetRequiredService<IApplicationStateWriter>();
        PhaseD.RegisterCatalog(stateRegistry);

        var products = provider.GetRequiredService<IProductCatalog>();
        products.Upsert("SKU-0001", 20m);
        provider.GetRequiredService<ISettingsStore>().Set("pricing.multiplier", "1");
        provider.GetRequiredService<ISearchIndex>().Index("SKU-0001");
        _ = provider.GetRequiredService<IIdentityDirectory>().SignIn("operator-soak");
        _ = provider.GetRequiredService<ITenantDirectory>().Switch("tenant-soak");

        var baselineSnapshot = stateRegistry.CreateSnapshot();
        var chainHits = 0;
        var maxDepth = 0;
        var correlations = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var eventOwnerRoot = provider.GetRequiredService<LifecycleScope>();
        using var workflowOwner = eventOwnerRoot.CreateChild(LifecycleScopeKind.Subscription, "five-module-workflow");

        void Observe<T>(EventContext<T> context)
        {
            correlations[context.CorrelationId] = 0;
            var observed = context.PublishDepth;
            var current = Volatile.Read(ref maxDepth);
            while (observed > current)
            {
                var previous = Interlocked.CompareExchange(ref maxDepth, observed, current);
                if (previous == current)
                {
                    break;
                }

                current = previous;
            }
        }

        _ = eventBus.Subscribe<OrderSubmitted>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.OrdersRevenue, value => value + context.Event.Price * context.Event.Quantity);
            await eventBus.PublishAsync(
                new InventoryReserved(context.Event.Sku, context.Event.Quantity),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<InventoryReserved>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.FulfillmentPending, value => value + 1);
            await eventBus.PublishAsync(
                new PaymentAuthorized($"order-{chainHits + 1}", $"payment-{chainHits + 1}", context.Event.Amount * 20m),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<PaymentAuthorized>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.PaymentsAuthorized, value => value + context.Event.Amount);
            await eventBus.PublishAsync(
                new PaymentCaptured(context.Event.PaymentId, context.Event.Amount),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<PaymentCaptured>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.PaymentsCaptured, value => value + context.Event.Amount);
            await eventBus.PublishAsync(
                new FulfillmentPlanned(context.Event.PaymentId, $"plan-{context.Event.PaymentId}"),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<FulfillmentPlanned>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.FulfillmentPending, value => value - 1);
            writer.Update(PhaseD.StateCatalog.FulfillmentCompleted, value => value + 1);
            await eventBus.PublishAsync(
                new PickListCreated(context.Event.PlanId, 1),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<PickListCreated>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            var quote = provider.GetRequiredService<IShippingQuote>().Quote(context.Event.ItemCount);
            await eventBus.PublishAsync(
                new ShippingQuoted(context.Event.PlanId, quote),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<ShippingQuoted>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.ShippingQuoted, value => value + 1);
            var shipment = provider.GetRequiredService<IShipmentTracker>().Dispatch(context.Event.OrderId);
            await eventBus.PublishAsync(
                new ShipmentDispatched(context.Event.OrderId, shipment),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<ShipmentDispatched>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.ShippingInTransit, value => value + 1);
            var searchIndex = provider.GetRequiredService<ISearchIndex>();
            searchIndex.Index("SKU-0001");
            await eventBus.PublishAsync(
                new SearchExecuted("0001", searchIndex.Search("0001").Count),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<SearchExecuted>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.SearchQueries, value => value + 1);
            var recommendation = provider.GetRequiredService<IRecommendationEngine>()
                .Recommend(provider.GetRequiredService<ISearchIndex>().Search(context.Event.Term));
            await eventBus.PublishAsync(
                new RecommendationProduced("operator-soak", recommendation),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<RecommendationProduced>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.RecommendationsGenerated, value => value + 1);
            var ticket = provider.GetRequiredService<ISupportDesk>().Open(context.Event.Subject, context.Event.Sku);
            await eventBus.PublishAsync(
                new SupportTicketOpened(context.Event.Subject, ticket),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<SupportTicketOpened>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.SupportOpenTickets, value => value + 1);
            await eventBus.PublishAsync(
                new AuditAppended($"support:{context.Event.TicketId}"),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<AuditAppended>(workflowOwner, async (context, handlerCancellationToken) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.AuditEntries, value => value + 1);
            await eventBus.PublishAsync(
                new DashboardUpdated(context.Event.Entry),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<DashboardUpdated>(workflowOwner, (context, _) =>
        {
            Observe(context);
            writer.Update(PhaseD.StateCatalog.DashboardViews, value => value + 1);
            Interlocked.Increment(ref chainHits);
            return ValueTask.CompletedTask;
        });

        stateRegistry.Add(StateDefinition.Create(
            new StateKey<int>("fixtures.viewmodel.workflow"),
            0,
            access: StateAccessPolicy.HostWrite));
        var workflowViewModel = new WorkflowViewModel(
            eventBus,
            eventOwnerRoot,
            provider.GetRequiredService<IApplicationState>(),
            writer,
            provider.GetRequiredService<IHostDiagnostics>());
        await workflowViewModel.ActivateAsync(new ActivationScope(), cancellationToken).ConfigureAwait(false);

        using var navigationServices = provider.CreateScope();
        var router = navigationServices.ServiceProvider.GetRequiredService<IRouter>();
        var routeFailures = new List<string>();

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var receipt = provider.GetRequiredService<IOperationsFacade>()
                .Execute("operator-soak", "SKU-0001", 2);
            if (receipt.Amount != 40m)
            {
                throw new InvalidOperationException($"Iteration {iteration} produced amount {receipt.Amount}.");
            }

            var result = await eventBus.PublishAsync(
                new OrderSubmitted("SKU-0001", 2, 20m),
                cancellationToken: cancellationToken);
            if (result.FailedCount != 0)
            {
                throw new InvalidOperationException($"Iteration {iteration} event chain reported {result.FailedCount} failures.");
            }

            workflowViewModel.SeedState(iteration);
            await eventBus.PublishAsync(new SettingsChanged($"workflow-{iteration}"), cancellationToken: cancellationToken);
            if (iteration % 5 == 0)
            {
                await workflowViewModel.RefreshCommand.ExecuteAsync(null).ConfigureAwait(false);
            }

            var route = (iteration % 3) switch
            {
                0 => await router.NavigateAsync(FixtureRoutes.Payments(), cancellationToken: cancellationToken),
                1 => await router.NavigateAsync(FixtureRoutes.Fulfillment(), cancellationToken: cancellationToken),
                _ => await router.NavigateAsync(
                    FixtureRoutes.Search(),
                    new SearchRouteParameters("regular"),
                    cancellationToken: cancellationToken),
            };
            if (route.Status != NavigationResultStatus.Success)
            {
                routeFailures.Add($"{iteration}:{route.Status}");
            }

            writer.Set(PhaseD.StateCatalog.NavigationCurrentRoute, route.ActiveRoute?.RouteId ?? "none");
        }

        var stateOk = chainHits == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.OrdersRevenue).Value == 1000m &&
                      stateRegistry.Get(PhaseD.StateCatalog.PaymentsAuthorized).Value == 1000m &&
                      stateRegistry.Get(PhaseD.StateCatalog.PaymentsCaptured).Value == 1000m &&
                      stateRegistry.Get(PhaseD.StateCatalog.FulfillmentPending).Value == 0 &&
                      stateRegistry.Get(PhaseD.StateCatalog.FulfillmentCompleted).Value == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.ShippingInTransit).Value == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.SearchQueries).Value == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.RecommendationsGenerated).Value == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.SupportOpenTickets).Value == Iterations &&
                      stateRegistry.Get(PhaseD.StateCatalog.DashboardViews).Value == Iterations;
        var vmOk = workflowViewModel.CountOf("state-reaction") == Iterations &&
                   workflowViewModel.CountOf("event") == Iterations &&
                   workflowViewModel.CountOf("refresh") == 5;

        FixtureState.Report.Record(
            "I16-workflow",
            "25 轮订单长链跨 Service/EventBus/State/Router/MVVM 后结果一致",
            stateOk && vmOk && routeFailures.Count == 0,
            $"chains={chainHits} depth={maxDepth} correlations={correlations.Count} vm=[{workflowViewModel.CountOf("state-reaction")},{workflowViewModel.CountOf("event")},{workflowViewModel.CountOf("refresh")}] routesFailed={routeFailures.Count}");

        stateRegistry.Restore(baselineSnapshot);
        var restoreOk = stateRegistry.Get(PhaseD.StateCatalog.OrdersRevenue).Value == 1000m &&
                        stateRegistry.Get(PhaseD.StateCatalog.PaymentsAuthorized).Value == 0m &&
                        stateRegistry.Get(PhaseD.StateCatalog.PaymentsCaptured).Value == 0m;

        var chainBeforeRelease = chainHits;
        workflowOwner.Dispose();
        await eventBus.PublishAsync(new OrderSubmitted("SKU-0001", 1, 20m), cancellationToken: cancellationToken);
        await workflowViewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
        var eventBefore = workflowViewModel.CountOf("event");
        await eventBus.PublishAsync(new SettingsChanged("after-release"), cancellationToken: cancellationToken);
        await Task.Delay(60, cancellationToken).ConfigureAwait(false);

        var cleanupOk = chainHits == chainBeforeRelease &&
                        workflowViewModel.CountOf("event") == eventBefore &&
                        workflowViewModel.ActivationState == ActivationState.Deactivated;
        FixtureState.Report.Record(
            "I18-soak-cleanup",
            "状态恢复后订阅与 ViewModel scope 收束，无释放后回调",
            restoreOk && cleanupOk,
            $"restore={restoreOk} chainStable={chainHits == chainBeforeRelease} vmStable={workflowViewModel.CountOf("event") == eventBefore}");

        workflowViewModel.Dispose();
        await host.StopAsync(cancellationToken);
    }
}
