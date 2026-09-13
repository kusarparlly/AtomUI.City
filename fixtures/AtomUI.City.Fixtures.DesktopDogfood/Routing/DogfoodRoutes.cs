using AtomUI.City.Routing;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public readonly record struct WorkspaceRouteParameters(Guid Id);
public readonly record struct ProductRouteParameters(string Sku);
public readonly record struct WarehouseRouteParameters(Guid Id);
public readonly record struct OrderRouteParameters(Guid Id);
public sealed record SearchRouteParameters
{
    public required string Term { get; init; }

    [Query]
    public int Page { get; init; } = 1;

    [Fragment]
    public string? Selection { get; init; }
}

[RouteMap]
public static partial class DogfoodRoutes
{
    // 6 layouts
    [LayoutRoute(typeof(ShellViewModel), Id = "dogfood.shell", Outlet = "primary")]
    public static partial RouteReference Shell();

    [LayoutRoute(typeof(NavigationViewModel), Id = "dogfood.layout.commerce", Parent = nameof(Shell))]
    public static partial RouteReference CommerceLayout();

    [LayoutRoute(typeof(FulfillmentPlanViewModel), Id = "dogfood.layout.fulfillment", Parent = nameof(Shell))]
    public static partial RouteReference FulfillmentLayout();

    [LayoutRoute(typeof(CustomerListViewModel), Id = "dogfood.layout.customer-care", Parent = nameof(Shell))]
    public static partial RouteReference CustomerCareLayout();

    [LayoutRoute(typeof(UserAdminViewModel), Id = "dogfood.layout.administration", Parent = nameof(Shell))]
    public static partial RouteReference AdministrationLayout();

    [LayoutRoute(typeof(FailureConsoleViewModel), Id = "dogfood.layout.diagnostics", Parent = nameof(Shell))]
    public static partial RouteReference DiagnosticsLayout();

    // 8 groups
    [RouteGroup("dashboard", Id = "dogfood.group.dashboard", Parent = nameof(Shell))]
    public static partial RouteReference DashboardGroup();

    [RouteGroup("catalog", Id = "dogfood.group.catalog", Parent = nameof(CommerceLayout))]
    public static partial RouteReference CatalogGroup();

    [RouteGroup("inventory", Id = "dogfood.group.inventory", Parent = nameof(CommerceLayout))]
    public static partial RouteReference InventoryGroup();

    [RouteGroup("orders", Id = "dogfood.group.orders", Parent = nameof(CommerceLayout))]
    public static partial RouteReference OrdersGroup();

    [RouteGroup("billing", Id = "dogfood.group.billing", Parent = nameof(CommerceLayout))]
    public static partial RouteReference BillingGroup();

    [RouteGroup("shipping", Id = "dogfood.group.shipping", Parent = nameof(FulfillmentLayout))]
    public static partial RouteReference ShippingGroup();

    [RouteGroup("support", Id = "dogfood.group.support", Parent = nameof(CustomerCareLayout))]
    public static partial RouteReference SupportGroup();

    [RouteGroup("reports", Id = "dogfood.group.reports", Parent = nameof(AdministrationLayout))]
    public static partial RouteReference ReportsGroup();

    // 10 indexes
    [IndexRoute(typeof(WorkspaceViewModel), Id = "dogfood.index.shell", Parent = nameof(Shell))]
    public static partial RouteReference ShellIndex();

    [IndexRoute(typeof(DashboardViewModel), Id = "dogfood.index.dashboard", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardIndex();

    [IndexRoute(typeof(ProductListViewModel), Id = "dogfood.index.catalog", Parent = nameof(CatalogGroup))]
    public static partial RouteReference CatalogIndex();

    [IndexRoute(typeof(InventoryListViewModel), Id = "dogfood.index.inventory", Parent = nameof(InventoryGroup))]
    public static partial RouteReference InventoryIndex();

    [IndexRoute(typeof(OrderListViewModel), Id = "dogfood.index.orders", Parent = nameof(OrdersGroup))]
    public static partial RouteReference OrdersIndex();

    [IndexRoute(typeof(PaymentWizardViewModel), Id = "dogfood.index.billing", Parent = nameof(BillingGroup))]
    public static partial RouteReference BillingIndex();

    [IndexRoute(typeof(ShipmentListViewModel), Id = "dogfood.index.shipping", Parent = nameof(ShippingGroup))]
    public static partial RouteReference ShippingIndex();

    [IndexRoute(typeof(SupportQueueViewModel), Id = "dogfood.index.support", Parent = nameof(SupportGroup))]
    public static partial RouteReference SupportIndex();

    [IndexRoute(typeof(ReportCenterViewModel), Id = "dogfood.index.reports", Parent = nameof(ReportsGroup))]
    public static partial RouteReference ReportsIndex();

    [IndexRoute(typeof(DiagnosticsSummaryViewModel), Id = "dogfood.index.diagnostics", Parent = nameof(DiagnosticsLayout))]
    public static partial RouteReference DiagnosticsIndex();

    // 60 navigable routes. Templates collectively cover literals, optional/default/catch-all
    // parameters and every built-in constraint supported by the Router.
    [Route("workspace/{id:guid}", typeof(WorkspaceViewModel), Id = "dogfood.workspace", Parent = nameof(Shell))]
    public static partial RouteReference<WorkspaceRouteParameters> Workspace();

    [Route("popout/{enabled:bool}", typeof(ShellViewModel), Id = "dogfood.popout", Parent = nameof(Shell))]
    public static partial RouteReference Popout();

    [Route("search/{term:minlength(2)}", typeof(GlobalSearchViewModel), Id = "dogfood.search", Parent = nameof(Shell))]
    public static partial RouteReference<SearchRouteParameters> Search();

    [Route("account/{id?}", typeof(AccountSwitcherViewModel), Id = "dogfood.account", Parent = nameof(Shell), Outlet = "inspector")]
    public static partial RouteReference Account();

    [Route("tenant/{code:alpha:length(3)=usa}", typeof(TenantSwitcherViewModel), Id = "dogfood.tenant", Parent = nameof(Shell), Outlet = "inspector")]
    public static partial RouteReference Tenant();

    [Route("activity/{*path:maxlength(80)}", typeof(ActivityCenterViewModel), Id = "dogfood.activity", Parent = nameof(Shell), Outlet = "activity")]
    public static partial RouteReference Activity();

    [Route("overview", typeof(DashboardViewModel), Id = "dogfood.dashboard.overview", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardOverview();

    [Route("health", typeof(HealthViewModel), Id = "dogfood.dashboard.health", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardHealth();

    [Route("alerts/{severity=all}", typeof(AlertsViewModel), Id = "dogfood.dashboard.alerts", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardAlerts();

    [Route("realtime/{sample:double:min(0)}", typeof(RealtimeMonitorViewModel), Id = "dogfood.dashboard.realtime", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardRealtime();

    [Route("kpi/{ratio:float:range(0,100)}", typeof(KpiBoardViewModel), Id = "dogfood.dashboard.kpi", Parent = nameof(DashboardGroup))]
    public static partial RouteReference DashboardKpi();

    [Route("diagnostics/{at:datetime}", typeof(DiagnosticsSummaryViewModel), Id = "dogfood.dashboard.diagnostics", Parent = nameof(DashboardGroup), Outlet = "inspector")]
    public static partial RouteReference DashboardDiagnostics();

    [Route("products", typeof(ProductListViewModel), Id = "dogfood.catalog.products", Parent = nameof(CatalogGroup))]
    public static partial RouteReference Products();

    [Route("products/{sku:regex(^SKU-[0-9]{4}$)}", typeof(ProductDetailsViewModel), Id = "dogfood.catalog.product-details", Parent = nameof(CatalogGroup), Outlet = "details")]
    public static partial RouteReference<ProductRouteParameters> ProductDetails();

    [Route("products/{sku}/edit", typeof(ProductEditorViewModel), Id = "dogfood.catalog.product-edit", Parent = nameof(CatalogGroup))]
    public static partial RouteReference ProductEdit();

    [Route("categories", typeof(CategoryTreeViewModel), Id = "dogfood.catalog.categories", Parent = nameof(CatalogGroup))]
    public static partial RouteReference Categories();

    [Route("categories/{id:int:min(1)}", typeof(CategoryTreeViewModel), Id = "dogfood.catalog.category-details", Parent = nameof(CatalogGroup))]
    public static partial RouteReference CategoryDetails();

    [Route("prices/{amount:decimal:max(999999)}", typeof(PricingBoardViewModel), Id = "dogfood.catalog.prices", Parent = nameof(CatalogGroup))]
    public static partial RouteReference Prices();

    [Route("currency/{code:alpha:length(3)}", typeof(CurrencyEditorViewModel), Id = "dogfood.catalog.currency", Parent = nameof(CatalogGroup))]
    public static partial RouteReference Currency();

    [Route("promotions", typeof(PromotionListViewModel), Id = "dogfood.catalog.promotions", Parent = nameof(CatalogGroup))]
    public static partial RouteReference Promotions();

    [Route("promotions/{id:long:min(1)}/edit", typeof(PromotionEditorViewModel), Id = "dogfood.catalog.promotion-edit", Parent = nameof(CatalogGroup))]
    public static partial RouteReference PromotionEdit();

    [Route("stock", typeof(InventoryListViewModel), Id = "dogfood.inventory.stock", Parent = nameof(InventoryGroup))]
    public static partial RouteReference Stock();

    [Route("stock/{sku}", typeof(StockDetailsViewModel), Id = "dogfood.inventory.stock-details", Parent = nameof(InventoryGroup), Outlet = "details")]
    public static partial RouteReference StockDetails();

    [Route("warehouses", typeof(WarehouseListViewModel), Id = "dogfood.inventory.warehouses", Parent = nameof(InventoryGroup))]
    public static partial RouteReference Warehouses();

    [Route("warehouses/{id:guid}", typeof(WarehouseDetailsViewModel), Id = "dogfood.inventory.warehouse-details", Parent = nameof(InventoryGroup))]
    public static partial RouteReference<WarehouseRouteParameters> WarehouseDetails();

    [Route("bins/{code:length(8)}", typeof(BinAllocationViewModel), Id = "dogfood.inventory.bins", Parent = nameof(InventoryGroup))]
    public static partial RouteReference Bins();

    [Route("transfers", typeof(TransferEditorViewModel), Id = "dogfood.inventory.transfers", Parent = nameof(InventoryGroup))]
    public static partial RouteReference Transfers();

    [Route("transfers/{quantity:int:range(1,500)}", typeof(TransferEditorViewModel), Id = "dogfood.inventory.transfer-edit", Parent = nameof(InventoryGroup))]
    public static partial RouteReference TransferEdit();

    [Route("purchase-orders/{id:long:min(1)}", typeof(PurchaseOrderListViewModel), Id = "dogfood.inventory.purchase-orders", Parent = nameof(InventoryGroup))]
    public static partial RouteReference PurchaseOrders();

    [Route("receiving/{dock:maxlength(12)}", typeof(ReceivingViewModel), Id = "dogfood.inventory.receiving", Parent = nameof(InventoryGroup))]
    public static partial RouteReference Receiving();

    [Route("list", typeof(OrderListViewModel), Id = "dogfood.orders.list", Parent = nameof(OrdersGroup))]
    public static partial RouteReference Orders();

    [Route("{id:guid}", typeof(OrderDetailsViewModel), Id = "dogfood.orders.details", Parent = nameof(OrdersGroup))]
    public static partial RouteReference<OrderRouteParameters> OrderDetails();

    [Route("{id:guid}/edit", typeof(OrderEditorViewModel), Id = "dogfood.orders.edit", Parent = nameof(OrdersGroup))]
    public static partial RouteReference OrderEdit();

    [Route("{id:guid}/timeline", typeof(OrderTimelineViewModel), Id = "dogfood.orders.timeline", Parent = nameof(OrdersGroup), Outlet = "timeline")]
    public static partial RouteReference OrderTimeline();

    [Route("search/{term:minlength(2):maxlength(40)}", typeof(OrderListViewModel), Id = "dogfood.orders.search", Parent = nameof(OrdersGroup))]
    public static partial RouteReference OrderSearch();

    [Route("invoices", typeof(InvoiceDetailsViewModel), Id = "dogfood.billing.invoices", Parent = nameof(BillingGroup))]
    public static partial RouteReference Invoices();

    [Route("invoices/{id:long:min(1)}", typeof(InvoiceDetailsViewModel), Id = "dogfood.billing.invoice-details", Parent = nameof(BillingGroup))]
    public static partial RouteReference InvoiceDetails();

    [Route("tax/{rate:decimal:range(0,1)}", typeof(TaxPreviewViewModel), Id = "dogfood.billing.tax", Parent = nameof(BillingGroup))]
    public static partial RouteReference Tax();

    [Route("payment/{id:guid}", typeof(PaymentWizardViewModel), Id = "dogfood.billing.payment", Parent = nameof(BillingGroup))]
    public static partial RouteReference Payment();

    [Route("reconciliation", typeof(PaymentReconciliationViewModel), Id = "dogfood.billing.reconciliation", Parent = nameof(BillingGroup))]
    public static partial RouteReference Reconciliation();

    [Route("fulfillment/{priority:int:min(1):max(5)}", typeof(FulfillmentPlanViewModel), Id = "dogfood.orders.fulfillment", Parent = nameof(OrdersGroup))]
    public static partial RouteReference Fulfillment();

    [Route("list", typeof(ShipmentListViewModel), Id = "dogfood.shipping.list", Parent = nameof(ShippingGroup))]
    public static partial RouteReference Shipments();

    [Route("shipment/{id:guid}", typeof(ShipmentDetailsViewModel), Id = "dogfood.shipping.details", Parent = nameof(ShippingGroup))]
    public static partial RouteReference ShipmentDetails();

    [Route("tracking/{*path:minlength(3)}", typeof(ShipmentDetailsViewModel), Id = "dogfood.shipping.tracking", Parent = nameof(ShippingGroup))]
    public static partial RouteReference Tracking();

    [Route("quotes/{weight:double:min(0)}", typeof(CarrierQuoteViewModel), Id = "dogfood.shipping.quotes", Parent = nameof(ShippingGroup))]
    public static partial RouteReference CarrierQuotes();

    [Route("returns", typeof(ReturnListViewModel), Id = "dogfood.shipping.returns", Parent = nameof(ShippingGroup))]
    public static partial RouteReference Returns();

    [Route("returns/{step:int:range(1,4)}", typeof(ReturnWizardViewModel), Id = "dogfood.shipping.return-wizard", Parent = nameof(ShippingGroup))]
    public static partial RouteReference ReturnWizard();

    [Route("refund/{id:guid}", typeof(RefundReviewViewModel), Id = "dogfood.shipping.refund", Parent = nameof(ShippingGroup))]
    public static partial RouteReference Refund();

    [Route("customers", typeof(CustomerListViewModel), Id = "dogfood.support.customers", Parent = nameof(SupportGroup))]
    public static partial RouteReference Customers();

    [Route("customers/{id:guid}", typeof(CustomerDetailsViewModel), Id = "dogfood.support.customer-details", Parent = nameof(SupportGroup), Outlet = "customer")]
    public static partial RouteReference CustomerDetails();

    [Route("customers/{id:guid}/timeline", typeof(CustomerTimelineViewModel), Id = "dogfood.support.customer-timeline", Parent = nameof(SupportGroup), Outlet = "timeline")]
    public static partial RouteReference CustomerTimeline();

    [Route("recommendations/{score:float:range(0,1)}", typeof(RecommendationPanelViewModel), Id = "dogfood.support.recommendations", Parent = nameof(SupportGroup))]
    public static partial RouteReference Recommendations();

    [Route("conversation/{ticketId:long:min(1)}", typeof(SupportConversationViewModel), Id = "dogfood.support.conversation", Parent = nameof(SupportGroup), Outlet = "conversation")]
    public static partial RouteReference Conversation();

    [Route("center", typeof(ReportCenterViewModel), Id = "dogfood.reports.center", Parent = nameof(ReportsGroup))]
    public static partial RouteReference Reports();

    [Route("designer/{page:int:min(1)}", typeof(ReportDesignerViewModel), Id = "dogfood.reports.designer", Parent = nameof(ReportsGroup))]
    public static partial RouteReference ReportDesigner();

    [Route("analytics/{scale:double:min(0)}", typeof(AnalyticsViewModel), Id = "dogfood.reports.analytics", Parent = nameof(ReportsGroup))]
    public static partial RouteReference Analytics();

    [Route("audit/{*path}", typeof(AuditExplorerViewModel), Id = "dogfood.reports.audit", Parent = nameof(ReportsGroup))]
    public static partial RouteReference Audit();

    [Route("users", typeof(UserAdminViewModel), Id = "dogfood.admin.users", Parent = nameof(AdministrationLayout))]
    public static partial RouteReference Users();

    [Route("permissions", typeof(PermissionAdminViewModel), Id = "dogfood.admin.permissions", Parent = nameof(AdministrationLayout))]
    public static partial RouteReference Permissions();

    [Route("failures/{code:int}", typeof(FailureConsoleViewModel), Id = "dogfood.admin.failures", Parent = nameof(DiagnosticsLayout), Outlet = "probe")]
    public static partial RouteReference Failures();

    // 6 redirects
    [RedirectRoute("home", Id = "dogfood.redirect.home", Parent = nameof(Shell), Target = nameof(DashboardOverview))]
    public static partial RouteReference LegacyHome();

    [RedirectRoute("old-products", Id = "dogfood.redirect.products", Parent = nameof(Shell), Target = nameof(Products))]
    public static partial RouteReference LegacyProducts();

    [RedirectRoute("old-stock", Id = "dogfood.redirect.stock", Parent = nameof(Shell), Target = nameof(Stock))]
    public static partial RouteReference LegacyStock();

    [RedirectRoute("old-orders", Id = "dogfood.redirect.orders", Parent = nameof(Shell), Target = nameof(Orders))]
    public static partial RouteReference LegacyOrders();

    [RedirectRoute("old-shipping", Id = "dogfood.redirect.shipping", Parent = nameof(Shell), Target = nameof(Shipments))]
    public static partial RouteReference LegacyShipping();

    [RedirectRoute("old-reports", Id = "dogfood.redirect.reports", Parent = nameof(Shell), Target = nameof(Reports))]
    public static partial RouteReference LegacyReports();

    // 6 contribution points
    [RouteExtensionPoint("dogfood.extensions.dashboard", Id = "dogfood.extension.dashboard", Parent = nameof(DashboardGroup))]
    public static partial RouteExtensionPoint DashboardExtensions();

    [RouteExtensionPoint("dogfood.extensions.catalog", Id = "dogfood.extension.catalog", Parent = nameof(CatalogGroup))]
    public static partial RouteExtensionPoint CatalogExtensions();

    [RouteExtensionPoint("dogfood.extensions.inventory", Id = "dogfood.extension.inventory", Parent = nameof(InventoryGroup))]
    public static partial RouteExtensionPoint InventoryExtensions();

    [RouteExtensionPoint("dogfood.extensions.orders", Id = "dogfood.extension.orders", Parent = nameof(OrdersGroup))]
    public static partial RouteExtensionPoint OrderExtensions();

    [RouteExtensionPoint("dogfood.extensions.support", Id = "dogfood.extension.support", Parent = nameof(SupportGroup))]
    public static partial RouteExtensionPoint SupportExtensions();

    [RouteExtensionPoint("dogfood.extensions.reports", Id = "dogfood.extension.reports", Parent = nameof(ReportsGroup))]
    public static partial RouteExtensionPoint ReportExtensions();
}
