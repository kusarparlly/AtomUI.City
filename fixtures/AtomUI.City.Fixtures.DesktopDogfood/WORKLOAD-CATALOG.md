# Framework 工作负载目录

> 实现状态：当前行为基线已覆盖 72 个 Event contract 的成功投递、72/20/16/20 State 目录、96 条 Route 定义及 76 条实际导航、64 个 ViewModel、96 个 Command、110 个 Service 的 8 条工作流，以及 HTTP/gRPC/SignalR、Localization 和 Security 多账号链路。下文列出的 12 种 Event channel profile、动态 contribution、完整故障矩阵和全部 API 分支仍是最终目标，不能因目录类型已存在而视为全部执行。

## 1. EventBus

### 1.1 72 个事件合同

所有 contract 使用 `[EventContract]` 声明稳定 id、owner module 和 schema version。每个 contract 至少有两个不同 owner 的 handler；至少一个 handler 形成跨模块写 State 或调用服务，但 handler 不直接修改 Avalonia control。

| 组 | 数量 | Contract type |
| --- | ---: | --- |
| Host/System | 8 | ApplicationReady; ApplicationStopping; ModuleHealthChanged; ConfigurationReloadRequested; SettingsChanged; DiagnosticRaised; FeatureFlagChanged; AutomationCheckpointReached |
| Identity/Security | 8 | SignInRequested; UserSignedIn; SignInFailed; UserSignedOut; TenantSwitchRequested; TenantSwitched; PermissionsChanged; AccessTokenExpiring |
| Data/Realtime | 10 | NetworkReachabilityChanged; DataRequestFailed; CacheInvalidated; SyncStarted; SyncCompleted; SyncFailed; RealtimeConnected; RealtimeDisconnected; RemoteSequenceGapDetected; LargeTransferProgressed |
| Commerce | 20 | ProductCreated; ProductUpdated; CatalogRebuilt; PriceChanged; CurrencyChanged; PromotionActivated; InventoryAdjusted; InventoryLow; WarehouseCapacityChanged; PurchaseOrderCreated; CustomerCreated; CustomerUpdated; OrderDraftSaved; OrderSubmitted; OrderConfirmed; TaxCalculated; InvoiceIssued; BillingSettled; CartAbandoned; SalesTargetChanged |
| Workflow | 14 | FraudAssessmentCompleted; FraudFlagged; PaymentAuthorized; PaymentCaptured; PaymentFailed; FulfillmentPlanned; PickWaveCreated; ShipmentQuoted; ShipmentDispatched; ShipmentProgressed; ReturnRequested; ReturnApproved; RefundCompleted; WorkflowCompensated |
| UI/Operations | 12 | SearchExecuted; RecommendationProduced; NotificationRaised; SupportTicketOpened; SupportMessageReceived; ReportGenerated; AnalyticsRefreshed; AuditAppended; NavigationCommitted; OutletReconciled; WindowWorkspaceOpened; UserActionRejected |

事件 payload 只使用生成器允许的 immutable 对象图。`UserSignedIn`、Data error 和 diagnostics 事件严禁携带 token、完整 claims、exception object、View、ViewModel、ServiceProvider 或 plugin-private 类型。

### 1.2 12 个 channel profile

| Channel | Mode | Backpressure | Capacity | 业务用途 |
| --- | --- | --- | ---: | --- |
| `critical` | Serialized | Wait | 64 | 身份、支付、Host 终止 |
| `orders` | Partitioned | Wait | 128 | 以 OrderId 分区保持单订单顺序 |
| `inventory` | Partitioned | CoalesceLatest | 128 | 以 SKU 合并库存刷新 |
| `realtime` | Partitioned | Reject | 256 | 以 tenant/account 分区的远端序列 |
| `telemetry` | Concurrent | DropOldest | 512 | 高频非关键遥测 |
| `notifications` | Concurrent | DropNewest | 128 | 用户通知风暴 |
| `audit` | Serialized | Wait | 256 | 审计顺序不可丢失 |
| `search` | Concurrent | DropOldest | 64 | 搜索建议与结果 |
| `ui-feedback` | Serialized | CoalesceLatest | 32 | 同 identity 的 UI 投影刷新 |
| `background` | Concurrent | Wait | 128 | 报表与导出完成事件 |
| `workflow` | Partitioned | Wait | 128 | 以 WorkflowId 串行 |
| `chaos-tiny` | Serialized | Reject | 2 | 确定性制造队列满载 |

覆盖要求：

- 同时使用 typed `PublishAsync`、`PostAsync`、默认/命名 `EventChannel<T>`。
- 使用 delegate、同步 `Action` extension 和 `IEventHandler<T>` 三类动态订阅，以及 generated static handler。
- 覆盖 `Current`、`UiThread`、`Background`、`Serialized` dispatch policy，`Post`/`InlineIfAllowed` mode，以及 `ContinueAndReport`、`StopPublication`、`FailPublisher`、`DisableSubscription` error policy、handler timeout 和连续失败禁用。
- 验证 owner scope stop、subscription Stop/Dispose/DisposeAsync、发布快照、递归发布和 serialized self-wait 防护。
- 读取 bus/channel metrics、payload projector、diagnostics sampling；敏感 payload 投影必须为 opt-in 安全字符串。
- 合成 contribution 验证 shared/private plane capability、quota、quiesce、drain、timeout 和重复 plugin id；真实 ALC 回收仍归 PluginSystem。

## 2. State

### 2.1 72 个 Application State

| 组 | 数量 | State key suffix |
| --- | ---: | --- |
| System | 10 | AppRunMode; HostHealth; NetworkReachability; DiagnosticsLevel; FeatureRevision; SettingsRevision; TelemetryRevision; ClockSkew; AutomationProfile; LastCheckpoint |
| Identity | 10 | UserCurrentAccount; UserDisplayName; TenantCurrent; TenantRevision; PermissionRevision; SessionRevision; TokenExpiryHint; AccountSwitchStatus; OfflineIdentityMode; PrincipalFingerprint |
| Data | 12 | HttpHealth; GrpcHealth; SignalRHealth; RealtimeConnectionState; ReconnectAttempt; RequestInFlight; RequestFailureCount; CacheHitRate; CacheRevision; SyncRevision; SyncBacklog; LastServerSequence |
| Commerce | 24 | CatalogRevision; ProductCount; ProductSelection; PricingCurrency; PricingRevision; PromotionRevision; InventoryRevision; InventoryLowCount; WarehouseCurrent; WarehouseCapacity; ProcurementPending; CustomerSelection; CustomerRevision; CartDraftId; OrderSelection; OrderCount; OrderPending; OrderFailed; TaxRegion; TaxRevision; BillingBalance; InvoicePending; RefundPending; SalesDayTotal |
| Operations | 16 | FraudThreshold; FraudFlaggedCount; PaymentPending; PaymentExposure; FulfillmentPending; PickWaveCurrent; ShipmentInTransit; ShipmentDelayed; ReturnsOpen; SearchQuery; SearchRevision; RecommendationRevision; NotificationsUnread; SupportOpen; WorkflowRunning; AuditRevision |

`StateDefinition<T>` 分布必须覆盖 `ReadOnly`、`HostWrite`、`OwnerWrite`、`AuthorizedWrite`、`PluginIsolated` 五种 access policy，`Application`、`Module`、`Route`、`Activation`、`Plugin` 五种 lifetime，以及 `Transient`/`Persisted` snapshot policy、schema version 和 write capability。State 值不保存 token、View、ViewModel、scope、service 或 exception。

### 2.2 20 个 Computed State

```text
ApplicationReadyToOperate, EffectiveOnlineMode, EffectiveCultureLabel,
CurrentIdentityLabel, CanMutateCommerce, NavigationTitle,
CatalogHealth, InventoryPressure, PricingHealth, OrderThroughput,
RevenueExposure, PaymentRisk, FulfillmentPressure, ShippingHealth,
ReturnRate, CustomerCareLoad, SearchYield, NotificationPressure,
OperationsScore, OverallHealth
```

依赖图必须包含：4 层长链、两个菱形、跨 collection revision 的计算、首次计算失败后恢复、并发 invalidation、dispose/read 合同，以及隔离进程内的直接和动态循环防护。计算函数不得在 UI 控件上工作。

### 2.3 16 个 Collection State

```text
Products, Categories, Prices, Promotions,
InventoryItems, Warehouses, PurchaseOrders, Customers,
Orders, Invoices, Payments, Shipments,
Returns, SupportTickets, Notifications, NavigationJournal
```

每个集合执行 add/update/remove/clear/range、snapshot/restore、重复 key、missing key、版本、无变化 restore、并发读写和 dispose 后查询。Orders、InventoryItems 和 Notifications 还要承受 10,000 次混合 mutation。

### 2.4 20 个 Route/Window scoped State

```text
WindowFocus, WindowBusy, WindowBanner, WindowSelection,
RouteParameters, RouteResolvedData, RouteDraftDirty, RouteValidation,
RouteCommandState, RouteError, OrderEditorDraft, ProductEditorDraft,
CustomerEditorDraft, PaymentWizardStep, ReturnWizardStep, SearchFilters,
ReportFilters, SupportConversationDraft, InspectorSelection, ActivityFilter
```

这些对象必须通过 `IStateFactory` 在当前 `IStateScopeAccessor` 中创建，绑定 Window/Route/Activation 生命周期。关闭页面或 Window 后，旧订阅不能再接收通知，旧 writer 必须失败或释放。

### 2.5 Dispatch 和 snapshot

- `Immediate`：后台 projection 和无 UI 依赖 handler。
- `Queued`：订单、审计和实时序列，验证严格 FIFO。
- `Background`：报表与 analytics，验证不阻塞 mutation。
- `Dispatcher`：ViewModel 可见投影，通过真实 Avalonia dispatcher 更新绑定属性。
- Application snapshot 保存 30 个 persisted state；恢复时包含 compatible、higher schema、policy reject、partial failure 和旧 snapshot migration-not-supported 路径。

## 3. Routing

### 3.1 96 条静态 route

| 区域 | 数量 | 代表 route id / template |
| --- | ---: | --- |
| Shell/Workspace | 8 | `shell` layout; index; workspace/{id:guid}; popout; legacy-home redirect; extension point |
| Dashboard | 8 | overview; health; activity; realtime; tenant/{tenantId}; alerts; KPI; diagnostics summary |
| Catalog/Pricing | 16 | products; products/{sku:regex(...)}; categories/{id:int:min(1)}; prices; currency/{code:alpha:length(3)}; promotions; catch-all import path |
| Inventory/Procurement | 14 | stock; warehouse/{id:guid}; bins/{code}; low; transfers; purchase-orders/{id:long:min(1)}; replenish; receiving |
| Orders/Billing/Payments | 18 | orders; orders/{id:guid}; order search with query/fragment; invoices; tax; payment/{id:guid}; reconciliation; guarded refund |
| Fulfillment/Shipping/Returns | 14 | fulfillment/{id}; pick-wave/{id}; shipment/{id}; tracking/{*path}; carrier quote; returns/{id}; refund wizard |
| Customers/Support | 10 | customers/{id:guid}; timeline; recommendations; support/{ticketId}; conversation; notifications |
| Admin/Diagnostics | 8 | users; permissions; policies; settings; feature flags; audit; failures; route explorer |

实际 route map 必须为 public static partial generated map，manifest 数量精确等于 96。至少包含：

- 6 Layout、8 Group、10 Index、60 Route、6 Redirect、6 ExtensionPoint。
- required/optional/default/catch-all parameter，以及全部 17 种内置 constraint。
- typed parameter 的 property、field、base member、`[Query]`、`[Fragment]` 和 invariant conversion。
- `primary`、`navigation`、`inspector`、`activity`、`details`、`timeline`、`customer`、`conversation`、`probe` 九个 outlet 名称。
- 两组同形模板，通过 `IRouteMatchPolicy` 按 feature flag、tenant 和权限裁决。
- 24 个 enter guard、12 个 leave guard、8 个双向 guard、18 个 resolver、12 个 middleware。

### 3.2 导航事务

自动化必须使用全部入口：typed/untyped `RouteReference`、path、URI deep link、Back、Forward。Options 覆盖：

- `Push`、`Replace`、`Reset`；
- `Record`、`Skip`、`ReplaceCurrent`；
- `CancelPrevious`、`Queue`、`RejectIfBusy`；
- `ForceReload`、`AllowRedirect=false`、timeout、journal capacity 和 named outlet。

管线断言顺序为 match policy、middleware enter、leave guard leaf-to-root、enter guard root-to-leaf、resolver hierarchy、middleware exit、Router commit、adapter UI commit。Router 失败不改变 snapshot/journal；Router 成功但 Presentation 失败进入可解释的不一致并由 reconcile 修复。

### 3.3 动态 contribution

`seasonal-operations` 在 6 个 ExtensionPoint 下附加 6 条 route，提供独立 service resolver 和 contribution lease。测试 graph version、重复 attach、错误 parent、revoke、在途导航、stale journal、同 contribution id 重用，以及撤销后 View/resource/localization/data 协同释放。

## 4. MVVM 与 View

### 4.1 64 个 ViewModel

| 组 | 数量 | ViewModel |
| --- | ---: | --- |
| Shell/Workspace | 8 | Shell; Navigation; AccountSwitcher; TenantSwitcher; ActivityCenter; Inspector; GlobalSearch; Workspace |
| Dashboard | 6 | Dashboard; KpiBoard; Health; Alerts; RealtimeMonitor; DiagnosticsSummary |
| Catalog | 8 | ProductList; ProductDetails; ProductEditor; CategoryTree; PricingBoard; CurrencyEditor; PromotionList; PromotionEditor |
| Inventory | 8 | InventoryList; StockDetails; WarehouseList; WarehouseDetails; BinAllocation; TransferEditor; PurchaseOrderList; Receiving |
| Orders | 10 | OrderList; OrderDetails; OrderEditor; OrderTimeline; InvoiceDetails; TaxPreview; PaymentWizard; PaymentReconciliation; FulfillmentPlan; PickWave |
| Shipping/Returns | 6 | ShipmentList; ShipmentDetails; CarrierQuote; ReturnList; ReturnWizard; RefundReview |
| Customers/Support | 8 | CustomerList; CustomerDetails; CustomerTimeline; RecommendationPanel; NotificationCenter; SupportQueue; SupportConversation; SupportComposer |
| Admin/Reports | 10 | ReportCenter; ReportDesigner; Analytics; AuditExplorer; UserAdmin; PermissionAdmin; PolicyAdmin; Settings; FeatureFlags; FailureConsole |

64 个 ViewModel 共提供不少于 96 个 command。所有 ViewModel 都由 `ViewModelBase` 或明确的 MVVM contract 构建，覆盖完整 activation/deactivation 状态；至少 12 个实现 `ICanDeactivate`，4 个实现 `IConfirmDeactivate`，但同一 Window 当前 Entry 最多只有一个 confirmation owner。

### 4.2 Command、Interaction、Validation

- Command：同步/异步、显式 state、ActivationScope、CanExecute、CommandGroup、成功、失败、取消、并发拒绝、Dispose。
- Interaction：Confirm destructive action、select account、choose warehouse、edit note、pick file、save conflict、reauthentication、show error、toast intent、open external link 等 18 种 request/result；Presentation 注册 Activation/Route、Window、Presentation 三层 handler。
- Modal：同 Window FIFO、不同 Window 并行、容量满载、registration revoke 和 Window close cancel。
- Validation：14 个编辑/向导页面显式创建规则和 `ValidationScope`；Presentation 只将 snapshot 应用到应用自定义 target。另有页面完全不启用 City validation，证明其可选。

### 4.3 View descriptor 和 ownership

- 64 个默认 ViewModel 至少有一个 View。
- 12 个 ViewModel 额外提供 compact/detailed/diagnostic `ViewKey`。
- 6 个 descriptor 由应用显式注册，其他由 generator 注册。
- 4 个 View 做 owner override/revoke，验证底层 descriptor 恢复。
- `EntryOwned`、`ServiceScopeOwned`、`Borrowed` 三种 ViewModel lease 都进入真实 Outlet。
- `BoundViewHandle.FromExisting` 与 factory-created handle 都被使用并验证 UI-thread dispose。

### 4.4 Headless 用户控件工作负载

主 Window 提供一个稳定的操作面板，由 30 个唯一 `AutomationId` 标识导航、搜索、语言、账号、场景选择/执行/取消、实时开关、优先级、结果列表和历史滚动控件。自动化只通过 Avalonia.Headless 的 pointer、key、text、wheel、focus 和 render API 驱动控件，再从绑定属性、Command 状态、VisualTree、跨模块 ledger、逐场景证据与运行报告验证结果；禁止直接调用对应的 ViewModel command 来伪造控件覆盖。

搜索链必须真实经过 TextBox 双向绑定、Button/Enter command、Data `CancelPrevious`、EventBus `SearchExecuted`、State `SearchQuery` 和 UI dispatcher；取消链必须在执行中禁用搜索按钮并收敛到 `OperationStatus.Canceled`。语言与账号选择使用 ComboBox 键盘交互，分别验证 Localization 文案投影和 Security 在线/离线受限会话；场景控件必须验证单场景、运行中取消、取消后恢复及完整 12 场景矩阵。CheckBox、Slider、ListBox、ScrollViewer 与 100%/150%/200% 渲染帧用于验证双向绑定、选择、滚动和 Headless drawing。

### 4.5 Windows GUI 系统输入工作负载

`gui` profile 在完成相同的 Host、四窗口和跨模块初始化后保持运行，供独立 Windows 驱动接管。驱动使用 UIA 读取对象身份、边界和 Pattern 状态，使用 `SendInput` 执行关键鼠标键盘动作；二者必须共同成功，不能只靠固定坐标或应用内调用。每轮输出不少于 10 张实际屏幕截图、GUI driver report 和应用 run report，并在任何成功、失败或紧急中止路径恢复原鼠标位置。

## 5. Localization

### 5.1 资源规模

至少 480 个稳定 key，分为 shell 60、navigation 70、commerce 140、operations 100、security/data error 60、diagnostics 50。每个 key 有 `en-US` 与 `zh-CN` 主文本；另有 `en`、`zh-Hans` parent fallback 包故意只提供部分 key。

来源同时包含：

- generated assembly attribute + embedded package；
- 独立 assembly package provider；
- file package provider；
- in-memory package provider；
- Host、Module、Route、Window 和合成 Plugin scope descriptor。

### 5.2 场景

覆盖 lazy load、并发 load 合并、culture parent/fallback、作用域 lease、lookup context 隔离、格式化、missing marker、provider failure、bridge failure、dynamic register、owner/contribution revoke、tracked `LocalizedText` FIFO refresh、自释放 handler 和 service dispose。

至少 100 次快速 `en-US <-> zh-CN` 切换与并发导航/SignalR 推送，最终菜单、标题、Validation、DataError 和 Notification 显示同一 culture revision。应用代码负责把 revision dispatch 到 Avalonia；Presentation 不引用 localization contract。

## 6. Security

### 6.1 权限和 policy

- 64 个 `PermissionDescriptor`，覆盖普通、host-only、contribution owner 和 revoke。
- 24 个 `AuthorizationPolicy`，覆盖 authenticated、permission、claim、role 和组合 requirement。
- 36 条 route 挂 Security guard；40 个 command 使用 authorization source。
- 角色：Anonymous、Viewer、Operator、Supervisor、Finance、Administrator。

认证状态必须走完 Unknown、Anonymous、Authenticating、Authenticated、Refreshing、Expired、SignedOut、Failed。principal snapshot/Actor chain 做不可变性验证，diagnostics 和 reports 扫描敏感字段。

### 6.2 Token 与账号

默认 `AccountSessionManager` 和文件 Provider 保存 Alice/Bob/Admin 三组账号资料、权限和多资源凭据；Data 的 HTTP credential adapter 从当前 Admin 账号读取 `dogfood-operations` token。独立 `DelegateAccessTokenProvider` 只用于覆盖 Success/None/Required/Expired/Failed/Unavailable/Cancelled 稳定结果，不参与正常 Data 请求。

账号场景执行 Alice 在线切换、Bob 过期权限/凭据拒绝、Bob 显式离线受限切换、Admin 在线切换与重复幂等；随后枚举账号、删除拥有两个资源凭据的临时账号，并重建 file stores 与 manager，验证最后活动 Admin 及其 token 可以恢复。测试只使用 Security 公开合同，不保留应用私有平行 coordinator。

## 7. Data

### 7.1 真实 loopback server

| 协议 | 工作负载 |
| --- | --- |
| HTTP | 24 个 REST endpoint；GET/query/cache、POST mutation、分页、条件请求、429/5xx/timeout、64 MiB upload/download/range resume |
| gRPC | 8 unary、4 server streaming、2 client streaming、2 bidi streaming；metadata、deadline、cancel、resource exhausted、data loss |
| SignalR | 12 个 invoke method、10 种 server push；automatic reconnect、账号 revision、订阅撤销、顺序 gap |

正常 profile 使用官方 native client；gRPC/SignalR delegate adapter 兼容入口也各执行一组场景。服务器只监听随机 `127.0.0.1` 端口，生命周期归 automation root scope。

### 7.2 Pipeline 矩阵

- transport：HTTP、gRPC adapter/native、SignalR adapter/native；
- concurrency：AllowConcurrent、DisallowConcurrent、Queue、CancelPrevious、LatestWins、KeyedSerial；
- resilience：timeout、retry、circuit breaker、rate limit、fallback；
- cache：hit/miss/TTL、principal/permission/route/client/policy revision、mutation invalidation、stale write suppression；
- capability：Host 与合成 contribution allow/deny，必须在 credential/cache 前执行；
- handler：8 个 fixed handler + 4 个 dynamic handler，覆盖 order、single continuation、throw、reentry；
- connection：Application/Module/Plugin/Operation owner、start/stop/reconnect、逆序清理和失败聚合；
- large payload：progress throttle、resume、declared-length mismatch、cancel、temp file cleanup；
- result/error：Success/Failed/Cancelled/Stale 及所有稳定 `DataErrorKind` 映射。

每次请求携带 correlation、principal、permission、route、client、policy 和 contribution revision。结果提交 State/cache/UI 前重验 operation identity，旧账号、旧 route、旧 mutation epoch 或已取消结果必须抑制。
