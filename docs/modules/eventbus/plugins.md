# AtomUI.City.EventBus Plugins 合同

## 适用范围

本专题属于 `AtomUI.City.EventBus` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugins` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

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
| AUC-EVENTBUS-002 | Subscription Ownership & Lifecycle | EventSubscriptionTests |
| AUC-EVENTBUS-003 | Contract Identity & Registry | EventContractRegistryTests |
| AUC-EVENTBUS-005 | Diagnostics & Observability | EventDiagnosticsTests |
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

## AtomUI.City.EventBus Plugin Integration 设计

适用范围：插件事件平面、事件 Contract 校验、Capability、Handler Contribution、插件启用停用、事件 drain、AssemblyLoadContext 卸载和错误隔离设计。

### 1. 定位

EventBus 是插件与 Host、静态模块和其他插件解耦通信的重要基础设施，但它也是最容易阻止插件卸载的系统之一。

EventBus 可能长期持有：

- 插件 handler instance。
- 插件 delegate 和 closure。
- 插件私有事件 `Type`。
- 泛型 invoker。
- 排队中的插件事件。
- Dispatcher callback。
- Subscription descriptor。

因此插件 EventBus 集成必须建立在 ContributionLease、Lifecycle、Threading 和 Host Contract Registry 之上。

### 2. 两个事件平面

插件 EventBus 分为两个逻辑平面。

#### Shared Contract Plane

用于：

- Host 向插件发布公共系统事件。
- 插件向 Host 发布授权事件。
- 多个插件通过稳定 Contract 通信。

规则：

- Contract 由 Host 注册。
- Contract Assembly 由 Default AssemblyLoadContext 加载。
- 插件必须声明 publish/subscribe capability。
- Channel 由 Host 管理。
- 订阅进入 Host EventBus registry。

#### Plugin Private Plane

用于：

- 插件内部模块之间通信。
- 插件私有事件。
- 不向 Host 暴露的实现通知。

规则：

- Contract 可以来自插件私有 Assembly。
- 只对当前插件可见。
- Registry、channel、queue 和 dispatch plan 由插件 runtime context 持有。
- 随插件停用或卸载整体释放。
- 不得把私有事件桥接进 Shared Plane。

### 3. Shared Plane Contract

共享事件的事件类型及完整对象图必须来自 Host 注册的共享 Contract Assembly，并由 Default AssemblyLoadContext 唯一加载。

详细规则见：[Event Contracts 设计](contracts.md)。

PluginSystem 在插件验证阶段检查：

- 插件依赖的 Contract Assembly。
- Contract version range。
- EventContractId。
- Publish capability。
- Subscribe capability。
- Channel capability。
- Host 当前是否启用对应 EventBus feature。

任何不匹配都应在插件执行代码前失败。

### 4. Plugin Manifest

插件 manifest 应声明：

```text
Event contracts referenced
Shared events published
Shared events subscribed
Private event manifest
Required channels
Requested dispatch policies
Requested queue capacities
Requested capabilities
```

Source Generator 生成 event manifest。PluginSystem 不通过运行时扫描插件程序集发现 handler。

### 5. Capability 模型

推荐 capability：

| Capability | 说明 |
|---|---|
| `eventbus.publish` | 允许发布指定共享事件。 |
| `eventbus.subscribe` | 允许订阅指定共享事件。 |
| `eventbus.private` | 允许创建插件私有事件平面。 |
| `eventbus.background` | 允许插件 handler 使用后台调度。 |
| `eventbus.channel.create` | 允许申请额外私有 channel。 |

Capability 应进一步绑定：

- EventContractId。
- Channel。
- 最大 queue capacity。
- 最大并发度。
- 允许的 DispatchTarget。

不能只授予一个无限制的全局 EventBus capability。

### 6. 插件获得的接口

Host 根据 capability 给插件暴露受限 contract：

- `IEventPublisher`。
- capability 受控的 EventBus handler contribution contract。
- Plugin Private EventBus。

插件不直接获得 Application Plane `IEventSubscriber`，也不把插件私有生命周期包装成 Host `LifecycleScope`。Shared Plane handler 和 Private Plane subscription 都由 EventBus 领域 ContributionLease 持有，并由 PluginSystem 在停用/卸载阶段显式等待撤销和 drain。

插件不应获得：

- Host `IEventSubscriptionRegistry` 可写接口。
- 其他插件私有 EventBus。
- Host 全部事件 contract 管理接口。
- 任意 channel 创建权限。
- EventBus worker 和 queue 内部对象。

### 7. Handler Contribution

插件 handler 通过 Contribution 注册：

```text
Plugin module
-> EventHandlerContributionRequest
-> Host capability validation
-> EventBus contract validation
-> Create subscription runtime
-> Add to publication snapshot
-> Return ContributionLease
```

Request 至少包含：

- PluginId。
- ModuleId。
- EventContractId。
- Channel。
- Handler descriptor。
- DispatchPolicy。
- ConcurrencyPolicy。
- Queue/backpressure request。
- Service context。
- Diagnostic metadata。

EventBus 可以降低插件请求的 capacity 或 concurrency，但不能静默扩大权限。

### 8. 插件 Handler 创建

插件 handler 必须从插件 ServiceProvider 创建。

规则：

- 不 fallback 到 Host Root ServiceProvider。
- Host contract 通过显式共享 service accessor 提供。
- Handler instance 不能进入 Host singleton。
- Transient handler 在 delivery 完成后释放。
- Scoped handler 随插件或更小 Scope 释放。
- Plugin ServiceProvider 停止后不能创建新 handler。

### 9. 插件激活

插件 EventBus 激活流程：

```text
Verify event manifest
-> Resolve shared contract assemblies
-> Validate capabilities
-> Create Plugin Private Plane
-> Register private descriptors
-> Apply shared handler contributions
-> Start private channel workers
-> Mark event capability Active
```

只有完成激活后：

- 插件才能发布 Shared Plane 事件。
- Host 才能向插件 handler 投递事件。
- 插件私有 EventBus 才能接受事件。

### 10. 激活失败回滚

激活中任一步失败：

```text
Reject plugin publications
-> Remove created shared subscriptions
-> Revoke created leases
-> Stop private channel workers
-> Clear private descriptors
-> Dispose created handlers and queues
-> Report activation failure
```

不能留下半激活插件订阅。

### 11. 插件停用屏障

插件停用必须先建立 EventBus quiescing barrier：

```text
Mark plugin event capability Quiescing
-> Reject new plugin publications
-> Reject new plugin subscriptions
-> Remove plugin handlers from new publication snapshots
-> Cancel queued plugin deliveries
-> Drain in-flight handlers
-> Revoke subscription leases
-> Stop private channel workers
-> Clear plugin dispatch plans
```

屏障建立后：

- Host 新发布不会开始插件 handler。
- 插件不能再向 Shared Plane 发布。
- 插件不能创建新订阅。
- 已捕获旧快照的 delivery 在执行前必须检查插件状态。

### 12. Queue 处理策略

插件停用时，队列策略分为：

| 类型 | 默认处理 |
|---|---|
| 发往插件 handler、尚未开始 | 取消。 |
| 插件私有 channel 中尚未开始 | 取消并清空。 |
| 插件发布到 Host、已被 Shared Plane 接受 | 由共享 channel 正常处理。 |
| 插件发布请求尚未被接受 | 拒绝。 |
| 正在执行的插件 handler | 触发取消并等待 drain。 |

一旦共享事件已经被 Shared Plane 接受，事件 payload 必须完全由 Shared Contract 组成，因此它可以在插件停用后继续由 Host 或其他插件处理。

### 13. In-flight Handler

正在执行的插件 handler：

- 收到 plugin deactivation token。
- 必须停止启动新的插件 Operation。
- 可以完成必要的短清理。
- 在 drain timeout 内结束。

EventBus 维护：

- PluginId -> active subscription 数量。
- PluginId -> queued delivery 数量。
- PluginId -> in-flight handler 数量。
- PluginId -> handler task/operation diagnostics。

无法 drain 时，插件停用结果必须明确失败或降级。

EventBus contribution 使用 `EventPluginQuotas.DrainTimeout` 作为整个终止事务的单一总 deadline，默认 30 秒。该 deadline 覆盖 subscription drain、已接受 operation、plugin `LifecycleScope` 与 private runtime 清理，不能为每个步骤重新计时。超过 deadline 时：

- lease 进入 `Faulted`，并以 `EventPluginDrainTimeoutException` 向 PluginSystem 报告；
- 写入 `EventBus.EventPluginDrainTimedOut`，最小 context 包含 `pluginId`、`drainTimeoutMilliseconds`、`activeOperations`、`activeSubscriptions` 和 `pendingRegistrations`；
- EventBus 不强制终止线程，也不声称插件已经安全卸载；
- 迟到清理继续被观察，PluginId 在真实清理完成前仍被占用，避免旧、新插件实例并存；
- PluginSystem 决定拒绝卸载、隔离、重试或上报故障，EventBus 不越权决定整插件策略。

ContributionLease 不允许在内部状态锁中调用 EventBus、Contract Registry、Diagnostics sink、用户代码或其他可重入组件。插件 Subscribe 采用三阶段提交：

```text
锁外验证 contract/capability
-> 锁内复核 Active、预留配额并登记 pending registration
-> 锁外创建底层 subscription
-> 锁内提交；若已 Quiescing 则拒绝提交
-> 锁外完成失败回滚
```

Stop 在锁内发布 Quiescing 和唯一终止 Task 后立即退出锁，再写诊断。终止 Task 必须先等待所有 pending registration 成为“完整提交”或“底层资源已完成回滚”，然后才能认定 subscription snapshot 完整。诊断 sink 同步重入 Stop/Subscribe 时只能观察完整状态，不能递归发布第二个终止事务。

### 14. 卸载流程

插件卸载前 EventBus 必须满足：

```text
No active shared subscription
No private subscription
No queued private event
No in-flight plugin handler
No plugin dispatch callback
No plugin-private descriptor in Host cache
No plugin-private Type in static generic cache
```

然后：

```text
Dispose plugin event runtime
-> Clear private registries
-> Clear private queues
-> Release generated invoker references
-> Release handler factory references
-> Verify EventBus plugin diagnostics
-> Allow PluginSystem to unload ALC
```

### 15. UnloadPending

EventBus 可能导致插件进入 `UnloadPending`：

- Subscription 未撤销。
- Handler 仍执行。
- Queue 中仍有插件私有 payload。
- Host snapshot 仍引用插件 descriptor。
- Delegate closure 持有插件服务。
- Static generic cache 使用插件私有事件类型。
- Dispatcher callback 持有插件 handler。
- Diagnostic buffer 保存插件异常对象或 payload。

EventBus 必须输出具体残留项，不能只报告“插件无法卸载”。

### 16. 诊断对象安全

插件异常和事件 payload 不能被 Host 诊断系统永久强引用。

规则：

- 结构化诊断保存稳定字符串、Id 和摘要。
- 插件私有 Exception 只在处理期间使用。
- 长期诊断缓冲不保存插件私有 Exception 实例。
- 默认不保存完整插件事件 payload。
- 需要 payload snapshot 时转换为 Host-owned stable representation。

### 17. 插件更新

插件更新流程：

```text
Deactivate old plugin
-> EventBus quiesce and drain
-> Revoke event contributions
-> Unload old plugin
-> Validate new event manifest
-> Load and activate new plugin
```

如果共享 Contract 版本发生变化：

- 必须由 Host 先支持新版本。
- 插件不能在更新时替换 Host Shared Contract。
- Contract 不兼容时拒绝新插件版本。

### 18. Plugin-to-Plugin 通信

插件之间只通过 Shared Contract Plane 通信。

禁止：

- Plugin A 引用 Plugin B 实现程序集。
- Plugin A 订阅 Plugin B 私有事件。
- Host 转发带 Plugin B 私有对象的 payload。
- 通过 `object` 绕过 Contract 校验。

插件不需要感知对方是否安装。没有订阅者时，发布结果可以成功但 handler count 为零，是否视为业务问题由发布方决定。

### 19. 插件错误隔离

插件 handler 失败默认：

- 标记当前 delivery failed。
- 继续其他独立 handler。
- 记录 PluginId、SubscriptionId、EventId。
- 不让插件私有异常直接穿透 Host 公共 API。

连续失败可以：

- 禁用当前 subscription。
- 降低插件 event capability。
- 请求 PluginSystem 停用插件。

具体升级策略由 Host 配置。

### 20. 性能和配额

插件不能消耗无限 EventBus 资源。

Host 应限制：

- 插件订阅数量。
- 私有 channel 数量。
- Queue capacity。
- 最大并发 handler 数。
- 单事件 payload 建议大小。
- 单 handler timeout。
- 诊断记录速率。

配额超限：

- 拒绝 Contribution。
- 拒绝发布。
- 触发 throttling。
- 记录插件资源诊断。

### 21. AOT 和 Dynamic Plugin Mode

Static Plugin Mode：

- Event manifest 在应用构建期合并。
- Handler invoker 静态生成。
- 最适合 AOT。

Dynamic Plugin Mode：

- 插件包携带预生成 event manifest 和 invoker。
- Host 运行时验证 manifest。
- 不扫描任意类型发现 handler。
- 不承诺完整 NativeAOT 动态加载。

### 22. 测试要求

必须测试：

- 插件 capability 校验。
- 插件共享 handler contribution。
- 插件私有 EventBus 隔离。
- 插件不能发布私有类型到 Shared Plane。
- 激活失败完整回滚。
- 停用后不接受新投递。
- 旧快照不能绕过 quiescing barrier。
- Queue 取消和 in-flight drain。
- Stop timeout。
- SubscriptionLease 撤销。
- 插件更新前 EventBus 已清空旧引用。
- EventBus 不阻止 AssemblyLoadContext 回收。
- UnloadPending 诊断包含具体残留。

### 23. 第一版明确决策

- Shared Plane 由 Host 管理。
- Private Plane 随插件释放。
- 插件 handler 必须通过 Contribution 注册。
- 插件 EventBus 权限采用 capability。
- 插件停用必须 quiesce、cancel、drain。
- Host 不长期保存插件 payload、Exception 或私有 Type。
- 插件之间不能直接共享私有事件。
