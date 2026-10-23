# AtomUI.City.EventBus Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.EventBus` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.EventBus` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publication | EventPublicationTests |
| AUC-EVENTBUS-002 | Subscription Ownership & Lifecycle | EventSubscriptionTests |
| AUC-EVENTBUS-003 | Contract Identity & Registry | EventContractRegistryTests |
| AUC-EVENTBUS-004 | Dispatch & Failure Policy | EventDispatchAndFailurePolicyTests |
| AUC-EVENTBUS-005 | Diagnostics & Observability | EventDiagnosticsTests |
| AUC-EVENTBUS-006 | DI & Host Lifecycle | EventBusHostIntegrationTests |
| AUC-EVENTBUS-007 | Bounded Channel Runtime | EventChannelRuntimeTests |
| AUC-EVENTBUS-008 | Generated Event Catalog & NativeAOT | EventBusGeneratorTests; EventBusNativeAotProcessTests |
| AUC-EVENTBUS-009 | Plugin Event Planes | EventBusPluginContractTests; EventBusPluginLifecycleTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.EventBus Diagnostics and Testing 设计

适用范围：事件诊断上下文、结构化记录、错误策略、指标、事件链、测试工具、确定性调度、插件卸载断言和性能验证。

### 1. 定位

EventBus 是高度解耦的系统。发布方通常不知道订阅方，订阅方也可能位于另一个模块、线程或插件中。如果缺少结构化诊断，事件丢失、顺序异常、handler 阻塞和插件卸载失败会很难定位。

EventBus 诊断必须回答：

- 谁发布了事件。
- 事件进入了哪个 channel。
- 捕获了哪些订阅。
- 每个 handler 在哪个执行目标运行。
- 是否排队、丢弃、取消或超时。
- 哪个插件或 Scope 持有订阅。
- 当前为什么无法停止或卸载。

### 2. DiagnosticContext 集成

EventBus 复用 Core `DiagnosticContext`，并增加事件维度：

- EventId。
- EventContractId。
- Channel。
- PartitionKey hash。
- CorrelationId。
- CausationId。
- PublishDepth。
- PublisherModuleId。
- PublisherPluginId。
- PublisherScopeId。
- SubscriptionId。
- HandlerTypeId。
- SubscriberModuleId。
- SubscriberPluginId。
- DispatchTarget。
- QueueWaitDuration。
- HandlerDuration。
- DeliveryResult。

Application Plane 1.0 已有上下文必须先落地；Publisher/Subscriber ModuleId、PluginId 和插件稳定身份由 `AUC-EVENTBUS-009` 的受限 publisher/subscriber 与 contribution lease 提供，不允许 Application Plane 通过持有插件对象提前伪造。

不应默认记录敏感 partition key 原文。

### 3. EventId、CorrelationId 与 CausationId

每次发布生成唯一 `EventId`。

规则：

- 外部调用没有 CorrelationId 时创建新值。
- Handler 发布子事件时继承 CorrelationId。
- 子事件的 CausationId 指向父 EventId。
- PublishDepth 加一。
- `PostAsync` 仍保留 correlation/causation 关系。
- Handler 内部未显式提供关联选项时，EventBus 通过当前 delivery 上下文自动完成上述继承；上下文必须在 handler 结束时恢复，不得泄漏到无关异步链。

事件链：

```text
Command operation
-> Event A
   -> Handler A1
      -> Event B
   -> Handler A2
```

诊断系统可以通过 CorrelationId 重建事件因果链。

### 4. Diagnostic Record

建议事件总线产生结构化 record：

| Record | 说明 |
|---|---|
| `EventPublished` | 发布开始和 publisher 信息。 |
| `EventAccepted` | PostAsync 事件进入 queue。 |
| `EventRejected` | Contract、capability、queue 或 lifecycle 拒绝。 |
| `EventDeliveryStarted` | Handler 开始。 |
| `EventDeliveryCompleted` | Handler 成功完成。 |
| `EventDeliveryFailed` | Handler 失败。 |
| `EventDeliveryCancelled` | Handler 取消。 |
| `EventDropped` | 背压策略丢弃。 |
| `EventSubscriptionAdded` | 新订阅进入 snapshot。 |
| `EventSubscriptionQuiescing` | 订阅停止接收新投递。 |
| `EventSubscriptionDisposed` | 订阅完全释放。 |
| `EventSubscriptionTerminationFailed` | 订阅终止或资源清理失败。 |
| `EventChannelBackpressure` | Queue 达到阈值。 |
| `EventPluginDrainTimedOut` | 插件 handler 无法在时限内结束。 |

`EventPluginDrainTimedOut` 的 code 在 `AUC-EVENTBUS-009` 冻结并实现；Application Plane 不得在尚无 plugin drain 事务时产生伪诊断。

### 5. Payload 记录

默认不记录完整事件 payload。

原因：

- 可能包含敏感信息。
- 可能很大。
- 可能包含插件私有对象。
- 长期保存 payload 会阻止插件卸载。
- `ToString()` 可能执行用户代码。

默认只记录：

- EventContractId。
- EventId。
- Schema version。
- Payload size estimate。
- 明确标记为安全的摘要字段。

当前 1.0 实现的 EventBus 结构化 context key 为 `contractId`、`eventId`、`subscriptionId`、`eventType`、`dispatchPolicy` 和 `errorPolicy`。Delivery failure 与 cancellation 必须至少包含 `contractId`、`eventId` 和 `subscriptionId`，posted FailPublisher 后台失败也必须保留这些 delivery 定位字段。

Payload diagnostics 必须 opt-in，并通过 contract 提供的稳定 formatter 生成 Host-owned snapshot。

Application Plane 1.0 使用强类型 `IEventPayloadDiagnosticProjector<TEvent>`。Projector 失败必须与发布事务隔离；插件 projector 的所有权、lease 和卸载在 `AUC-EVENTBUS-009` 收口。公开的 `AddEventPayloadDiagnosticProjector<TEvent,TProjector>` 必须向 NativeAOT linker 声明 `TProjector` 的 public constructors 可达；双 TFM 原生测试必须使用带 DI 构造依赖的 projector 走完整注册、解析和投影路径，不能只验证 generated event catalog。

### 6. Exception 记录

静态模块异常可以进入 Core diagnostic sink。

插件异常：

- 在当前调用链中可用于错误策略。
- 长期缓冲只保存稳定异常摘要。
- 不长期保存插件 Exception 实例。
- 不保存插件 stack object references。

稳定摘要包括：

- Error code。
- Exception type name string。
- Message。
- Sanitized stack trace string。
- PluginId。
- EventId。
- SubscriptionId。

### 7. 错误策略

EventBus 错误策略：

| 策略 | 行为 |
|---|---|
| `ContinueAndReport` | 继续独立 handler，聚合失败。默认。 |
| `StopPublication` | 不再开始剩余 delivery。 |
| `FailPublisher` | 传播 handler 异常给 publisher。 |
| `DisableSubscription` | 订阅进入 quiescing 并移除。 |

1.0 Application Plane 只开放 subscription-level error policy，因此不存在运行时优先级合并或静默覆盖。Global、Event contract、Channel 和 Plugin policy 属于后续扩展；引入前必须先在 API contract 中定义“只能收紧”的合并规则，不能提前假设已经实现。

### 8. 连续失败

EventBus 可以维护订阅健康状态：

- ConsecutiveFailureCount。
- LastFailureTime。
- LastSuccessTime。
- DisabledReason。

达到阈值时：

- 记录 health diagnostic。
- 根据策略禁用订阅。
- 插件 handler 可以触发插件降级。

失败计数必须有恢复窗口，避免一次历史错误永久污染订阅状态。

### 9. 指标

建议指标：

- Publications total。
- Publications by contract/channel。
- Active subscriptions。
- Queue depth。
- Queue capacity utilization。
- Dropped/rejected events。
- Handler duration。
- Queue wait duration。
- Handler failures。
- Handler cancellations。
- In-flight handlers。
- Plugin drain duration。
- Snapshot replacements。

指标标签必须受控，不能把 EventId、用户 id 或任意 partition key 作为高基数标签。

Application Plane 1.0 必须公开线程安全的 EventBus 快照，至少包含 active subscription、publication、delivery 终止结果、diagnostic sink failure 与累计 handler duration；channel 快照继续承载 queue depth、capacity、backpressure 和 queue wait。

### 10. 日志级别

建议：

| 场景 | 级别 |
|---|---|
| 普通发布完成 | Trace/Debug。 |
| Queue 高水位 | Warning。 |
| Event rejected | Warning 或 Error，取决于原因。 |
| Handler failed | Error。 |
| Handler cancelled | Debug。 |
| Event dropped | Warning。 |
| Subscription disabled | Warning/Error。 |
| Plugin drain timeout | Error。 |
| Shared Contract 冲突 | Error/Fatal。 |

高频事件不能默认逐条输出 Information 日志。

EventBus Trace 记录采样决策必须基于 EventId 且在同一 publication 内保持一致；Warning/Error 和所有非成功终止记录不采样。

### 11. 诊断缓冲

桌面应用现场排查可以使用有界内存诊断缓冲。

规则：

- 缓冲必须有容量上限。
- 保存稳定 record，不保存 handler 和 payload 对象。
- 支持按 CorrelationId 查询。
- 支持按 PluginId、EventContractId、SubscriptionId 查询。
- 插件卸载前不需要清空共享稳定摘要，但必须清除插件对象引用。

### 12. Testing 包能力

`AtomUI.City.Testing` 应提供：

| 工具 | 职责 |
|---|---|
| `TestEventBus` | 使用确定性运行时的 EventBus。 |
| `EventRecorder` | 记录发布、投递和结果。 |
| `TestEventPublisher` | 驱动事件发布。 |
| `TestEventSubscription` | 控制 handler 完成、失败和取消。 |
| `DeterministicEventDispatcher` | 手动推进 dispatch queue。 |
| `EventBusAssertions` | 顺序、线程目标、错误和生命周期断言。 |
| `PluginEventBusProbe` | 检查插件残留订阅、queue 和 handler。 |

### 13. EventRecorder

EventRecorder 记录稳定事件事实：

- EventId。
- ContractId。
- Channel。
- Publish sequence。
- SubscriptionId。
- DispatchTarget。
- Start/end sequence。
- Result。

默认不记录完整 payload。测试可以显式提供安全 projector。

### 14. 确定性调度

测试不能依赖真实线程和 `Task.Delay` 猜测时序。

Deterministic dispatcher 应支持：

- 查看待执行队列。
- 执行下一项。
- 执行指定 channel。
- 执行指定 partition。
- 执行到 idle。
- 模拟 UI dispatcher。
- 模拟 handler 阻塞。
- 模拟 cancellation。
- 模拟 timeout。

这样可以稳定断言多线程顺序。

### 15. 顺序测试

必须覆盖：

- Serialized channel 接受顺序。
- 同一 subscription 不重入。
- Partition 内顺序。
- Partition 间并行。
- Concurrent 最大并发度。
- UI 和 Background handler 的独立完成顺序。
- 注册/撤销与发布竞争。
- Quiescing 后没有新 handler 开始。

测试应断言框架承诺的顺序，不应断言文档明确不保证的跨订阅完成顺序。

### 16. 背压测试

每种策略都需要：

- Queue 恰好满。
- Queue 超过容量。
- 发布方取消等待。
- EventBus shutdown。
- Plugin deactivation。

断言：

- Wait 确实等待且可取消。
- Reject 返回明确结果。
- DropOldest 记录被丢弃 EventId。
- DropNewest 拒绝当前事件。
- CoalesceLatest 只保留指定 key 的最新事件。

### 17. 错误测试

需要覆盖：

- Handler 创建失败。
- Handler 同步抛出。
- Handler 异步失败。
- Handler timeout。
- Handler dispose 失败。
- Dispatcher 拒绝。
- 多 handler 错误聚合。
- ContinueAndReport。
- StopPublication。
- FailPublisher。
- DisableSubscription。

### 18. Lifecycle 测试

需要覆盖：

- ApplicationScope 停止。
- WindowScope 停止。
- RouteScope 离开。
- ActivationScope 停用。
- EventBus shutdown。
- Subscription dispose 幂等。
- StopAsync drain。
- Shutdown timeout。

所有测试必须断言：

- CancellationToken 已触发。
- Queue 已停止接受新事件。
- Handler 已释放。
- Snapshot 不再包含订阅。

### 19. Plugin 测试

需要覆盖：

- Shared Contract 由 Default ALC 加载。
- Plugin Private Plane 隔离。
- Capability 拒绝。
- Handler Contribution 创建 Lease。
- 插件停用建立 quiescing barrier。
- 旧 snapshot 无法启动新 handler。
- In-flight handler drain。
- Queue 清理。
- Plugin private descriptor 清理。
- Diagnostic buffer 不持有插件对象。
- AssemblyLoadContext 可以被 GC 回收。
- UnloadPending 输出具体 EventBus 残留。

### 20. Contract 测试

需要覆盖：

- Contract Id 唯一。
- Schema fingerprint 稳定。
- Compatible version。
- Breaking version。
- 私有类型对象图检测。
- `object` payload 风险诊断。
- Plugin-to-plugin 共享 Contract。

Generator golden tests 应验证 manifest 和 generated invoker 的确定性。

### 21. 性能测试

Benchmark 与 correctness test 分开。

Benchmark 场景：

- 0/1/10/100 subscriptions。
- 同步完成 ValueTask。
- 异步 handler。
- Serialized/Partitioned/Concurrent。
- Diagnostics off/on。
- Snapshot 频繁更新。
- Plugin 批量撤销。

性能回归不能只看吞吐，还要检查：

- Allocations。
- Tail latency。
- Queue growth。
- Drain time。
- Cancellation responsiveness。

### 22. 测试完成标准

EventBus 开始实现前，测试设计必须能覆盖：

- 公共 API contract。
- 线程和顺序保证。
- 所有背压策略。
- Scope 生命周期。
- Plugin unload。
- AOT generated registration。
- 结构化诊断。
- 发布热路径 benchmark。
