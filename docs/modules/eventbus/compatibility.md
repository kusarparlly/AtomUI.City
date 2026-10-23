# AtomUI.City.EventBus Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Publish 不隐式切 UI 线程。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。
- 跨插件事件类型必须来自 Host 共享 contract 程序集。
- 默认派发顺序稳定。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `IEventBus.PublishAsync` / `PostAsync` 的 null event、预取消 token、disposed bus、publish options、correlation/causation 传播和 delivery/post result 边界进入 1.0 兼容承诺。
- `EventDeliveryResult` 成功结果的 error message 必须为 null；`EventPostResult` accepted 结果的 rejection reason 必须为 null，rejected 结果必须携带非空白 rejection reason。
- `EventDeliveryResult.Duration`、`EventBusMetricsSnapshot`、`IEventBusMonitor`、`EventBusDiagnosticsOptions` 和安全 payload projector 合同进入 1.0 公开 API 基线。
- EventBus 默认 `InMemoryHostDiagnostics` 容量为 2048；应用预注册 sink 的 override 语义保持不变。
- `IEventSubscriber.Subscribe` 只保留显式 `LifecycleScope` owner 的异步 delegate 和 `IEventHandler<TEvent>` 两种核心 handler 形式，并分别提供默认/命名 channel overload；两者都通过显式末尾 token 接收唯一有效令牌。同步 `Action<EventContext<TEvent>, CancellationToken>` 由同样有 owner 的默认/命名 channel 扩展方法提供。
- 一条 subscription 只有一个 owner；owner running 原子校验、Quiescing barrier、唯一终止事务、StopAsync drain、owner cancellation 和 EventBus dispose 清理进入 1.0 兼容承诺。
- `EventSubscriptionState` 的稳定值为 `Created=0`、`Active=1`、`Quiescing=2`、`Draining=3`、`Disposed=4`、`Faulted=5`；timeout 不形成独立永久状态。
- `IEventContractRegistry` 的 shared-only 配置期注册、Default ALC 身份校验、冻结快照、精确 id/type 查询、重复 contract id/type 拒绝，以及冻结后未知 contract 拒绝进入 1.0 兼容承诺；DI 创建 EventBus 前必须汇入 collected descriptors、冻结并核验映射。
- `EventContractAttribute`、`EventChannelAttribute`、`EventHandlerAttribute`、`GeneratedEventManifestAttribute.CurrentVersion`、`GeneratedEventHandlerDescriptor`，以及 `EventContractDescriptor.SchemaVersion/SchemaFingerprint` 进入 1.0 兼容边界。修改 attribute 构造参数或默认值、generated registrar 形状、manifest version 或 fingerprint canonicalization 都属于显式 schema 变更。
- `EventPayloadDiagnosticProjectorDescriptor`、`EventPluginAccessRule` 和 `ThreadPoolEventBackgroundScheduler` 是 Preview public support types；投影擦除、access/schema range 和 ExecutionContext 抑制语义变化必须同步 API 合同与测试。
- `AddEventPayloadDiagnosticProjector<TEvent,TProjector>` 必须在 `TProjector` 上传播 `DynamicallyAccessedMembers(PublicConstructors)`；这是 City 对 Microsoft DI 构造激活承担的 NativeAOT 元数据合同，应用不需要另写 linker descriptor。
- `AtomUI.City.Generated.GeneratedModularityModuleRegistrar_*` 的 public visibility 只用于跨程序集 generated catalog 装载，不授予开发者手工调用合同；稳定的是 registrar identity/manifest 协议和每程序集唯一聚合规则，而不是允许业务源码引用生成类名。
- generated handler 的 `TEvent` 必须通过本地/引用 generated catalog 编译期门禁；实际选中 Module 的 handler/contract 闭包必须在 Host Build 阶段成立。把上述失败延迟到 Host Start 或放宽为仅检查 CLR type/attribute 属于兼容性和安全边界变化。
- `PluginPrivate` descriptor 只接受 collectible non-default ALC 类型且不能进入 Shared Registry；`EventBus.EventContractRejected` 及其 `eventType`、`channel`、`operation` context 进入 1.0 兼容承诺。
- 插件可访问的 Shared contract 必须来自 generated closed-object-graph proof；白名单由稳定 scalar、contract-local enum/immutable DTO 及递归安全的 `Nullable<T>`、`ImmutableArray<T>`、`KeyValuePair<TKey,TValue>` 构成。扩大或收紧白名单、改变 proof 生成规则或允许手工 descriptor 获得插件 capability 属于兼容性变更。
- 009 的 `EventPluginPlane`、`EventPluginAccess`、`EventBusContributionState` 数值，request/access/quota 模型、contribution controller/lease 和受限 publisher/subscriber 是 EventBus 与 PluginSystem 的跨模块协议。EventBus 侧合同已经冻结；在 PluginSystem 真实集成验收前，完整 Plugin Plane 仍不得标记为 Verified。
- `EventPluginQuotas.DrainTimeout` 默认 30 秒并作为单个 contribution 唯一终止事务的总 deadline；超时后的 `Faulted` 状态、`EventPluginDrainTimeoutException`、`EventBus.EventPluginDrainTimedOut` context 以及真实清理完成前禁止复用 PluginId 进入 1.0 跨模块兼容承诺。
- Plugin Lease 的 pending registration barrier、Subscribe 三阶段提交/回滚、Quiescing 前发布唯一终止 Task，以及不在 Lease 锁内调用 EventBus/Registry/Diagnostics 的重入安全规则进入 1.0 并发兼容承诺。
- `EventDispatchPolicy` 的 `Current=0`、`UiThread=1`、`Background=2`、`Serialized=3`，`EventDispatchMode` 的 `Post=0`、`InlineIfAllowed=1`，以及 `EventErrorPolicy` 的 `ContinueAndReport=0`、`StopPublication=1`、`FailPublisher=2`、`DisableSubscription=3` 进入 1.0 兼容承诺。
- `EventDeliveryStatus` 的 `Succeeded=0`、`Failed=1`、`Canceled=2`、`TimedOut=3`、`Skipped=4`，默认 handler timeout `30s`、默认 DisableSubscription 连续失败阈值 `3`，以及默认单次 publication 最大 delivery 并发 `16` 进入 1.0 兼容承诺。
- 两类 public metrics snapshot 拒绝 default ContractId、未知 execution mode、空白 channel、非正 capacity、负计数和负 duration；放宽为可表达不可能状态属于合同变更。
- `EventChannel<TEvent>.Default.Name="default"`，channel identity 由精确事件类型和 ordinal name 组成；默认 contract channel 的 `Capacity=256`、`Wait`、`Serialized`、`MaximumConcurrency=1` 和无限 queue wait 进入 1.0 兼容承诺。
- `EventChannelBackpressurePolicy` 的 `Wait=0`、`Reject=1`、`DropOldest=2`、`DropNewest=3`、`CoalesceLatest=4`，以及 `EventChannelExecutionMode` 的 `Serialized=0`、`Partitioned=1`、`Concurrent=2` 进入 1.0 兼容承诺。
- `ConfigureEventChannel<TEvent>` 的精确 type/channel override、重复配置拒绝，命名 channel 发布/订阅隔离，以及 `PartitionKey` 模式匹配规则进入 1.0 兼容承诺。
- `EventBusRuntimeOptions.MaximumChannelRuntimes` 默认值为 256、合法范围为 1..65536；达到上限时不驱逐已有 runtime，Publish 明确失败而 Post 返回 rejected result。`ConfigureEventBusRuntime` 使用 DI 中的唯一配置替换语义。这些资源边界进入 1.0 兼容承诺。
- 同一默认 Serialized channel 中 PublishAsync/PostAsync 共享 admission、按接受顺序执行；Post 只等待接受而 Publish 等待完整交付。`EventPublicationRejectedException` 保留 EventId 和 ContractId，进入 1.0 兼容承诺。
- `EventDiagnosticIds` 已登记的 `EventBus.Event*` 字符串、delivery failure/cancellation/dropped 的定位 context，以及 subscription quiescing/disposed/termination-failed 的 `subscriptionId` context 进入 1.0 兼容承诺。
- `AddEventBus` 默认注册 `IHostDiagnostics`、`IEventContractRegistry`、`IEventBus`、`IEventPublisher` 和 `IEventSubscriber`，并保留调用方预注册 diagnostics 的 override 行为，进入 1.0 兼容承诺。
- 单独调用 `AddEventBus` 保留普通 Microsoft DI 的立即可用模式；City 应用选择 `EventBusModule` 后切换为 Host-managed 模式，必须等 Core application initialization 才开放操作，并在 module shutdown 加入同一终止事务。`EventBusModule` 只能是无状态适配器，不得与 `IHostedService` 形成双生命周期。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。

## 1.0 前 Subscription API 收口

EventBus 尚未正式发布，下列第一版源码入口不进入 1.0 合同，施工时直接移除而不建立长期 obsolete 周期：

- 所有 ownerless `Subscribe<TEvent>` overload。
- `Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask>)` 简化入口。
- `EventContext<TEvent>.CancellationToken` 以及 tokenless handler/delegate 入口；迁移到显式末尾 `CancellationToken`。
- 核心接口中的同步 `Action<EventContext<TEvent>>` overload；迁移到 `EventSubscriberExtensions.Subscribe` 并显式传入 owner。

迁移规则：应用级静态/DI handler 由 generated registration 自动绑定 `ApplicationScope`；Window、Route、Activation 等动态订阅传入对应 `LifecycleScope`；插件 handler 通过 EventBus contribution contract 注册并由领域 Lease 持有。
