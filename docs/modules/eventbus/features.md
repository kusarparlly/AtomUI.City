# AtomUI.City.EventBus Features

本文件是 EventBus 唯一 Feature 来源。没有 Feature ID 的能力不能进入实现；专题设计、API contract、诊断和测试矩阵必须引用本文件中的 Feature ID。

EventBus Application Plane 已完成实现与模块级验证；Plugin Plane 的 EventBus 侧 contract 已冻结，真实 PluginSystem 集成仍待后续模块施工。

## Feature 索引

| Feature ID | 名称 | 阶段 | 状态 | Contract Family | 主测试族 |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publication | Application Plane MVP | Verified | IEventPublisher, EventPublishOptions, EventContext, EventPublishResult, EventPostResult | EventPublicationTests |
| AUC-EVENTBUS-002 | Subscription Ownership & Lifecycle | Application Plane MVP | EventBus Contract Verified / PluginSystem Integration Pending | IEventSubscriber, IEventHandler, IEventSubscription, subscription state/options/descriptor | EventSubscriptionTests |
| AUC-EVENTBUS-003 | Contract Identity & Registry | Application Plane MVP | Verified | EventContractId, EventContractDescriptor, EventContractPlane, IEventContractRegistry | EventContractRegistryTests |
| AUC-EVENTBUS-004 | Dispatch & Failure Policy | Application Plane MVP | Verified | dispatch target/mode, delivery result, error and timeout policy | EventDispatchAndFailurePolicyTests |
| AUC-EVENTBUS-005 | Diagnostics & Observability | Application Plane MVP | Verified | EventDiagnosticIds, diagnostic context, metrics and safe payload projection | EventDiagnosticsTests |
| AUC-EVENTBUS-006 | DI & Host Lifecycle | Application Plane MVP | Verified | AddEventBus, EventBus module/options, internal lifecycle control plane | EventBusHostIntegrationTests |
| AUC-EVENTBUS-007 | Bounded Channel Runtime | Application Plane MVP | Verified | EventChannel, channel options, execution/concurrency/backpressure policy | EventChannelRuntimeTests |
| AUC-EVENTBUS-008 | Generated Event Catalog & NativeAOT | Application Plane MVP | Verified | event attributes, generated registrar/manifest/descriptors/invokers | EventBusGeneratorTests; EventBusNativeAotProcessTests |
| AUC-EVENTBUS-009 | Plugin Event Planes | Plugin Integration Phase | EventBus Contract Verified / PluginSystem Integration Pending | shared/private plane, capability, EventBus contribution controller and domain lease | EventBusPluginContractTests；PluginSystem lifecycle suite pending |

## 完成口径

- `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 全部达到 `Verified`，表示 EventBus Application Plane MVP 完成。
- `AUC-EVENTBUS-009` 达到 `Verified`，表示 EventBus Plugin Plane 完成。
- Plugin Plane 的 EventBus 侧接口和测试替身在当前设计阶段冻结；真实动态插件、AssemblyLoadContext drain/unload 和 capability 集成等待 PluginSystem 施工，不阻断 Application Plane MVP。
- 性能 Benchmark、确定性调度器和 CLI dogfood 是横向验收门禁，不单独建立运行时 Feature。

## Feature 硬门禁

| 约束 | 归属 | 验收要求 |
| --- | --- | --- |
| Publish 不提供阻塞式同步入口，也不隐式切换 UI 线程。 | 001, 004 | 必须有 API contract、线程测试和真实 Host 验证。 |
| handler 外部代码不能在 EventBus 内部锁内执行。 | 002, 004, 007 | 必须覆盖 publish/subscribe/stop 并发和重入。 |
| 所有订阅都有明确 owner，并返回可停止、可释放句柄。 | 002 | 必须覆盖 owner stop、drain、timeout、重复释放和 Dispose 后行为。 |
| 所有异步 channel 和订阅队列有界。 | 007 | 必须覆盖容量、背压、取消、关闭及资源上限。 |
| Handler 错误、取消、丢弃和超时不能静默丢失。 | 004, 005, 007 | 必须产生声明的 Result、异常或稳定诊断。 |
| 跨插件事件对象图只使用 Host 共享 contract。 | 003, 009 | 必须在插件代码执行前校验，并覆盖卸载残留。 |
| 默认生产路径不扫描程序集或反射调用 handler。 | 008 | 必须覆盖 generated catalog、静态 invoker、trimming 和 NativeAOT。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract，不能把核心 API 留给施工阶段决定。
- Feature 必须定义非法输入、状态非法、取消、重复调用、并发冲突和释放后行为。
- Feature 必须定义诊断码或明确说明由哪个上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、build 或 generator 的功能必须增加专项测试。
- Feature 只有在文档、实现、测试、诊断、兼容性和发布门禁全部提供证据后才能标记为 `Verified`。

## AUC-EVENTBUS-001 Typed Publication

Feature ID: `AUC-EVENTBUS-001`
Status: Verified
Goal: 提供强类型、可取消、可观察完成状态的进程内事件发布。
Contract Family: `IEventPublisher`、`EventPublishOptions`、`EventContext<TEvent>`、`EventPublishResult`、`EventPostResult`。
Design Coverage: `PublishAsync` 等待当前 delivery plan 完成；`PostAsync` 只等待受管有界 channel 接受；定义 EventId、correlation/causation、publish depth、订阅快照时点及无订阅者语义。
Failure Coverage: null/default 输入、非法 options、预取消、总线停止、channel 拒绝、结果模型非法状态。
Primary Tests: `EventPublicationTests`。

## AUC-EVENTBUS-002 Subscription Ownership & Lifecycle

Feature ID: `AUC-EVENTBUS-002`
Status: EventBus Contract Verified / PluginSystem Integration Pending
Goal: 把每个 handler 的所有权、创建上下文、停止、drain 和释放纳入唯一生命周期事务。
Contract Family: `IEventSubscriber`、`IEventHandler<TEvent>`、`IEventSubscription`、subscription state/options/descriptor。
Design Coverage: 每条订阅只有一个 owner；静态/DI 订阅由框架绑定 ApplicationScope，Application Plane 动态订阅显式绑定 LifecycleScope，插件订阅绑定 EventBus 领域 ContributionLease；第一版强引用；定义 Created、Active、Quiescing、Draining、Disposed、Faulted 和唯一终止事务。
Failure Coverage: null/stopped owner、owner/EventBus stop 与注册提交竞争、in-flight handler、等待取消或 drain timeout、handler 创建/释放失败、重复及并发 Stop/Dispose/owner cancellation。
Primary Tests: `EventSubscriptionTests`。

## AUC-EVENTBUS-003 Contract Identity & Registry

Feature ID: `AUC-EVENTBUS-003`
Status: Verified
Goal: 为事件提供独立于临时 CLR 名称的稳定身份、唯一映射和加载边界。
Contract Family: `EventContractId`、`EventContractDescriptor`、`EventContractPlane`、`IEventContractRegistry`。
Design Coverage: ContractId/精确 Type 双向唯一映射、Shared/PluginPrivate plane 与 Assembly/ALC 身份、配置期只读快照、Register/Freeze 原子竞争、DI descriptor 汇入与生产冻结。版本、schema/object graph 生成验证属于 AUC-EVENTBUS-008；Plugin Private Registry 动态生命周期属于 AUC-EVENTBUS-009。
Failure Coverage: default/重复 id、重复 type、冻结后写入或未知 contract、私有类型进入 Shared Plane、Default/non-collectible ALC 冒充 PluginPrivate、自定义 Registry 丢失 collected descriptor。
Primary Tests: `EventContractRegistryTests`。

## AUC-EVENTBUS-004 Dispatch & Failure Policy

Feature ID: `AUC-EVENTBUS-004`
Status: Verified
Goal: 明确 handler 的执行目标、完成语义、错误隔离、取消和超时行为。
Contract Family: dispatch target/mode、delivery result、error policy、handler timeout policy。
Design Coverage: Current、UiThread、Background 和受管 Serialized dispatch；Post/InlineIfAllowed；ContinueAndReport、StopPublication、FailPublisher、DisableSubscription；订阅级 handler timeout 和有界 publication delivery 并发。1.0 只开放 subscription error policy，不声明尚未落地的 Global/Contract/Channel/Plugin 优先级。
Failure Coverage: dispatcher unavailable、handler failure/cancellation/timeout、PostAsync 后台失败、错误策略冲突和 cleanup failure。
Primary Tests: `EventDispatchAndFailurePolicyTests`。

## AUC-EVENTBUS-005 Diagnostics & Observability

Feature ID: `AUC-EVENTBUS-005`
Status: Verified
Goal: 用稳定诊断和指标重建 publication、delivery、subscription、channel 与插件因果链。
Contract Family: `EventDiagnosticIds`、EventBus diagnostic context、metrics snapshot、safe payload projector。
Design Coverage: EventId、CorrelationId、CausationId、ContractId、SubscriptionId、owner、channel、partition、policy 和 duration。
Failure Coverage: 诊断 sink 不可用、payload/exception 持有插件对象、限流、丢弃、超时和卸载残留。
Primary Tests: `EventDiagnosticsTests`。

## AUC-EVENTBUS-006 DI & Host Lifecycle

Feature ID: `AUC-EVENTBUS-006`
Status: Verified
Goal: 将 EventBus 作为 ApplicationScope 运行时模块接入 Core Build、Start、Stop 和 Dispose。
Contract Family: `AddEventBus`、EventBus module/options、只读业务接口、internal lifecycle controller。
Design Coverage: descriptor 收集、配置冻结、worker 启停、拒绝新操作、总 deadline 下的 drain、Root DI 能力隔离。
Failure Coverage: 重复注册、配置非法、启动中途失败、Stop-before-Start、并发 Stop/Dispose 和清理聚合失败。
Primary Tests: `EventBusHostIntegrationTests`。

## AUC-EVENTBUS-007 Bounded Channel Runtime

Feature ID: `AUC-EVENTBUS-007`
Status: Verified
Goal: 用有界 channel 提供声明式顺序、并发、partition 和背压控制。
Contract Family: `EventChannel<TEvent>`、`EventChannelOptions`、`EventBusRuntimeOptions`、execution/concurrency/backpressure policy。
Design Coverage: Serialized、Partitioned、Concurrent；Wait、Reject、DropOldest、DropNewest、CoalesceLatest；有限 capacity、全局 runtime 总量、并发度、partition 数量和回收规则。
Failure Coverage: queue full/closed、runtime 总量达到上限、等待取消或超时、drop/coalesce 诊断、递归发布、serialized 自等待和 shutdown drain。
Primary Tests: `EventChannelRuntimeTests`。

## AUC-EVENTBUS-008 Generated Event Catalog & NativeAOT

Feature ID: `AUC-EVENTBUS-008`
Status: Verified
Goal: 在编译期生成 contract、handler、channel 和 manifest 元数据，并接入唯一生产注册流程。
Contract Family: event/handler/owner attributes、generated registrar/manifest、descriptor 和 strongly typed invoker。
Design Coverage: 多程序集 catalog 合并、稳定 registrar identity、Module owner、生成代码 Host 接入、manifest version、无运行时扫描或 `MethodInfo.Invoke`。
Failure Coverage: owner 缺失/冒充、重复 ContractId、非法对象图、非法 plane/capability、manifest 版本和 registrar 冲突。
Primary Tests: `EventBusGeneratorTests`、`EventBusNativeAotProcessTests`。

### 1.0 frozen design

- `[EventContract(contractId, ownerModuleType)]` 是 Shared Application Plane 事件的唯一生成入口；contract 与 owner 必须由同一程序集声明，owner 必须实现 `IModule`。`SchemaVersion` 必须大于零。
- `[EventChannel(name)]` 可以在同一 contract 上声明零到多个命名 channel；容量、背压、执行模式、并发度和等待超时在编译期验证并生成 `EventChannelDescriptor`。
- `[EventHandler(ownerModuleType)]` 标记实现且只实现一个 `IEventHandler<TEvent>` 的可构造 class。handler 与 owner 必须同程序集；生成代码使用封闭泛型 descriptor 建立强类型调用，不允许 `MethodInfo.Invoke`。
- `[EventHandler]` 的 `TEvent` 必须属于可达 generated Shared contract catalog：本 compilation 中必须存在有效 `[EventContract]` candidate；引用程序集必须同时携带版本匹配且指向同一 registrar 的 Event manifest 与 Core service manifest。缺失、伪造或不可达 contract 产生 AUCGEN005，并阻止当前 compilation 的 registrar 输出。
- 每个程序集仍只生成 Core `GeneratedServiceManifestAttribute` 指向的唯一 `IServiceRegistrar`。EventBus contribution 与普通 generated service 合并进入该 registrar，并按 owner 分组；不得创建第二套程序集扫描、第二套 owner 选择或第二个 Root Provider。
- 生成程序集同时写入 `GeneratedEventManifestAttribute(registrarType, version)`，只表达稳定工具元数据。Host 的生产加载仍由 Core generated service catalog 完成；不在运行时枚举程序集或类型。
- Shared contract 完整对象图采用封闭白名单：稳定 scalar、contract-local enum、sealed immutable class、readonly struct，以及递归安全的 `Nullable<T>`、`ImmutableArray<T>`、`KeyValuePair<TKey,TValue>`。`object`/`dynamic`、interface、abstract/mutable/local generic、数组和未批准的外部类型产生 AUCGEN005，且不得生成 registrar contribution。
- 生成 registrar 使用 `EventContractDescriptor.GeneratedShared<TEvent>` 携带 object-graph validation proof。手工 `AddEventContract<TEvent>` 只具备 Application Plane 身份，不得被 Plugin contribution capability 引用。
- 只有被 Host 模块图选中的 owner contribution 才进入 Root DI。静态 handler 在 `EventBusModule` 启动时绑定到同一个 `ApplicationScope`，Scope/Host 终止时确定性撤销。
- `EventBusModule.PostConfigureServices` 在 Root Provider 创建前校验实际选中 contribution 的 handler/contract 闭包；因此跨 Module handler 合法，但提供 contract 的 Module 未被选中时，Host `Build()` 必须失败且不得激活任何 handler。
- 跨程序集聚合按 registrar `Type` 去重，不按 owner 去重；引用链中的同一 registrar 只执行一次，而一个 owner 可以合法拥有多个本程序集 contribution。
- 008 只生成 `Shared` plane。`PluginPrivate`、capability 和动态 contribution lease 由 009 实现；在 008 attribute 中不得伪装插件 plane。

## AUC-EVENTBUS-009 Plugin Event Planes

Feature ID: `AUC-EVENTBUS-009`
Status: EventBus Contract Verified / PluginSystem Integration Pending
Goal: 允许 PluginSystem 通过受控协议使用 Shared Plane，并为单个插件建立可整体释放的 Private Plane。
Contract Family: plugin event capability、EventBus contribution request/controller/domain lease、private plane runtime contract。
Design Coverage: EventBus 不依赖 PluginSystem 具体实现；EventBus 侧先冻结接口和测试替身；真实动态插件集成在 PluginSystem 阶段完成。
Failure Coverage: capability denied、contract/version 不兼容、半激活回滚、quiescing、drain timeout、私有类型泄漏和 ALC 无法回收。
Primary Tests: `EventBusPluginContractTests`；PluginSystem 可用后增加 `EventBusPluginLifecycleTests`。

### EventBus-side frozen contract

- EventBus 定义 `EventBusContributionRequest`、`IEventBusContributionController`、`IEventBusContributionLease` 和受限 plugin publisher/subscriber；不得引用 PluginSystem 的 manifest、loader 或具体 Plugin 类型。
- request 只携带稳定 `PluginId`、Shared ContractId/channel 权限、PluginPrivate descriptors 和本领域资源配额。Shared 权限按 contract、channel、Publish/Subscribe 方向精确匹配，默认拒绝。
- controller 只能在 Host-managed EventBus 已进入 ApplicationScope 后创建 lease。同一 PluginId 只能存在一个 Active/terminating lease；半激活必须完整回滚。
- lease 为插件建立 ApplicationScope 子 Scope。Shared subscriptions 与 Private Plane 的 registry、runtime、queue、handler/delegate cache 全部由该 lease 持有，不挂到插件可访问的 Host Scope。
- `Active -> Quiescing -> Draining -> Disposed/Faulted` 是唯一终止事务。Quiescing 后拒绝新 publish/post/subscribe；等待取消只取消调用方等待，不撤销后台终止事务。
- `EventPluginQuotas.DrainTimeout` 是整个 EventBus contribution 清理链的单一总 deadline，默认 30 秒，不能按 subscription/operation/Scope/private runtime 分别重置。超时进入 `Faulted`、写 `EventBus.EventPluginDrainTimedOut` 并抛稳定 timeout exception；迟到清理继续受观察，但真正结束前不得释放 PluginId 给新 lease。
- Lease admission 使用三阶段事务：锁外完成 contract/capability 查询，锁内预留 pending registration/operation，锁外调用 EventBus，最后锁内提交或决定回滚。Quiescing 必须等待 pending registration 在锁外完成底层 subscription 回滚；Diagnostics 和 `_onTerminated` 均不能在 Lease 锁内执行。
- Controller 创建 contribution 的最终字典提交也只在总线锁内决定成功/失败；提交失败后的 `lease.Dispose()` 必须移到锁外，避免跨层锁顺序和诊断重入。
- Lease 构造时保持 Activating 且不发出 Activated 诊断；Controller 先原子占有 PluginId，随后在锁外切换 Active 并写诊断，最后复核 Host/字典身份。Activated 诊断重入同名 Create 必须看到已占用身份并失败，不能让内层实例抢先提交。
- PluginPrivate descriptor 必须来自同一个 collectible non-default ALC，Private Plane 不进入 Root DI、Shared Registry 或 Host 静态泛型缓存。
- EventBus 侧本轮使用 fake plugin request 验证协议和释放；真实 manifest 映射、插件 DI Scope 顺序与 ALC `WeakReference` 回收由 PluginSystem 集成阶段验收。
