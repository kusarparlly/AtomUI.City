using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public abstract class DogfoodPageViewModel : ViewModelBase
{
    private string _activity = "Constructed";
    private int _executionCount;

    protected DogfoodPageViewModel()
    {
        PrimaryCommand = CommandFactory.CreateAsync(ExecutePrimaryAsync);
    }

    public string PageTitle => GetType().Name.Replace("ViewModel", string.Empty, StringComparison.Ordinal);

    public string Activity
    {
        get => _activity;
        private set => SetProperty(ref _activity, value);
    }

    public IAsyncRelayCommand PrimaryCommand { get; }

    protected override async ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Activity = $"Active in {context.Scope.Id}";
    }

    protected override ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity = "Deactivated";
        return ValueTask.CompletedTask;
    }

    private async Task ExecutePrimaryAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Activity = $"Primary command #{Interlocked.Increment(ref _executionCount)}";
    }
}

public abstract class DualCommandDogfoodPageViewModel : DogfoodPageViewModel
{
    protected DualCommandDogfoodPageViewModel()
    {
        SecondaryCommand = CommandFactory.Create(() => SecondaryExecutionCount++);
    }

    public int SecondaryExecutionCount { get; private set; }

    public IRelayCommand SecondaryCommand { get; }
}

public sealed class ShellViewModel : DualCommandDogfoodPageViewModel;
public sealed class NavigationViewModel : DualCommandDogfoodPageViewModel;
public sealed class AccountSwitcherViewModel : DualCommandDogfoodPageViewModel;
public sealed class TenantSwitcherViewModel : DualCommandDogfoodPageViewModel;
public sealed class ActivityCenterViewModel : DualCommandDogfoodPageViewModel;
public sealed class InspectorViewModel : DualCommandDogfoodPageViewModel;
public sealed class GlobalSearchViewModel : DualCommandDogfoodPageViewModel;
public sealed class WorkspaceViewModel : DualCommandDogfoodPageViewModel;
public sealed class KpiBoardViewModel : DualCommandDogfoodPageViewModel;
public sealed class HealthViewModel : DualCommandDogfoodPageViewModel;
public sealed class AlertsViewModel : DualCommandDogfoodPageViewModel;
public sealed class RealtimeMonitorViewModel : DualCommandDogfoodPageViewModel;
public sealed class DiagnosticsSummaryViewModel : DualCommandDogfoodPageViewModel;
public sealed class ProductListViewModel : DualCommandDogfoodPageViewModel;
public sealed class ProductDetailsViewModel : DualCommandDogfoodPageViewModel;
public sealed class ProductEditorViewModel : DualCommandDogfoodPageViewModel;
public sealed class CategoryTreeViewModel : DualCommandDogfoodPageViewModel;
public sealed class PricingBoardViewModel : DualCommandDogfoodPageViewModel;
public sealed class CurrencyEditorViewModel : DualCommandDogfoodPageViewModel;
public sealed class PromotionListViewModel : DualCommandDogfoodPageViewModel;
public sealed class PromotionEditorViewModel : DualCommandDogfoodPageViewModel;
public sealed class InventoryListViewModel : DualCommandDogfoodPageViewModel;
public sealed class StockDetailsViewModel : DualCommandDogfoodPageViewModel;
public sealed class WarehouseListViewModel : DualCommandDogfoodPageViewModel;
public sealed class WarehouseDetailsViewModel : DualCommandDogfoodPageViewModel;
public sealed class BinAllocationViewModel : DualCommandDogfoodPageViewModel;
public sealed class TransferEditorViewModel : DualCommandDogfoodPageViewModel;
public sealed class PurchaseOrderListViewModel : DualCommandDogfoodPageViewModel;
public sealed class ReceivingViewModel : DualCommandDogfoodPageViewModel;
public sealed class OrderListViewModel : DualCommandDogfoodPageViewModel;
public sealed class OrderDetailsViewModel : DualCommandDogfoodPageViewModel;
public sealed class OrderEditorViewModel : DogfoodPageViewModel;

public sealed class OrderTimelineViewModel : DogfoodPageViewModel;
public sealed class InvoiceDetailsViewModel : DogfoodPageViewModel;
public sealed class TaxPreviewViewModel : DogfoodPageViewModel;
public sealed class PaymentWizardViewModel : DogfoodPageViewModel;
public sealed class PaymentReconciliationViewModel : DogfoodPageViewModel;
public sealed class FulfillmentPlanViewModel : DogfoodPageViewModel;
public sealed class PickWaveViewModel : DogfoodPageViewModel;
public sealed class ShipmentListViewModel : DogfoodPageViewModel;
public sealed class ShipmentDetailsViewModel : DogfoodPageViewModel;
public sealed class CarrierQuoteViewModel : DogfoodPageViewModel;
public sealed class ReturnListViewModel : DogfoodPageViewModel;
public sealed class ReturnWizardViewModel : DogfoodPageViewModel;
public sealed class RefundReviewViewModel : DogfoodPageViewModel;
public sealed class CustomerListViewModel : DogfoodPageViewModel;
public sealed class CustomerDetailsViewModel : DogfoodPageViewModel;
public sealed class CustomerTimelineViewModel : DogfoodPageViewModel;
public sealed class RecommendationPanelViewModel : DogfoodPageViewModel;
public sealed class NotificationCenterViewModel : DogfoodPageViewModel;
public sealed class SupportQueueViewModel : DogfoodPageViewModel;
public sealed class SupportConversationViewModel : DogfoodPageViewModel;
public sealed class SupportComposerViewModel : DogfoodPageViewModel;
public sealed class ReportCenterViewModel : DogfoodPageViewModel;
public sealed class ReportDesignerViewModel : DogfoodPageViewModel;
public sealed class AnalyticsViewModel : DogfoodPageViewModel;
public sealed class AuditExplorerViewModel : DogfoodPageViewModel;
public sealed class UserAdminViewModel : DogfoodPageViewModel;
public sealed class PermissionAdminViewModel : DogfoodPageViewModel;
public sealed class PolicyAdminViewModel : DogfoodPageViewModel;
public sealed class SettingsViewModel : DogfoodPageViewModel;
public sealed class FeatureFlagsViewModel : DogfoodPageViewModel;
public sealed class FailureConsoleViewModel : DogfoodPageViewModel;

internal static class DogfoodViewModelCatalog
{
    public static IReadOnlyList<Type> Types { get; } = new[]
    {
        typeof(DashboardViewModel),
        typeof(ShellViewModel), typeof(NavigationViewModel), typeof(AccountSwitcherViewModel),
        typeof(TenantSwitcherViewModel), typeof(ActivityCenterViewModel), typeof(InspectorViewModel),
        typeof(GlobalSearchViewModel), typeof(WorkspaceViewModel), typeof(KpiBoardViewModel),
        typeof(HealthViewModel), typeof(AlertsViewModel), typeof(RealtimeMonitorViewModel),
        typeof(DiagnosticsSummaryViewModel), typeof(ProductListViewModel), typeof(ProductDetailsViewModel),
        typeof(ProductEditorViewModel), typeof(CategoryTreeViewModel), typeof(PricingBoardViewModel),
        typeof(CurrencyEditorViewModel), typeof(PromotionListViewModel), typeof(PromotionEditorViewModel),
        typeof(InventoryListViewModel), typeof(StockDetailsViewModel), typeof(WarehouseListViewModel),
        typeof(WarehouseDetailsViewModel), typeof(BinAllocationViewModel), typeof(TransferEditorViewModel),
        typeof(PurchaseOrderListViewModel), typeof(ReceivingViewModel), typeof(OrderListViewModel),
        typeof(OrderDetailsViewModel), typeof(OrderEditorViewModel), typeof(OrderTimelineViewModel),
        typeof(InvoiceDetailsViewModel), typeof(TaxPreviewViewModel), typeof(PaymentWizardViewModel),
        typeof(PaymentReconciliationViewModel), typeof(FulfillmentPlanViewModel), typeof(PickWaveViewModel),
        typeof(ShipmentListViewModel), typeof(ShipmentDetailsViewModel), typeof(CarrierQuoteViewModel),
        typeof(ReturnListViewModel), typeof(ReturnWizardViewModel), typeof(RefundReviewViewModel),
        typeof(CustomerListViewModel), typeof(CustomerDetailsViewModel), typeof(CustomerTimelineViewModel),
        typeof(RecommendationPanelViewModel), typeof(NotificationCenterViewModel), typeof(SupportQueueViewModel),
        typeof(SupportConversationViewModel), typeof(SupportComposerViewModel), typeof(ReportCenterViewModel),
        typeof(ReportDesignerViewModel), typeof(AnalyticsViewModel), typeof(AuditExplorerViewModel),
        typeof(UserAdminViewModel), typeof(PermissionAdminViewModel), typeof(PolicyAdminViewModel),
        typeof(SettingsViewModel), typeof(FeatureFlagsViewModel), typeof(FailureConsoleViewModel),
    };

    public static void Register(IServiceCollection services)
    {
        foreach (var type in Types)
        {
            services.AddScoped(type);
        }
    }
}
