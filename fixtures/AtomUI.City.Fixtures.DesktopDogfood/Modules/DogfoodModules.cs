using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Presentation;
using AtomUI.City.Routing;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodModuleCatalog
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Foundation", "Diagnostics", "Settings", "Persistence", "Scheduling", "Telemetry",
        "AuditInfrastructure", "Automation", "Identity", "Authorization", "AccountWorkspace",
        "DataGateway", "Realtime", "Cache", "Sync", "LocalizationComposition", "Catalog",
        "Pricing", "Promotions", "Inventory", "Warehouse", "Procurement", "Customers", "Orders",
        "Tax", "Billing", "Fraud", "Payments", "Fulfillment", "Shipping", "Returns", "Search",
        "Recommendations", "Notifications", "Support", "Workflow", "Reporting", "Analytics",
        "AuditDomain", "Operations", "FeatureFlags", "Administration", "DashboardPresentation",
        "CommercePresentation", "FulfillmentPresentation", "CustomerCarePresentation",
        "AdministrationPresentation", "ShellPresentation",
    ];
}

public abstract class DogfoodModuleBase : ModuleBase
{
    protected DogfoodModuleBase(string moduleName)
    {
        ModuleName = moduleName;
        ModuleRuntimeLedger.Constructed(moduleName);
    }

    protected string ModuleName { get; }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        DogfoodServiceCatalog.RegisterModule(context.Services, ModuleName);
        ModuleRuntimeLedger.Configured(ModuleName);
    }

    public override async ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        ModuleRuntimeLedger.Initialized(ModuleName);
    }

    public override async ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        ModuleRuntimeLedger.Stopped(ModuleName);
    }
}

[Module("Dogfood.Foundation")]
public sealed class FoundationModule() : DogfoodModuleBase("Foundation");

[Module("Dogfood.Diagnostics"), DependsOn(typeof(FoundationModule))]
public sealed class DiagnosticsModule() : DogfoodModuleBase("Diagnostics");

[Module("Dogfood.Settings"), DependsOn(typeof(FoundationModule)), DependsOn(typeof(DiagnosticsModule))]
public sealed class SettingsModule() : DogfoodModuleBase("Settings");

[Module("Dogfood.Persistence"), DependsOn(typeof(FoundationModule)), DependsOn(typeof(SettingsModule)), DependsOn(typeof(DiagnosticsModule))]
public sealed class PersistenceModule() : DogfoodModuleBase("Persistence");

[Module("Dogfood.Scheduling"), DependsOn(typeof(FoundationModule)), DependsOn(typeof(DiagnosticsModule))]
public sealed class SchedulingModule() : DogfoodModuleBase("Scheduling");

[Module("Dogfood.Telemetry"), DependsOn(typeof(FoundationModule)), DependsOn(typeof(DiagnosticsModule)), DependsOn(typeof(SchedulingModule))]
public sealed class TelemetryModule() : DogfoodModuleBase("Telemetry");

[Module("Dogfood.AuditInfrastructure"), DependsOn(typeof(PersistenceModule)), DependsOn(typeof(TelemetryModule))]
public sealed class AuditInfrastructureModule() : DogfoodModuleBase("AuditInfrastructure");

[Module("Dogfood.Automation"), DependsOn(typeof(DiagnosticsModule)), DependsOn(typeof(SchedulingModule)), DependsOn(typeof(TelemetryModule))]
public sealed class AutomationModule() : DogfoodModuleBase("Automation");

[Module("Dogfood.Identity"), DependsOn(typeof(FoundationModule)), DependsOn(typeof(PersistenceModule)), DependsOn(typeof(AuditInfrastructureModule))]
public sealed class IdentityModule() : DogfoodModuleBase("Identity");

[Module("Dogfood.Authorization"), DependsOn(typeof(IdentityModule)), DependsOn(typeof(DiagnosticsModule))]
public sealed class AuthorizationModule() : DogfoodModuleBase("Authorization");

[Module("Dogfood.AccountWorkspace"), DependsOn(typeof(IdentityModule)), DependsOn(typeof(AuthorizationModule)), DependsOn(typeof(SettingsModule))]
public sealed class AccountWorkspaceModule() : DogfoodModuleBase("AccountWorkspace");

[Module("Dogfood.DataGateway"), DependsOn(typeof(IdentityModule)), DependsOn(typeof(AuthorizationModule)), DependsOn(typeof(TelemetryModule))]
public sealed class DataGatewayModule() : DogfoodModuleBase("DataGateway");

[Module("Dogfood.Realtime"), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(SchedulingModule)), DependsOn(typeof(TelemetryModule))]
public sealed class RealtimeModule() : DogfoodModuleBase("Realtime");

[Module("Dogfood.Cache"), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(PersistenceModule)), DependsOn(typeof(TelemetryModule))]
public sealed class CacheModule() : DogfoodModuleBase("Cache");

[Module("Dogfood.Sync"), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(CacheModule)), DependsOn(typeof(SchedulingModule))]
public sealed class SyncModule() : DogfoodModuleBase("Sync");

[Module("Dogfood.LocalizationComposition"), DependsOn(typeof(SettingsModule)), DependsOn(typeof(PersistenceModule)), DependsOn(typeof(DiagnosticsModule))]
public sealed class LocalizationCompositionModule() : DogfoodModuleBase("LocalizationComposition");

[Module("Dogfood.Catalog"), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(CacheModule)), DependsOn(typeof(LocalizationCompositionModule))]
public sealed class CatalogModule() : DogfoodModuleBase("Catalog");

[Module("Dogfood.Pricing"), DependsOn(typeof(CatalogModule)), DependsOn(typeof(SettingsModule)), DependsOn(typeof(RealtimeModule))]
public sealed class PricingModule() : DogfoodModuleBase("Pricing");

[Module("Dogfood.Promotions"), DependsOn(typeof(PricingModule)), DependsOn(typeof(AccountWorkspaceModule)), DependsOn(typeof(SchedulingModule))]
public sealed class PromotionsModule() : DogfoodModuleBase("Promotions");

[Module("Dogfood.Inventory"), DependsOn(typeof(CatalogModule)), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(CacheModule))]
public sealed class InventoryModule() : DogfoodModuleBase("Inventory");

[Module("Dogfood.Warehouse"), DependsOn(typeof(InventoryModule)), DependsOn(typeof(SettingsModule))]
public sealed class WarehouseModule() : DogfoodModuleBase("Warehouse");

[Module("Dogfood.Procurement"), DependsOn(typeof(InventoryModule)), DependsOn(typeof(WarehouseModule)), DependsOn(typeof(DataGatewayModule))]
public sealed class ProcurementModule() : DogfoodModuleBase("Procurement");

[Module("Dogfood.Customers"), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(CacheModule)), DependsOn(typeof(IdentityModule))]
public sealed class CustomersModule() : DogfoodModuleBase("Customers");

[Module("Dogfood.Orders"), DependsOn(typeof(CatalogModule)), DependsOn(typeof(InventoryModule)), DependsOn(typeof(CustomersModule)), DependsOn(typeof(PricingModule))]
public sealed class OrdersModule() : DogfoodModuleBase("Orders");

[Module("Dogfood.Tax"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(SettingsModule)), DependsOn(typeof(AccountWorkspaceModule))]
public sealed class TaxModule() : DogfoodModuleBase("Tax");

[Module("Dogfood.Billing"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(TaxModule)), DependsOn(typeof(AuditInfrastructureModule))]
public sealed class BillingModule() : DogfoodModuleBase("Billing");

[Module("Dogfood.Fraud"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(AuthorizationModule)), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(TelemetryModule))]
public sealed class FraudModule() : DogfoodModuleBase("Fraud");

[Module("Dogfood.Payments"), DependsOn(typeof(BillingModule)), DependsOn(typeof(FraudModule)), DependsOn(typeof(DataGatewayModule)), DependsOn(typeof(AuditInfrastructureModule))]
public sealed class PaymentsModule() : DogfoodModuleBase("Payments");

[Module("Dogfood.Fulfillment"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(InventoryModule)), DependsOn(typeof(WarehouseModule)), DependsOn(typeof(SchedulingModule))]
public sealed class FulfillmentModule() : DogfoodModuleBase("Fulfillment");

[Module("Dogfood.Shipping"), DependsOn(typeof(FulfillmentModule)), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(DataGatewayModule))]
public sealed class ShippingModule() : DogfoodModuleBase("Shipping");

[Module("Dogfood.Returns"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(InventoryModule)), DependsOn(typeof(PaymentsModule)), DependsOn(typeof(ShippingModule))]
public sealed class ReturnsModule() : DogfoodModuleBase("Returns");

[Module("Dogfood.Search"), DependsOn(typeof(CatalogModule)), DependsOn(typeof(CacheModule)), DependsOn(typeof(TelemetryModule))]
public sealed class SearchModule() : DogfoodModuleBase("Search");

[Module("Dogfood.Recommendations"), DependsOn(typeof(SearchModule)), DependsOn(typeof(CustomersModule)), DependsOn(typeof(PricingModule))]
public sealed class RecommendationsModule() : DogfoodModuleBase("Recommendations");

[Module("Dogfood.Notifications"), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(LocalizationCompositionModule)), DependsOn(typeof(CustomersModule))]
public sealed class NotificationsModule() : DogfoodModuleBase("Notifications");

[Module("Dogfood.Support"), DependsOn(typeof(CustomersModule)), DependsOn(typeof(OrdersModule)), DependsOn(typeof(NotificationsModule)), DependsOn(typeof(AuthorizationModule))]
public sealed class SupportModule() : DogfoodModuleBase("Support");

[Module("Dogfood.Workflow"), DependsOn(typeof(SchedulingModule)), DependsOn(typeof(AuditInfrastructureModule)), DependsOn(typeof(NotificationsModule))]
public sealed class WorkflowModule() : DogfoodModuleBase("Workflow");

[Module("Dogfood.Reporting"), DependsOn(typeof(OrdersModule)), DependsOn(typeof(BillingModule)), DependsOn(typeof(ReturnsModule)), DependsOn(typeof(DataGatewayModule))]
public sealed class ReportingModule() : DogfoodModuleBase("Reporting");

[Module("Dogfood.Analytics"), DependsOn(typeof(ReportingModule)), DependsOn(typeof(InventoryModule)), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(TelemetryModule))]
public sealed class AnalyticsModule() : DogfoodModuleBase("Analytics");

[Module("Dogfood.AuditDomain"), DependsOn(typeof(AuditInfrastructureModule)), DependsOn(typeof(IdentityModule)), DependsOn(typeof(AuthorizationModule))]
public sealed class AuditDomainModule() : DogfoodModuleBase("AuditDomain");

[Module("Dogfood.Operations"), DependsOn(typeof(WorkflowModule)), DependsOn(typeof(FulfillmentModule)), DependsOn(typeof(PaymentsModule)), DependsOn(typeof(ShippingModule)), DependsOn(typeof(SupportModule))]
public sealed class OperationsModule() : DogfoodModuleBase("Operations");

[Module("Dogfood.FeatureFlags"), DependsOn(typeof(SettingsModule)), DependsOn(typeof(AuthorizationModule)), DependsOn(typeof(TelemetryModule))]
public sealed class FeatureFlagsModule() : DogfoodModuleBase("FeatureFlags");

[Module("Dogfood.Administration"), DependsOn(typeof(AuthorizationModule)), DependsOn(typeof(FeatureFlagsModule)), DependsOn(typeof(AuditDomainModule)), DependsOn(typeof(OperationsModule))]
public sealed class AdministrationModule() : DogfoodModuleBase("Administration");

[Module("Dogfood.DashboardPresentation"), DependsOn(typeof(AnalyticsModule)), DependsOn(typeof(OperationsModule)), DependsOn(typeof(RealtimeModule)), DependsOn(typeof(LocalizationCompositionModule))]
public sealed class DashboardPresentationModule() : DogfoodModuleBase("DashboardPresentation");

[Module("Dogfood.CommercePresentation"), DependsOn(typeof(CatalogModule)), DependsOn(typeof(PricingModule)), DependsOn(typeof(PromotionsModule)), DependsOn(typeof(OrdersModule)), DependsOn(typeof(BillingModule))]
public sealed class CommercePresentationModule() : DogfoodModuleBase("CommercePresentation");

[Module("Dogfood.FulfillmentPresentation"), DependsOn(typeof(InventoryModule)), DependsOn(typeof(WarehouseModule)), DependsOn(typeof(ProcurementModule)), DependsOn(typeof(FulfillmentModule)), DependsOn(typeof(ShippingModule)), DependsOn(typeof(ReturnsModule))]
public sealed class FulfillmentPresentationModule() : DogfoodModuleBase("FulfillmentPresentation");

[Module("Dogfood.CustomerCarePresentation"), DependsOn(typeof(CustomersModule)), DependsOn(typeof(RecommendationsModule)), DependsOn(typeof(NotificationsModule)), DependsOn(typeof(SupportModule))]
public sealed class CustomerCarePresentationModule() : DogfoodModuleBase("CustomerCarePresentation");

[Module("Dogfood.AdministrationPresentation"), DependsOn(typeof(AdministrationModule)), DependsOn(typeof(AuditDomainModule)), DependsOn(typeof(FeatureFlagsModule))]
public sealed class AdministrationPresentationModule() : DogfoodModuleBase("AdministrationPresentation");

[ApplicationModule]
[ServiceRegistrationOwner]
[Module("Dogfood.ShellPresentation")]
[DependsOn(typeof(EventBusModule))]
[DependsOn(typeof(RoutingModule))]
[DependsOn(typeof(DataModule))]
[DependsOn(typeof(PresentationModule))]
[DependsOn(typeof(DashboardPresentationModule))]
[DependsOn(typeof(CommercePresentationModule))]
[DependsOn(typeof(FulfillmentPresentationModule))]
[DependsOn(typeof(CustomerCarePresentationModule))]
[DependsOn(typeof(AdministrationPresentationModule))]
[DependsOn(typeof(AutomationModule))]
[DependsOn(typeof(SyncModule))]
public sealed class ShellPresentationModule() : DogfoodModuleBase("ShellPresentation");
