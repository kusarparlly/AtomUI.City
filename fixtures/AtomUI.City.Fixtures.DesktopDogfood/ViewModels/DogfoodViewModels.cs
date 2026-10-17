using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public abstract class DogfoodPageViewModel : ViewModelBase
{
    private string _activity = "Constructed";
    private int _executionCount;

    protected DogfoodPageViewModel(IUiCommandFactory commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        PrimaryCommand = commands.CreateAsync(ExecutePrimaryAsync);
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
    protected DualCommandDogfoodPageViewModel(IUiCommandFactory commands)
        : base(commands)
    {
        SecondaryCommand = commands.Create(() => SecondaryExecutionCount++);
    }

    public int SecondaryExecutionCount { get; private set; }

    public IRelayCommand SecondaryCommand { get; }
}

public sealed class ShellViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class NavigationViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class AccountSwitcherViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class TenantSwitcherViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class ActivityCenterViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class InspectorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class GlobalSearchViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class WorkspaceViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class KpiBoardViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class HealthViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class AlertsViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class RealtimeMonitorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class DiagnosticsSummaryViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class ProductListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class ProductDetailsViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class ProductEditorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class CategoryTreeViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class PricingBoardViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class CurrencyEditorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class PromotionListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class PromotionEditorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class InventoryListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class StockDetailsViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class WarehouseListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class WarehouseDetailsViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class BinAllocationViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class TransferEditorViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class PurchaseOrderListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class ReceivingViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class OrderListViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class OrderDetailsViewModel(IUiCommandFactory commands) : DualCommandDogfoodPageViewModel(commands);
public sealed class OrderEditorViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);

public sealed class OrderTimelineViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class InvoiceDetailsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class TaxPreviewViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class PaymentWizardViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class PaymentReconciliationViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class FulfillmentPlanViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class PickWaveViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ShipmentListViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ShipmentDetailsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class CarrierQuoteViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ReturnListViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ReturnWizardViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class RefundReviewViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class CustomerListViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class CustomerDetailsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class CustomerTimelineViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class RecommendationPanelViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class NotificationCenterViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class SupportQueueViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class SupportConversationViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class SupportComposerViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ReportCenterViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class ReportDesignerViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class AnalyticsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class AuditExplorerViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class UserAdminViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class PermissionAdminViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class PolicyAdminViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class SettingsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class FeatureFlagsViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);
public sealed class FailureConsoleViewModel(IUiCommandFactory commands) : DogfoodPageViewModel(commands);

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
