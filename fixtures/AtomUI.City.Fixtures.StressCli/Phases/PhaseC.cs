using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>
/// Phase C：43 条事件契约的交付与调度验证
/// + 发布对账（I4-delivery）+ Serialized 无交叠（I4-serialized）
/// + 错误策略差异化（I4-errorpolicy）+ owner scope 释放退订（I4-unsubscribe）。
/// </summary>
public static class PhaseC
{
    private static readonly Dictionary<string, int> DeliveryCounts = new(StringComparer.Ordinal);
    private static readonly object CountGate = new();
    private static readonly List<(long Start, long End)> SerializedWindows = [];

    public static int CountOf(string key)
    {
        lock (CountGate)
        {
            return DeliveryCounts.TryGetValue(key, out var value) ? value : 0;
        }
    }

    private static void Bump(string key)
    {
        lock (CountGate)
        {
            DeliveryCounts[key] = DeliveryCounts.TryGetValue(key, out var value) ? value + 1 : 1;
        }
    }

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        DeliveryCounts.Clear();
        SerializedWindows.Clear();

        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var eventBus = host.Services.GetRequiredService<IEventBus>();

        // 订阅 owner：自建 LifecycleScope 根 + 每域一个子 scope。
        using var ownerRoot = LifecycleScope.CreateRoot(
            LifecycleScopeKind.Subscription, "fixtures-events-root");
        using var ordersOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-orders");
        using var auditOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-audit");
        using var telemetryOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-telemetry");
        using var disposableOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-disposable");

        // 43 条契约各 1 个基础订阅（Current 策略，计数对账基线）。
        IEventSubscription Subscribe<TEvent>(LifecycleScope owner, string counterKey)
            where TEvent : notnull
        {
            return eventBus.Subscribe<TEvent>(
                owner,
                (context, _) =>
                {
                    Bump(counterKey);
                    return ValueTask.CompletedTask;
                },
                EventSubscriptionOptions.Current);
        }

        var baselineSubscriptions = new List<IEventSubscription>
        {
            Subscribe<OrderSubmitted>(ordersOwner, "orders.submitted"),
            Subscribe<OrderConfirmed>(ordersOwner, "orders.confirmed"),
            Subscribe<InventoryReserved>(ordersOwner, "inventory.reserved"),
            Subscribe<InventoryLow>(ordersOwner, "inventory.low"),
            Subscribe<CustomerRegistered>(ordersOwner, "customer.registered"),
            Subscribe<BillingSettled>(ordersOwner, "billing.settled"),
            Subscribe<TaxComputed>(ordersOwner, "billing.tax"),
            Subscribe<ReportGenerated>(ordersOwner, "report.generated"),
            Subscribe<AnalyticsRefreshed>(ordersOwner, "analytics.refreshed"),
            Subscribe<NotificationDispatched>(ordersOwner, "notification.dispatched"),
            Subscribe<AuditAppended>(auditOwner, "audit.appended"),
            Subscribe<DashboardUpdated>(auditOwner, "dashboard.updated"),
            Subscribe<TelemetrySampled>(telemetryOwner, "telemetry.sampled"),
            Subscribe<ScheduleFired>(telemetryOwner, "schedule.fired"),
            Subscribe<SettingsChanged>(telemetryOwner, "settings.changed"),
            Subscribe<CatalogRebuilt>(ordersOwner, "catalog.rebuilt"),
            Subscribe<MessageJournaled>(auditOwner, "message.journaled"),
            Subscribe<WorkspaceSaved>(auditOwner, "workspace.saved"),
            Subscribe<SecurityCrossed>(auditOwner, "security.crossed"),
            Subscribe<FaultInjected>(telemetryOwner, "fault.injected"),
            Subscribe<UserSignedIn>(ordersOwner, "identity.signed-in"),
            Subscribe<TenantSwitched>(ordersOwner, "tenant.switched"),
            Subscribe<ProductIndexed>(ordersOwner, "product.indexed"),
            Subscribe<PriceChanged>(ordersOwner, "pricing.changed"),
            Subscribe<PromotionApplied>(ordersOwner, "promotion.applied"),
            Subscribe<FulfillmentPlanned>(ordersOwner, "fulfillment.planned"),
            Subscribe<PickListCreated>(ordersOwner, "picklist.created"),
            Subscribe<ShippingQuoted>(ordersOwner, "shipping.quoted"),
            Subscribe<ShipmentDispatched>(ordersOwner, "shipment.dispatched"),
            Subscribe<PaymentAuthorized>(auditOwner, "payment.authorized"),
            Subscribe<PaymentCaptured>(auditOwner, "payment.captured"),
            Subscribe<ReturnRequested>(auditOwner, "return.requested"),
            Subscribe<SearchExecuted>(telemetryOwner, "search.executed"),
            Subscribe<RecommendationProduced>(telemetryOwner, "recommendation.produced"),
            Subscribe<FraudFlagged>(auditOwner, "fraud.flagged"),
            Subscribe<SupportTicketOpened>(auditOwner, "support.opened"),
            Subscribe<RemoteProductLoaded>(ordersOwner, "remote.product-loaded"),
            Subscribe<RemoteOrderSubmitted>(ordersOwner, "remote.order-submitted"),
            Subscribe<RemoteDataFailed>(auditOwner, "remote.data-failed"),
            Subscribe<RemoteInventoryChanged>(ordersOwner, "remote.inventory-changed"),
            Subscribe<RemotePriceChanged>(ordersOwner, "remote.price-changed"),
            Subscribe<RemoteShipmentProgressed>(ordersOwner, "remote.shipment-progressed"),
            Subscribe<RemotePrincipalSwitched>(auditOwner, "remote.principal-switched"),
        };

        if (baselineSubscriptions.Count != StressCliProgram.EventContractCount)
        {
            throw new InvalidOperationException($"基线订阅数异常：{baselineSubscriptions.Count}（期望 {StressCliProgram.EventContractCount}）。");
        }

        // Serialized 无交叠探针：慢 handler + 连续发布。
        var serializedWindows = new List<(long Start, long End)>();
        var serializedGate = new object();
        using var serializedOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-serialized");
        _ = eventBus.Subscribe<TelemetrySampled>(
            serializedOwner,
            async (context, handlerCancellationToken) =>
            {
                var start = Environment.TickCount64;
                await Task.Delay(30, handlerCancellationToken).ConfigureAwait(false);
                lock (serializedGate)
                {
                    SerializedWindows.Add((start, Environment.TickCount64));
                }
            },
            EventSubscriptionOptions.Serialized.WithHandlerTimeout(TimeSpan.FromSeconds(5)));

        // 发布全部 43 条契约（Serialized 额外 3 次连续发布制造交叠压力）。
        await eventBus.PublishAsync(new OrderSubmitted("SKU-001", 2, 19.9m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new OrderConfirmed("SKU-001"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new InventoryReserved("SKU-001", 2), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new InventoryLow("SKU-001", 8), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new CustomerRegistered("客户甲"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new BillingSettled(119m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new TaxComputed(0.19m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new ReportGenerated("日报"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new AnalyticsRefreshed(3), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new NotificationDispatched("通知"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new AuditAppended("审计条目"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new DashboardUpdated("总览"), cancellationToken: cancellationToken);

        for (var sample = 1; sample <= 3; sample++)
        {
            await eventBus.PublishAsync(new TelemetrySampled(sample), cancellationToken: cancellationToken);
        }

        await eventBus.PublishAsync(new ScheduleFired("日终批处理"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new SettingsChanged("theme"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new CatalogRebuilt(1), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new MessageJournaled("日志"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new WorkspaceSaved("默认工作区"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new SecurityCrossed("边界校验"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new FaultInjected("预热"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new UserSignedIn("operator-a", "session-a"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new TenantSwitched("tenant-east", 1), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new ProductIndexed("SKU-0001", 1), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new PriceChanged("SKU-0001", 20m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new PromotionApplied("SKU-0001", 18m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new FulfillmentPlanned("order-1", "plan-1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new PickListCreated("plan-1", 1), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new ShippingQuoted("order-1", 7m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new ShipmentDispatched("order-1", "shipment-1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new PaymentAuthorized("order-1", "payment-1", 20m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new PaymentCaptured("payment-1", 20m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new ReturnRequested("payment-1", "return-1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new SearchExecuted("0001", 1), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RecommendationProduced("operator-a", "SKU-0001"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new FraudFlagged("operator-a", 0.2m), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new SupportTicketOpened("operator-a", "ticket-1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(
            new RemoteProductLoaded(new DataIntegration.StressProductSnapshot("SKU-0001", 20m, 100, "operator-a", 1)),
            cancellationToken: cancellationToken);
        await eventBus.PublishAsync(
            new RemoteOrderSubmitted(new DataIntegration.StressOrderReceipt("remote-1", "SKU-0001", 1, 20m, "operator-a", 2)),
            cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RemoteDataFailed("query", "NetworkUnavailable", "Errors.Network"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RemoteInventoryChanged("SKU-0001", 99, 1, "r1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RemotePriceChanged("SKU-0001", 20.1m, 1, "r1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RemoteShipmentProgressed("remote-1", "packed", 1, "r1"), cancellationToken: cancellationToken);
        await eventBus.PublishAsync(new RemotePrincipalSwitched("r1", "operator-b", "r2"), cancellationToken: cancellationToken);

        // I04-delivery：36 条契约计数全部为 1（Serialized 探针 3 次另计）。
        var deliveryFailures = new List<string>();
        foreach (var (key, expected) in new (string, int)[]
                 {
                     ("orders.submitted", 1), ("orders.confirmed", 1), ("inventory.reserved", 1),
                     ("inventory.low", 1), ("customer.registered", 1), ("billing.settled", 1),
                     ("billing.tax", 1), ("report.generated", 1), ("analytics.refreshed", 1),
                     ("notification.dispatched", 1), ("audit.appended", 1), ("dashboard.updated", 1),
                     ("schedule.fired", 1), ("settings.changed", 1), ("catalog.rebuilt", 1),
                     ("message.journaled", 1), ("workspace.saved", 1), ("security.crossed", 1),
                     ("fault.injected", 1),
                     ("identity.signed-in", 1), ("tenant.switched", 1), ("product.indexed", 1),
                     ("pricing.changed", 1), ("promotion.applied", 1), ("fulfillment.planned", 1),
                     ("picklist.created", 1), ("shipping.quoted", 1), ("shipment.dispatched", 1),
                     ("payment.authorized", 1), ("payment.captured", 1), ("return.requested", 1),
                     ("search.executed", 1), ("recommendation.produced", 1), ("fraud.flagged", 1),
                     ("support.opened", 1),
                     ("remote.product-loaded", 1), ("remote.order-submitted", 1),
                     ("remote.data-failed", 1), ("remote.inventory-changed", 1),
                     ("remote.price-changed", 1), ("remote.shipment-progressed", 1),
                     ("remote.principal-switched", 1),
                 })
        {
            if (CountOf(key) != expected)
            {
                deliveryFailures.Add($"{key} 期望 {expected} 实际 {CountOf(key)}");
            }
        }

        if (CountOf("telemetry.sampled") != 3)
        {
            deliveryFailures.Add($"telemetry.sampled 期望 3 实际 {CountOf("telemetry.sampled")}");
        }

        FixtureState.Report.Record(
            "I04-delivery",
            "43 条契约交付计数与发布数一致",
            deliveryFailures.Count == 0,
            deliveryFailures.Count == 0 ? null : string.Join("; ", deliveryFailures.Take(5)));

        // I4-serialized：Serialized 订阅执行窗口互不交叠。
        List<(long Start, long End)> windows;
        lock (serializedGate)
        {
            windows = [.. SerializedWindows];
        }

        windows.Sort((a, b) => a.Start.CompareTo(b.Start));
        var overlaps = new List<string>();
        for (var i = 1; i < windows.Count; i++)
        {
            if (windows[i].Start < windows[i - 1].End)
            {
                overlaps.Add($"窗口 {i - 1} 与 {i} 交叠");
            }
        }

        FixtureState.Report.Record(
            "I4-serialized",
            $"Serialized 订阅执行窗口互不交叠（{windows.Count} 个窗口）",
            overlaps.Count == 0,
            overlaps.Count == 0 ? null : string.Join("; ", overlaps.Take(3)));

        // I4-errorpolicy：三种错误策略差异化。
        // ContinueAndReport：handler 失败不影响后续订阅者。
        // StopPublication：handler 失败后同一发布内的后续订阅者不再执行。
        // FailPublisher：失败呈现在发布结果上（FailedCount > 0 或发布调用抛出）。
        using var errorOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-errorpolicy");
        var continueHits = 0;
        var stopTailHits = 0;

        _ = eventBus.Subscribe<FaultInjected>(
            errorOwner,
            (context, _) => { Bump("errorpolicy-continue"); ++continueHits; return ValueTask.CompletedTask; },
            EventSubscriptionOptions.Current.WithErrorPolicy(EventErrorPolicy.ContinueAndReport));

        _ = eventBus.Subscribe<FaultInjected>(
            errorOwner,
            (_, _) => throw new InvalidOperationException("StopPublication 探针"),
            EventSubscriptionOptions.Current.WithErrorPolicy(EventErrorPolicy.StopPublication));

        _ = eventBus.Subscribe<FaultInjected>(
            errorOwner,
            (context, _) => { Bump("errorpolicy-stop"); ++stopTailHits; return ValueTask.CompletedTask; },
            EventSubscriptionOptions.Current);

        var failPublisherObserved = false;
        try
        {
            using var failOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-failpublisher");
            _ = eventBus.Subscribe<FaultInjected>(
                failOwner,
                (_, _) => throw new InvalidOperationException("FailPublisher 探针"),
                EventSubscriptionOptions.Current.WithErrorPolicy(EventErrorPolicy.FailPublisher));

            var result = await eventBus.PublishAsync(new FaultInjected("错误策略演练"), cancellationToken: cancellationToken);

            if (result.FailedCount > 0)
            {
                failPublisherObserved = true;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failPublisherObserved = true;
        }

        var errorPolicyOk = continueHits == 1 && stopTailHits == 0 && failPublisherObserved;

        FixtureState.Report.Record(
            "I4-errorpolicy",
            "三种错误策略差异化执行（Continue 继续送达 / Stop 截断后续 / FailPublisher 上浮失败）",
            errorPolicyOk,
            $"Continue={continueHits} StopTail={stopTailHits} FailPublisherObserved={failPublisherObserved}");

        // I4-unsubscribe：owner scope 释放后发布不再送达。
        using var ephemeralOwner = ownerRoot.CreateChild(LifecycleScopeKind.Subscription, "owner-ephemeral");
        _ = eventBus.Subscribe<SettingsChanged>(
            ephemeralOwner,
            (context, _) => { Bump("ephemeral"); return ValueTask.CompletedTask; },
            EventSubscriptionOptions.Current);

        ephemeralOwner.Dispose();
        await eventBus.PublishAsync(new SettingsChanged("post-dispose"), cancellationToken: cancellationToken);

        var ephemeralAfter = CountOf("ephemeral");
        FixtureState.Report.Record(
            "I4-unsubscribe",
            "owner scope 释放后订阅不再送达",
            ephemeralAfter == 0,
            $"释放后送达 {ephemeralAfter} 次");

        await host.StopAsync(cancellationToken);

        // Host 停止后 EventBus 已释放——发布必须以 ObjectDisposedException 拒绝（停止语义）。
        var stopDrainObserved = false;
        try
        {
            await eventBus.PublishAsync(new OrderSubmitted("SKU-AFTER", 1, 1m), cancellationToken: cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            stopDrainObserved = true;
        }

        FixtureState.Report.Record(
            "I4-stopdrain",
            "Host 停止后 EventBus 拒绝发布（ObjectDisposedException）",
            stopDrainObserved,
            stopDrainObserved ? null : "停止后发布未抛 ObjectDisposedException");
    }

    private static LifecycleScopeKind LifecycleKindSubscription() => LifecycleScopeKind.Subscription;
}
