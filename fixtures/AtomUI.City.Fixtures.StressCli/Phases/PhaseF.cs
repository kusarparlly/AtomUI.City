using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
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

/// <summary>Phase F：三轮订单通过五个框架模块形成短联合闭环。</summary>
public static class PhaseF
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var eventBus = host.Services.GetRequiredService<IEventBus>();
        var state = host.Services.GetRequiredService<IApplicationState>();
        var writer = host.Services.GetRequiredService<IApplicationStateWriter>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var eventOwnerRoot = host.Services.GetRequiredService<LifecycleScope>();
        var registry = host.Services.GetRequiredService<IStateRegistry>();
        registry.Add(StateDefinition.Create(
            new StateKey<int>("fixtures.viewmodel.dashboard"),
            0,
            access: StateAccessPolicy.HostWrite));

        using var businessScope = host.Services.CreateScope();
        var ledger = businessScope.ServiceProvider.GetRequiredService<IBillingLedger>();
        var calculator = host.Services.GetRequiredService<IBillingCalculator>();
        var audit = host.Services.GetRequiredService<IAuditTrail>();
        var router = businessScope.ServiceProvider.GetRequiredService<IRouter>();

        var dashboard = new DashboardViewModel(eventBus, eventOwnerRoot, state, writer, diagnostics);
        await dashboard.ActivateAsync(new ActivationScope(diagnostics), cancellationToken).ConfigureAwait(false);

        using var handlers = eventOwnerRoot.CreateChild(LifecycleScopeKind.Subscription, "phase-f-chain");
        var routed = 0;
        _ = eventBus.Subscribe<OrderSubmitted>(handlers, async (context, handlerCancellationToken) =>
        {
            var amount = calculator.Settle(context.Event.Price * context.Event.Quantity);
            await eventBus.PublishAsync(new BillingSettled(amount), cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<BillingSettled>(handlers, async (context, handlerCancellationToken) =>
        {
            ledger.Record(context.Event.Amount);
            dashboard.SeedState(ledger.Settled);
            await eventBus.PublishAsync(
                new AuditAppended($"billing:{context.Event.Amount}"),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<AuditAppended>(handlers, async (context, handlerCancellationToken) =>
        {
            audit.Append(context.Event.Entry);
            await eventBus.PublishAsync(
                new DashboardUpdated($"round:{audit.Entries}"),
                cancellationToken: handlerCancellationToken);
        });
        _ = eventBus.Subscribe<DashboardUpdated>(handlers, async (context, handlerCancellationToken) =>
        {
            var navigation = await router.NavigateAsync(
                FixtureRoutes.Dashboard(),
                new NavigationOptions { ForceReload = true },
                handlerCancellationToken);
            if (navigation.Status == NavigationResultStatus.Success)
            {
                Interlocked.Increment(ref routed);
            }

            await dashboard.RefreshCommand.ExecuteAsync(null).ConfigureAwait(false);
        });

        var publicationFailures = 0;
        for (var round = 1; round <= 3; round++)
        {
            var result = await eventBus.PublishAsync(
                new OrderSubmitted($"SKU-F{round}", round, 100m),
                cancellationToken: cancellationToken);
            publicationFailures += result.FailedCount;
        }

        var jointOk = publicationFailures == 0 && ledger.Settled == 3 && audit.Entries == 3 &&
                      dashboard.CountOf("state-reaction") == 3 &&
                      dashboard.CountOf("refresh") == 3 && routed == 3;
        FixtureState.Report.Record(
            "I16-short-chain",
            "三轮订单通过 Core/Service→EventBus→State→MVVM→Router 完成闭环",
            jointOk,
            $"ledger={ledger.Settled} audit={audit.Entries} state={dashboard.CountOf("state-reaction")} refresh={dashboard.CountOf("refresh")} routed={routed} failures={publicationFailures}");

        await dashboard.DeactivateAsync(cancellationToken).ConfigureAwait(false);
        dashboard.Dispose();
        await host.StopAsync(cancellationToken);
    }
}
