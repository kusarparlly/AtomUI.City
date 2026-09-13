using AtomUI.City.EventBus;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public interface IDogfoodEvent
{
    Guid OperationId { get; }
    string Subject { get; }
    int Revision { get; }
}

[EventContract("dogfood.host.application-ready", typeof(FoundationModule), SchemaVersion = 2)]
[EventChannel("critical", Capacity = 64, BackpressurePolicy = EventChannelBackpressurePolicy.Wait)]
public sealed record ApplicationReady(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.application-stopping", typeof(FoundationModule))]
public sealed record ApplicationStopping(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.module-health-changed", typeof(DiagnosticsModule))]
public sealed record ModuleHealthChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.configuration-reload-requested", typeof(SettingsModule))]
public sealed record ConfigurationReloadRequested(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.settings-changed", typeof(SettingsModule))]
public sealed record SettingsChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.diagnostic-raised", typeof(DiagnosticsModule))]
[EventChannel("telemetry", Capacity = 512, BackpressurePolicy = EventChannelBackpressurePolicy.DropOldest, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 4)]
public sealed record DiagnosticRaised(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.feature-flag-changed", typeof(FeatureFlagsModule))]
public sealed record FeatureFlagChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.host.automation-checkpoint-reached", typeof(AutomationModule))]
[EventChannel("chaos-tiny", Capacity = 2, BackpressurePolicy = EventChannelBackpressurePolicy.Reject)]
public sealed record AutomationCheckpointReached(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;

[EventContract("dogfood.identity.sign-in-requested", typeof(IdentityModule))]
public sealed record SignInRequested(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.user-signed-in", typeof(IdentityModule), SchemaVersion = 2)]
public sealed record UserSignedIn(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.sign-in-failed", typeof(IdentityModule))]
public sealed record SignInFailed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.user-signed-out", typeof(IdentityModule))]
public sealed record UserSignedOut(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.tenant-switch-requested", typeof(AccountWorkspaceModule))]
public sealed record TenantSwitchRequested(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.tenant-switched", typeof(AccountWorkspaceModule), SchemaVersion = 2)]
public sealed record TenantSwitched(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.permissions-changed", typeof(AuthorizationModule), SchemaVersion = 2)]
public sealed record PermissionsChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.identity.access-token-expiring", typeof(IdentityModule))]
public sealed record AccessTokenExpiring(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;

[EventContract("dogfood.data.network-reachability-changed", typeof(DataGatewayModule))]
public sealed record NetworkReachabilityChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.request-failed", typeof(DataGatewayModule), SchemaVersion = 2)]
public sealed record DataRequestFailed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.cache-invalidated", typeof(CacheModule))]
public sealed record CacheInvalidated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.sync-started", typeof(SyncModule))]
public sealed record SyncStarted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.sync-completed", typeof(SyncModule))]
public sealed record SyncCompleted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.sync-failed", typeof(SyncModule))]
public sealed record SyncFailed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.realtime-connected", typeof(RealtimeModule))]
[EventChannel("realtime", Capacity = 256, BackpressurePolicy = EventChannelBackpressurePolicy.Reject, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 4)]
public sealed record RealtimeConnected(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.realtime-disconnected", typeof(RealtimeModule))]
public sealed record RealtimeDisconnected(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.remote-sequence-gap-detected", typeof(RealtimeModule))]
public sealed record RemoteSequenceGapDetected(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.data.large-transfer-progressed", typeof(DataGatewayModule))]
public sealed record LargeTransferProgressed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;

[EventContract("dogfood.commerce.product-created", typeof(CatalogModule))]
public sealed record ProductCreated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.product-updated", typeof(CatalogModule))]
public sealed record ProductUpdated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.catalog-rebuilt", typeof(CatalogModule))]
public sealed record CatalogRebuilt(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.price-changed", typeof(PricingModule))]
public sealed record PriceChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.currency-changed", typeof(PricingModule))]
public sealed record CurrencyChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.promotion-activated", typeof(PromotionsModule))]
public sealed record PromotionActivated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.inventory-adjusted", typeof(InventoryModule))]
[EventChannel("inventory", Capacity = 128, BackpressurePolicy = EventChannelBackpressurePolicy.CoalesceLatest, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 4)]
public sealed record InventoryAdjusted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.inventory-low", typeof(InventoryModule))]
public sealed record InventoryLow(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.warehouse-capacity-changed", typeof(WarehouseModule))]
public sealed record WarehouseCapacityChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.purchase-order-created", typeof(ProcurementModule))]
public sealed record PurchaseOrderCreated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.customer-created", typeof(CustomersModule))]
public sealed record CustomerCreated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.customer-updated", typeof(CustomersModule))]
public sealed record CustomerUpdated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.order-draft-saved", typeof(OrdersModule))]
public sealed record OrderDraftSaved(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.order-submitted", typeof(OrdersModule), SchemaVersion = 2)]
[EventChannel("orders", Capacity = 128, BackpressurePolicy = EventChannelBackpressurePolicy.Wait, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 4)]
public sealed record OrderSubmitted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.order-confirmed", typeof(OrdersModule))]
public sealed record OrderConfirmed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.tax-calculated", typeof(TaxModule))]
public sealed record TaxCalculated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.invoice-issued", typeof(BillingModule))]
public sealed record InvoiceIssued(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.billing-settled", typeof(BillingModule))]
public sealed record BillingSettled(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.cart-abandoned", typeof(OrdersModule))]
public sealed record CartAbandoned(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.commerce.sales-target-changed", typeof(AnalyticsModule))]
public sealed record SalesTargetChanged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;

[EventContract("dogfood.workflow.fraud-assessment-completed", typeof(FraudModule))]
public sealed record FraudAssessmentCompleted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.fraud-flagged", typeof(FraudModule))]
public sealed record FraudFlagged(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.payment-authorized", typeof(PaymentsModule))]
public sealed record PaymentAuthorized(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.payment-captured", typeof(PaymentsModule))]
public sealed record PaymentCaptured(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.payment-failed", typeof(PaymentsModule))]
public sealed record PaymentFailed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.fulfillment-planned", typeof(FulfillmentModule))]
public sealed record FulfillmentPlanned(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.pick-wave-created", typeof(FulfillmentModule))]
public sealed record PickWaveCreated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.shipment-quoted", typeof(ShippingModule))]
public sealed record ShipmentQuoted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.shipment-dispatched", typeof(ShippingModule))]
public sealed record ShipmentDispatched(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.shipment-progressed", typeof(ShippingModule))]
public sealed record ShipmentProgressed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.return-requested", typeof(ReturnsModule))]
public sealed record ReturnRequested(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.return-approved", typeof(ReturnsModule))]
public sealed record ReturnApproved(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.refund-completed", typeof(ReturnsModule))]
public sealed record RefundCompleted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.workflow.compensated", typeof(WorkflowModule))]
[EventChannel("workflow", Capacity = 128, BackpressurePolicy = EventChannelBackpressurePolicy.Wait, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 4)]
public sealed record WorkflowCompensated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;

[EventContract("dogfood.ui.search-executed", typeof(SearchModule))]
[EventChannel("search", Capacity = 64, BackpressurePolicy = EventChannelBackpressurePolicy.DropOldest, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 4)]
public sealed record SearchExecuted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.recommendation-produced", typeof(RecommendationsModule))]
public sealed record RecommendationProduced(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.notification-raised", typeof(NotificationsModule))]
[EventChannel("notifications", Capacity = 128, BackpressurePolicy = EventChannelBackpressurePolicy.DropNewest, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 4)]
public sealed record NotificationRaised(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.support-ticket-opened", typeof(SupportModule))]
public sealed record SupportTicketOpened(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.support-message-received", typeof(SupportModule))]
public sealed record SupportMessageReceived(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.report-generated", typeof(ReportingModule))]
[EventChannel("background", Capacity = 128, BackpressurePolicy = EventChannelBackpressurePolicy.Wait, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 4)]
public sealed record ReportGenerated(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.analytics-refreshed", typeof(AnalyticsModule))]
public sealed record AnalyticsRefreshed(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.audit-appended", typeof(AuditDomainModule))]
[EventChannel("audit", Capacity = 256, BackpressurePolicy = EventChannelBackpressurePolicy.Wait)]
public sealed record AuditAppended(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.navigation-committed", typeof(ShellPresentationModule), SchemaVersion = 2)]
public sealed record NavigationCommitted(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.outlet-reconciled", typeof(ShellPresentationModule))]
[EventChannel("ui-feedback", Capacity = 32, BackpressurePolicy = EventChannelBackpressurePolicy.CoalesceLatest)]
public sealed record OutletReconciled(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.window-workspace-opened", typeof(ShellPresentationModule))]
public sealed record WindowWorkspaceOpened(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
[EventContract("dogfood.ui.user-action-rejected", typeof(ShellPresentationModule))]
public sealed record UserActionRejected(Guid OperationId, string Subject, int Revision) : IDogfoodEvent;
