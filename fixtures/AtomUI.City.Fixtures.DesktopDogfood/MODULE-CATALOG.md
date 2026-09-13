# Module 与 Service 目录

## 1. 计数规则

- 下表定义 48 个应用模块和恰好 110 个业务服务；City 自身的 `EventBusModule`、`RoutingModule`、`DataModule`、`PresentationModule` 不计入 48。
- `ShellPresentationModule` 是唯一默认应用根；显式 `UseModule<T>` 入口另设 automation profile 验证。
- 每个模块必须有真实 owner contribution，且至少一个服务被其他模块调用。只有生命周期 ledger、没有业务参与者的空模块不计数。
- 依赖闭包由 Generator 产生，启动时断言实际构造 48 个应用模块和 4 个 City 模块。未选中的 canary module 必须保持零构造。
- 下表依赖均为 required dependency。另设 4 个 optional dependency 组合验证 available/missing 分支，但不改变主图计数。

## 2. 模块与服务

| # | Module | Required dependencies | Owned services |
| ---: | --- | --- | --- |
| 1 | FoundationModule | - | SystemClock; CorrelationContextAccessor; RuntimeIdentityService |
| 2 | DiagnosticsModule | Foundation | DiagnosticProjectionService; FailureJournal |
| 3 | SettingsModule | Foundation, Diagnostics | UserSettingsService; FeatureConfigurationService |
| 4 | PersistenceModule | Foundation, Settings, Diagnostics | AtomicFileStore; WorkspaceSnapshotStore |
| 5 | SchedulingModule | Foundation, Diagnostics | WorkScheduler; DebounceCoordinator |
| 6 | TelemetryModule | Foundation, Diagnostics, Scheduling | TelemetryRecorder; PerformanceSampler |
| 7 | AuditInfrastructureModule | Persistence, Telemetry | AuditSink; AuditRetentionService |
| 8 | AutomationModule | Diagnostics, Scheduling, Telemetry | ScenarioDriver; FaultGateRegistry; InvariantLedger |
| 9 | IdentityModule | Foundation, Persistence, AuditInfrastructure | SignInWorkflow; PrincipalProjectionService; SessionExpiryMonitor |
| 10 | AuthorizationModule | Identity, Diagnostics | PermissionCatalogService; PolicyCatalogService; AuthorizationProjectionService |
| 11 | AccountWorkspaceModule | Identity, Authorization, Settings | AccountContextService; TenantContextService; AccountSwitchCoordinator |
| 12 | DataGatewayModule | Identity, Authorization, Telemetry | ApiRequestService; RemoteCommandService; ClientCatalogService; DataErrorTranslator |
| 13 | RealtimeModule | DataGateway, Scheduling, Telemetry | RealtimeSessionService; ServerEventRouter; ReconnectCoordinator |
| 14 | CacheModule | DataGateway, Persistence, Telemetry | QueryCacheService; CacheInvalidationService; OfflineReadService |
| 15 | SyncModule | DataGateway, Realtime, Cache, Scheduling | SyncOrchestrator; MutationReplayService; SequenceReconciler |
| 16 | LocalizationCompositionModule | Settings, Persistence, Diagnostics | ApplicationTextService; CultureWorkflow |
| 17 | CatalogModule | DataGateway, Cache, LocalizationComposition | ProductCatalogService; CategoryService |
| 18 | PricingModule | Catalog, Settings, Realtime | PricingService; CurrencyService |
| 19 | PromotionsModule | Pricing, AccountWorkspace, Scheduling | PromotionService; DiscountRuleService |
| 20 | InventoryModule | Catalog, Realtime, Cache | InventoryService; StockProjectionService |
| 21 | WarehouseModule | Inventory, Settings | WarehouseService; BinAllocationService |
| 22 | ProcurementModule | Inventory, Warehouse, DataGateway | PurchaseOrderService; ReplenishmentService |
| 23 | CustomersModule | DataGateway, Cache, Identity | CustomerService; CustomerTimelineService |
| 24 | OrdersModule | Catalog, Inventory, Customers, Pricing | OrderService; OrderDraftService |
| 25 | TaxModule | Orders, Settings, AccountWorkspace | TaxService; TaxRegionService |
| 26 | BillingModule | Orders, Tax, AuditInfrastructure | InvoiceService; BillingLedgerService |
| 27 | FraudModule | Orders, Authorization, DataGateway, Telemetry | FraudAssessmentService; FraudRuleService |
| 28 | PaymentsModule | Billing, Fraud, DataGateway, AuditInfrastructure | PaymentService; PaymentReconciliationService |
| 29 | FulfillmentModule | Orders, Inventory, Warehouse, Scheduling | FulfillmentService; PickWaveService |
| 30 | ShippingModule | Fulfillment, Realtime, DataGateway | ShipmentService; CarrierQuoteService |
| 31 | ReturnsModule | Orders, Inventory, Payments, Shipping | ReturnService; RefundService |
| 32 | SearchModule | Catalog, Cache, Telemetry | SearchService; SearchIndexService |
| 33 | RecommendationsModule | Search, Customers, Pricing | RecommendationService; AffinityProjectionService |
| 34 | NotificationsModule | Realtime, LocalizationComposition, Customers | NotificationService; NotificationPreferenceService |
| 35 | SupportModule | Customers, Orders, Notifications, Authorization | SupportTicketService; ConversationService |
| 36 | WorkflowModule | Scheduling, AuditInfrastructure, Notifications | WorkflowEngine; WorkflowRecoveryService |
| 37 | ReportingModule | Orders, Billing, Returns, DataGateway | ReportService; ExportService |
| 38 | AnalyticsModule | Reporting, Inventory, Realtime, Telemetry | AnalyticsService; DashboardProjectionService |
| 39 | AuditDomainModule | AuditInfrastructure, Identity, Authorization | AuditQueryService; ComplianceExportService |
| 40 | OperationsModule | Workflow, Fulfillment, Payments, Shipping, Support | OperationsCoordinator; IncidentService |
| 41 | FeatureFlagsModule | Settings, Authorization, Telemetry | FeatureFlagService; ExperimentAssignmentService |
| 42 | AdministrationModule | Authorization, FeatureFlags, AuditDomain, Operations | AdministrationService; MaintenanceService |
| 43 | DashboardPresentationModule | Analytics, Operations, Realtime, LocalizationComposition | DashboardNavigationAdapter; DashboardViewModelFactory; DashboardInteractionCoordinator |
| 44 | CommercePresentationModule | Catalog, Pricing, Promotions, Orders, Billing | CommerceNavigationAdapter; CommerceViewModelFactory; CommerceInteractionCoordinator |
| 45 | FulfillmentPresentationModule | Inventory, Warehouse, Procurement, Fulfillment, Shipping, Returns | FulfillmentNavigationAdapter; FulfillmentViewModelFactory; FulfillmentInteractionCoordinator |
| 46 | CustomerCarePresentationModule | Customers, Recommendations, Notifications, Support | CustomerCareNavigationAdapter; CustomerCareViewModelFactory; CustomerCareInteractionCoordinator |
| 47 | AdministrationPresentationModule | Administration, AuditDomain, FeatureFlags | AdministrationNavigationAdapter; AdministrationInteractionCoordinator |
| 48 | ShellPresentationModule | DashboardPresentation, CommercePresentation, FulfillmentPresentation, CustomerCarePresentation, AdministrationPresentation, Automation | ShellNavigationAdapter; WindowWorkspaceCoordinator |

`ShellPresentationModule` 还通过 generated dependency 声明依赖 City 的四个 runtime module。State、MVVM、Localization 和 Security 没有独立 `ModuleBase` 入口时由应用模块在配置阶段使用其正式 DI extension；不得人为包装出第二套生命周期。

## 3. DI 覆盖

110 个服务必须按以下方式分布并由自动化精确断言：

| 注册维度 | 最低数量 | 目的 |
| --- | ---: | --- |
| Singleton | 32 | registry、projection、connection coordinator、diagnostics |
| Scoped | 40 | Window/Route/operation 业务上下文和 ViewModel 依赖 |
| Transient | 30 | command handler、formatter、resolver、短事务服务 |
| Keyed | 8 | transport strategy、exporter、carrier、payment method |
| Generator attribute/marker | 80 | Service/ScopedService/ExposeServices/owner catalog |
| Module manual ConfigureServices | 20 | options、factory delegate、keyed 或外部 Avalonia 类型 |
| Builder ConfigureServices override | 10 | 验证模块注册后用户 override 的最终优先级 |

同一个实现暴露多个 contract 时必须验证共享实例语义；disposable 多 contract 不允许产生重复释放。至少 12 个服务实现 `IAsyncDisposable`，8 个实现 `IDisposable`，并在 Host 正常停止、启动回滚和 stop failure 三条路径验证完整清理。

## 4. 模块生命周期负载

每个模块的钩子不能只写 ledger：

- `PreConfigureServices`：读取自己的 immutable configuration section，注册 owner metadata。
- `ConfigureServices`：注册本模块服务、State definition、Event contribution 或 route behavior。
- `PostConfigureServices`：验证跨模块 contract 完整性，不解析 Root runtime service。
- `OnApplicationStartingAsync`：启动可取消的模块资源；至少 12 个模块包含真实异步 continuation。
- `OnApplicationStartedAsync`：发布 ready 事件或建立 projection。
- `OnApplicationStoppingAsync`：停止接收新业务工作，等待 owner operation。
- `OnApplicationStoppedAsync`：释放剩余 lease 并写最终计数。

自动化检查每个模块恰好构造一次、按拓扑启动、按逆拓扑停止。故障 profile 在不同模块和不同阶段注入异常，验证已经完成的节点被补偿，未进入节点不执行停止 hook，全部可释放资源仍被尝试。

## 5. 服务调用图

禁止 110 个服务成为彼此孤立的“打卡对象”。至少形成以下六条跨域调用链：

1. `OrderDraftService -> PricingService -> PromotionService -> TaxService -> OrderService`。
2. `OrderService -> InventoryService -> FraudAssessmentService -> PaymentService -> FulfillmentService -> ShipmentService`。
3. `CustomerService -> AffinityProjectionService -> RecommendationService -> NotificationService`。
4. `SupportTicketService -> CustomerTimelineService -> OrderService -> ReturnService -> RefundService`。
5. `SyncOrchestrator -> MutationReplayService -> SequenceReconciler -> CacheInvalidationService`。
6. `DashboardProjectionService -> AnalyticsService -> ReportService -> AuditQueryService -> DiagnosticProjectionService`。

验收报告记录每个服务的 resolve count、call count、success/failure/cancel count 和 dispose count。任何业务服务 call count 为零都使整个 Dogfood 失败。

