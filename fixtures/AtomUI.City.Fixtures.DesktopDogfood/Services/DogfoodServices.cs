using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public interface IDogfoodWorkloadService
{
    string Id { get; }

    string Execute(string operation);

    ValueTask<DogfoodServiceReceipt> ExecuteAsync(
        DogfoodServiceRequest request,
        CancellationToken cancellationToken = default);
}

public abstract class DogfoodWorkloadService : IDogfoodWorkloadService
{
    private long _revision;

    public string Id => GetType().Name;

    public string Execute(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var receipt = ExecuteAsync(
                new DogfoodServiceRequest(Guid.Empty, "direct", 0, operation, IsCompensation: false))
            .AsTask()
            .GetAwaiter()
            .GetResult();
        return receipt.Signature;
    }

    public async ValueTask<DogfoodServiceReceipt> ExecuteAsync(
        DogfoodServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var role = ClassifyRole(Id);
        if (role is DogfoodServiceRole.Gateway or DogfoodServiceRole.Scheduler)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var revision = Interlocked.Increment(ref _revision);
        var signature = CreateSignature(
            $"{Id}|{role}|{request.OperationId:N}|{request.Workflow}|{request.Stage}|{request.InputDigest}|{request.IsCompensation}|{revision}");
        ServiceInvocationLedger.Record(Id);
        return new DogfoodServiceReceipt(Id, role, revision, signature, request.IsCompensation);
    }

    private static DogfoodServiceRole ClassifyRole(string id)
    {
        if (id.Contains("Store", StringComparison.Ordinal) ||
            id.Contains("Ledger", StringComparison.Ordinal) ||
            id.Contains("Journal", StringComparison.Ordinal))
        {
            return DogfoodServiceRole.Persistence;
        }

        if (id.Contains("Gateway", StringComparison.Ordinal) ||
            id.Contains("Api", StringComparison.Ordinal) ||
            id.Contains("Remote", StringComparison.Ordinal) ||
            id.Contains("Realtime", StringComparison.Ordinal))
        {
            return DogfoodServiceRole.Gateway;
        }

        if (id.Contains("Coordinator", StringComparison.Ordinal) ||
            id.Contains("Workflow", StringComparison.Ordinal) ||
            id.Contains("Orchestrator", StringComparison.Ordinal))
        {
            return DogfoodServiceRole.Orchestrator;
        }

        if (id.Contains("Projection", StringComparison.Ordinal) ||
            id.Contains("Analytics", StringComparison.Ordinal) ||
            id.Contains("Report", StringComparison.Ordinal))
        {
            return DogfoodServiceRole.Projection;
        }

        if (id.Contains("Scheduler", StringComparison.Ordinal) ||
            id.Contains("Monitor", StringComparison.Ordinal) ||
            id.Contains("Debounce", StringComparison.Ordinal))
        {
            return DogfoodServiceRole.Scheduler;
        }

        return DogfoodServiceRole.Domain;
    }

    private static string CreateSignature(string value)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return $"{hash:x16}";
    }
}

public enum DogfoodServiceRole
{
    Domain,
    Persistence,
    Gateway,
    Orchestrator,
    Projection,
    Scheduler,
}

public sealed record DogfoodServiceRequest(
    Guid OperationId,
    string Workflow,
    int Stage,
    string InputDigest,
    bool IsCompensation);

public sealed record DogfoodServiceReceipt(
    string ServiceId,
    DogfoodServiceRole Role,
    long Revision,
    string Signature,
    bool IsCompensation);

internal static class ServiceInvocationLedger
{
    private static readonly ConcurrentDictionary<string, int> Calls = new(StringComparer.Ordinal);

    public static int Count => Calls.Count;

    public static long TotalCount => Calls.Values.Sum(static count => (long)count);

    public static void Record(string serviceId)
    {
        Calls.AddOrUpdate(serviceId, 1, static (_, current) => current + 1);
    }

    public static int CountFor(string serviceId) => Calls.TryGetValue(serviceId, out var count) ? count : 0;

    public static IReadOnlyDictionary<string, int> Snapshot() =>
        new Dictionary<string, int>(Calls, StringComparer.Ordinal);
}

internal static class DogfoodServiceCatalog
{
    internal static IReadOnlyList<ServiceRegistration> Registrations { get; } = CreateRegistrations();

    public static void RegisterModule(IServiceCollection services, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        foreach (var registration in Registrations.Where(item => item.ModuleName == moduleName))
        {
            switch (registration.Lifetime)
            {
                case ServiceLifetime.Singleton when registration.Key is null:
                    services.AddSingleton(registration.ServiceType);
                    break;
                case ServiceLifetime.Scoped when registration.Key is null:
                    services.AddScoped(registration.ServiceType);
                    break;
                case ServiceLifetime.Transient when registration.Key is null:
                    services.AddTransient(registration.ServiceType);
                    break;
                case ServiceLifetime.Singleton:
                    services.AddKeyedSingleton(
                        registration.ServiceType,
                        registration.Key!,
                        registration.ServiceType);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported registration: {registration}.");
            }
        }
    }

    public static void Verify(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (Registrations.Count != 110 || Registrations.Select(item => item.ServiceType).Distinct().Count() != 110)
        {
            throw new InvalidOperationException("The Dogfood service catalog must contain 110 unique service types.");
        }

        using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        foreach (var registration in Registrations)
        {
            var instance = registration.Key is null
                ? scope.ServiceProvider.GetRequiredService(registration.ServiceType)
                : scope.ServiceProvider.GetRequiredKeyedService(registration.ServiceType, registration.Key);
            if (instance is not IDogfoodWorkloadService)
            {
                throw new InvalidOperationException($"Service '{registration.ServiceType}' is not a workload service.");
            }
        }

        if (ServiceInvocationLedger.Count != 0)
        {
            throw new InvalidOperationException(
                "Bootstrap verification must not count as business Service execution.");
        }
    }

    private static IReadOnlyList<ServiceRegistration> CreateRegistrations()
    {
        var services = new (Type Type, string Module)[]
        {
            E<SystemClockService>("Foundation"), E<CorrelationContextAccessor>("Foundation"), E<RuntimeIdentityService>("Foundation"),
            E<DiagnosticProjectionService>("Diagnostics"), E<FailureJournal>("Diagnostics"),
            E<UserSettingsService>("Settings"), E<FeatureConfigurationService>("Settings"),
            E<AtomicFileStore>("Persistence"), E<WorkspaceSnapshotStore>("Persistence"),
            E<WorkScheduler>("Scheduling"), E<DebounceCoordinator>("Scheduling"),
            E<TelemetryRecorder>("Telemetry"), E<PerformanceSampler>("Telemetry"),
            E<AuditSink>("AuditInfrastructure"), E<AuditRetentionService>("AuditInfrastructure"),
            E<ScenarioDriver>("Automation"), E<FaultGateRegistry>("Automation"), E<InvariantLedger>("Automation"),
            E<SignInWorkflow>("Identity"), E<PrincipalProjectionService>("Identity"), E<SessionExpiryMonitor>("Identity"),
            E<PermissionCatalogService>("Authorization"), E<PolicyCatalogService>("Authorization"), E<AuthorizationProjectionService>("Authorization"),
            E<AccountContextService>("AccountWorkspace"), E<TenantContextService>("AccountWorkspace"), E<AccountSwitchCoordinator>("AccountWorkspace"),
            E<ApiRequestService>("DataGateway"), E<RemoteCommandService>("DataGateway"), E<ClientCatalogService>("DataGateway"), E<DataErrorTranslator>("DataGateway"),
            E<RealtimeSessionService>("Realtime"), E<ServerEventRouter>("Realtime"), E<ReconnectCoordinator>("Realtime"),
            E<QueryCacheService>("Cache"), E<CacheInvalidationService>("Cache"), E<OfflineReadService>("Cache"),
            E<SyncOrchestrator>("Sync"), E<MutationReplayService>("Sync"), E<SequenceReconciler>("Sync"),
            E<ApplicationTextService>("LocalizationComposition"), E<CultureWorkflow>("LocalizationComposition"),
            E<ProductCatalogService>("Catalog"), E<CategoryService>("Catalog"),
            E<PricingService>("Pricing"), E<CurrencyService>("Pricing"),
            E<PromotionService>("Promotions"), E<DiscountRuleService>("Promotions"),
            E<InventoryService>("Inventory"), E<StockProjectionService>("Inventory"),
            E<WarehouseService>("Warehouse"), E<BinAllocationService>("Warehouse"),
            E<PurchaseOrderService>("Procurement"), E<ReplenishmentService>("Procurement"),
            E<CustomerService>("Customers"), E<CustomerTimelineService>("Customers"),
            E<OrderService>("Orders"), E<OrderDraftService>("Orders"),
            E<TaxService>("Tax"), E<TaxRegionService>("Tax"),
            E<InvoiceService>("Billing"), E<BillingLedgerService>("Billing"),
            E<FraudAssessmentService>("Fraud"), E<FraudRuleService>("Fraud"),
            E<PaymentService>("Payments"), E<PaymentReconciliationService>("Payments"),
            E<FulfillmentService>("Fulfillment"), E<PickWaveService>("Fulfillment"),
            E<ShipmentService>("Shipping"), E<CarrierQuoteService>("Shipping"),
            E<ReturnService>("Returns"), E<RefundService>("Returns"),
            E<SearchService>("Search"), E<SearchIndexService>("Search"),
            E<RecommendationService>("Recommendations"), E<AffinityProjectionService>("Recommendations"),
            E<NotificationService>("Notifications"), E<NotificationPreferenceService>("Notifications"),
            E<SupportTicketService>("Support"), E<ConversationService>("Support"),
            E<WorkflowEngine>("Workflow"), E<WorkflowRecoveryService>("Workflow"),
            E<ReportService>("Reporting"), E<ExportService>("Reporting"),
            E<AnalyticsService>("Analytics"), E<DashboardProjectionService>("Analytics"),
            E<AuditQueryService>("AuditDomain"), E<ComplianceExportService>("AuditDomain"),
            E<OperationsCoordinator>("Operations"), E<IncidentService>("Operations"),
            E<FeatureFlagService>("FeatureFlags"), E<ExperimentAssignmentService>("FeatureFlags"),
            E<AdministrationService>("Administration"), E<MaintenanceService>("Administration"),
            E<DashboardNavigationAdapter>("DashboardPresentation"), E<DashboardViewModelFactory>("DashboardPresentation"), E<DashboardInteractionCoordinator>("DashboardPresentation"),
            E<CommerceNavigationAdapter>("CommercePresentation"), E<CommerceViewModelFactory>("CommercePresentation"), E<CommerceInteractionCoordinator>("CommercePresentation"),
            E<FulfillmentNavigationAdapter>("FulfillmentPresentation"), E<FulfillmentViewModelFactory>("FulfillmentPresentation"), E<FulfillmentInteractionCoordinator>("FulfillmentPresentation"),
            E<CustomerCareNavigationAdapter>("CustomerCarePresentation"), E<CustomerCareViewModelFactory>("CustomerCarePresentation"), E<CustomerCareInteractionCoordinator>("CustomerCarePresentation"),
            E<AdministrationNavigationAdapter>("AdministrationPresentation"), E<AdministrationInteractionCoordinator>("AdministrationPresentation"),
            E<ShellNavigationAdapter>("ShellPresentation"), E<WindowWorkspaceCoordinator>("ShellPresentation"),
        };

        return services.Select((item, index) => new ServiceRegistration(
                item.Type,
                item.Module,
                index < 32 ? ServiceLifetime.Singleton :
                index < 72 ? ServiceLifetime.Scoped :
                index < 102 ? ServiceLifetime.Transient : ServiceLifetime.Singleton,
                index < 102 ? null : $"dogfood-{index - 101}"))
            .ToArray();
    }

    private static (Type Type, string Module) E<T>(string module) where T : IDogfoodWorkloadService =>
        (typeof(T), module);

    internal sealed record ServiceRegistration(
        Type ServiceType,
        string ModuleName,
        ServiceLifetime Lifetime,
        string? Key);
}

public sealed class SystemClockService : DogfoodWorkloadService;
public sealed class CorrelationContextAccessor : DogfoodWorkloadService;
public sealed class RuntimeIdentityService : DogfoodWorkloadService;
public sealed class DiagnosticProjectionService : DogfoodWorkloadService;
public sealed class FailureJournal : DogfoodWorkloadService;
public sealed class UserSettingsService : DogfoodWorkloadService;
public sealed class FeatureConfigurationService : DogfoodWorkloadService;
public sealed class AtomicFileStore : DogfoodWorkloadService;
public sealed class WorkspaceSnapshotStore : DogfoodWorkloadService;
public sealed class WorkScheduler : DogfoodWorkloadService;
public sealed class DebounceCoordinator : DogfoodWorkloadService;
public sealed class TelemetryRecorder : DogfoodWorkloadService;
public sealed class PerformanceSampler : DogfoodWorkloadService;
public sealed class AuditSink : DogfoodWorkloadService;
public sealed class AuditRetentionService : DogfoodWorkloadService;
public sealed class ScenarioDriver : DogfoodWorkloadService;
public sealed class FaultGateRegistry : DogfoodWorkloadService;
public sealed class InvariantLedger : DogfoodWorkloadService;
public sealed class SignInWorkflow : DogfoodWorkloadService;
public sealed class PrincipalProjectionService : DogfoodWorkloadService;
public sealed class SessionExpiryMonitor : DogfoodWorkloadService;
public sealed class PermissionCatalogService : DogfoodWorkloadService;
public sealed class PolicyCatalogService : DogfoodWorkloadService;
public sealed class AuthorizationProjectionService : DogfoodWorkloadService;
public sealed class AccountContextService : DogfoodWorkloadService;
public sealed class TenantContextService : DogfoodWorkloadService;
public sealed class AccountSwitchCoordinator : DogfoodWorkloadService;
public sealed class ApiRequestService : DogfoodWorkloadService;
public sealed class RemoteCommandService : DogfoodWorkloadService;
public sealed class ClientCatalogService : DogfoodWorkloadService;
public sealed class DataErrorTranslator : DogfoodWorkloadService;
public sealed class RealtimeSessionService : DogfoodWorkloadService;
public sealed class ServerEventRouter : DogfoodWorkloadService;
public sealed class ReconnectCoordinator : DogfoodWorkloadService;
public sealed class QueryCacheService : DogfoodWorkloadService;
public sealed class CacheInvalidationService : DogfoodWorkloadService;
public sealed class OfflineReadService : DogfoodWorkloadService;
public sealed class SyncOrchestrator : DogfoodWorkloadService;
public sealed class MutationReplayService : DogfoodWorkloadService;
public sealed class SequenceReconciler : DogfoodWorkloadService;
public sealed class ApplicationTextService : DogfoodWorkloadService;
public sealed class CultureWorkflow : DogfoodWorkloadService;
public sealed class ProductCatalogService : DogfoodWorkloadService;
public sealed class CategoryService : DogfoodWorkloadService;
public sealed class PricingService : DogfoodWorkloadService;
public sealed class CurrencyService : DogfoodWorkloadService;
public sealed class PromotionService : DogfoodWorkloadService;
public sealed class DiscountRuleService : DogfoodWorkloadService;
public sealed class InventoryService : DogfoodWorkloadService;
public sealed class StockProjectionService : DogfoodWorkloadService;
public sealed class WarehouseService : DogfoodWorkloadService;
public sealed class BinAllocationService : DogfoodWorkloadService;
public sealed class PurchaseOrderService : DogfoodWorkloadService;
public sealed class ReplenishmentService : DogfoodWorkloadService;
public sealed class CustomerService : DogfoodWorkloadService;
public sealed class CustomerTimelineService : DogfoodWorkloadService;
public sealed class OrderService : DogfoodWorkloadService;
public sealed class OrderDraftService : DogfoodWorkloadService;
public sealed class TaxService : DogfoodWorkloadService;
public sealed class TaxRegionService : DogfoodWorkloadService;
public sealed class InvoiceService : DogfoodWorkloadService;
public sealed class BillingLedgerService : DogfoodWorkloadService;
public sealed class FraudAssessmentService : DogfoodWorkloadService;
public sealed class FraudRuleService : DogfoodWorkloadService;
public sealed class PaymentService : DogfoodWorkloadService;
public sealed class PaymentReconciliationService : DogfoodWorkloadService;
public sealed class FulfillmentService : DogfoodWorkloadService;
public sealed class PickWaveService : DogfoodWorkloadService;
public sealed class ShipmentService : DogfoodWorkloadService;
public sealed class CarrierQuoteService : DogfoodWorkloadService;
public sealed class ReturnService : DogfoodWorkloadService;
public sealed class RefundService : DogfoodWorkloadService;
public sealed class SearchService : DogfoodWorkloadService;
public sealed class SearchIndexService : DogfoodWorkloadService;
public sealed class RecommendationService : DogfoodWorkloadService;
public sealed class AffinityProjectionService : DogfoodWorkloadService;
public sealed class NotificationService : DogfoodWorkloadService;
public sealed class NotificationPreferenceService : DogfoodWorkloadService;
public sealed class SupportTicketService : DogfoodWorkloadService;
public sealed class ConversationService : DogfoodWorkloadService;
public sealed class WorkflowEngine : DogfoodWorkloadService;
public sealed class WorkflowRecoveryService : DogfoodWorkloadService;
public sealed class ReportService : DogfoodWorkloadService;
public sealed class ExportService : DogfoodWorkloadService;
public sealed class AnalyticsService : DogfoodWorkloadService;
public sealed class DashboardProjectionService : DogfoodWorkloadService;
public sealed class AuditQueryService : DogfoodWorkloadService;
public sealed class ComplianceExportService : DogfoodWorkloadService;
public sealed class OperationsCoordinator : DogfoodWorkloadService;
public sealed class IncidentService : DogfoodWorkloadService;
public sealed class FeatureFlagService : DogfoodWorkloadService;
public sealed class ExperimentAssignmentService : DogfoodWorkloadService;
public sealed class AdministrationService : DogfoodWorkloadService;
public sealed class MaintenanceService : DogfoodWorkloadService;
public sealed class DashboardNavigationAdapter : DogfoodWorkloadService;
public sealed class DashboardViewModelFactory : DogfoodWorkloadService;
public sealed class DashboardInteractionCoordinator : DogfoodWorkloadService;
public sealed class CommerceNavigationAdapter : DogfoodWorkloadService;
public sealed class CommerceViewModelFactory : DogfoodWorkloadService;
public sealed class CommerceInteractionCoordinator : DogfoodWorkloadService;
public sealed class FulfillmentNavigationAdapter : DogfoodWorkloadService;
public sealed class FulfillmentViewModelFactory : DogfoodWorkloadService;
public sealed class FulfillmentInteractionCoordinator : DogfoodWorkloadService;
public sealed class CustomerCareNavigationAdapter : DogfoodWorkloadService;
public sealed class CustomerCareViewModelFactory : DogfoodWorkloadService;
public sealed class CustomerCareInteractionCoordinator : DogfoodWorkloadService;
public sealed class AdministrationNavigationAdapter : DogfoodWorkloadService;
public sealed class AdministrationInteractionCoordinator : DogfoodWorkloadService;
public sealed class ShellNavigationAdapter : DogfoodWorkloadService;
public sealed class WindowWorkspaceCoordinator : DogfoodWorkloadService;
