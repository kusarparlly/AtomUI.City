# AtomUI.City.EventBus API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

`PublicAPI.Shipped.txt` 中现有签名当前全部为 `Preview-frozen`：门禁阻止未经 review 的签名漂移，但不把它们解释为已经对外发布的 `Stable` API。后续只有在本文件逐项标记 `Stable` 的类型和成员才获得 1.x 稳定兼容承诺。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Publish | IEventBus, IEventPublisher, EventChannel<TEvent>, EventPublishOptions, EventPublishResult | 向默认或命名强类型 channel 发布事件。 | Publish/Post 共享同一精确 contract/channel admission；Publish 返回每个 handler 结果。 |
| Subscribe | IEventSubscriber, EventSubscriberExtensions, IEventHandler<TEvent>, IEventSubscription | 在默认或命名强类型 channel 注册和释放 handler。 | 每条动态订阅必须绑定且只能绑定一个 owner；owner 释放只撤销其拥有的订阅。 |
| Channel Runtime | EventChannelOptions, EventBusRuntimeOptions, EventChannelDescriptor, IEventChannelMonitor | 定义单 runtime 容量、全局 runtime 数量、顺序/并发、partition、背压和指标。 | 每个 runtime 及其总数量均有界；禁止静默丢弃、无界并发和跨 channel 顺序假设。 |
| Contract | IEventContractRegistry, EventContractDescriptor | 事件边界声明。 | 跨插件 contract 必须来自共享程序集。 |
| Diagnostics | EventDiagnosticIds | 现有 EventBus.* 诊断。 | 保持现有字符串兼容；delivery failure/cancellation 诊断必须包含 contractId、eventId 和 subscriptionId context。 |
| Diagnostics | EventBusDiagnosticsOptions | Trace 采样、payload projector 开关和快照边界。 | 概率必须为 0..1；字段数和字段长度必须为正且有上限的值。 |
| Diagnostics | EventBusMetricsSnapshot / IEventBusMonitor | EventBus 聚合指标的线程安全快照。 | 读取不修改运行时；计数单调，active subscription 允许增减。 |
| Diagnostics | IEventPayloadDiagnosticProjector&lt;TEvent&gt; | 显式 opt-in 的 payload 安全投影。 | 输出只能是 Host-owned 字符串快照；投影异常不传播给 publisher。 |

## 关键方法合同

### Plugin contribution contract

`AUC-EVENTBUS-009` 的跨模块 API 由 EventBus 所有：

- `EventBusContributionRequest`：稳定 PluginId、Shared access rules、Private contract descriptors、`EventPluginQuotas`；配额包含单个 lease 唯一终止事务的总 `DrainTimeout`，默认 30 秒。
- `IEventBusContributionController.CreateAsync`：验证并原子创建一个领域 lease；重复 PluginId、未启动 Host、非法 ALC/plane/配额必须在提交前失败。Lease 构造保持 Activating；Controller 先占有 PluginId，再在锁外激活和写诊断，并在返回前复核 Host/字典身份。
- `IEventBusContributionLease`：公开 state、受限 Publisher/Subscriber，并实现 `IAsyncDisposable`；不公开 Host `LifecycleScope` 和原始 `IEventBus`。
- `IPluginEventPublisher` / `IPluginEventSubscriber`：每次操作显式选择 `Shared` 或 `Private` plane；Shared 按 ContractId + channel + direction 授权，Private 按 lease registry 精确类型授权。
- `EventBusContributionState`：`Activating=0`、`Active=1`、`Quiescing=2`、`Draining=3`、`Disposed=4`、`Faulted=5`，枚举值进入兼容性合同。领域 drain 超时进入 `Faulted`，由稳定诊断和异常区分于普通清理失败，不新增可被误解为仍在运行的 `TimedOut` 状态。
- Dispose 与 DisposeAsync 共享唯一终止事务；同步 Dispose 只发布终止，StopAsync/DisposeAsync 等待同一事务。一个失败传播原异常，多个失败使用 `AggregateException`；超过领域总 deadline 抛 `EventPluginDrainTimeoutException`。调用方 cancellation 只取消本次等待，领域 deadline 则终结该 lease 的公开终止事务。
- timeout 后 EventBus 不谎报 `Disposed`，也不强杀线程；它保持 `Faulted`、继续观察迟到的内部清理，并在真实清理完成前保留 PluginId 占用，禁止同名 contribution 重入。
- Lease 的内部锁只允许读取/提交自身状态和计数，不得调用 `IEventBus`、`IEventContractRegistry`、`IHostDiagnostics`、用户 handler、LifecycleScope 或终止回调。Subscribe 使用 pending reservation：锁内预留配额，锁外创建底层 subscription，再锁内提交；如果期间进入 Quiescing，则在锁外异步回滚，且终止事务等待回滚真正完成。

### Generated event catalog public surface

`AUC-EVENTBUS-008` 的 1.0 public surface 固定为：

```csharp
[EventContract(contractId, ownerModuleType)]
[EventChannel(name)]
public sealed class WorkspaceChanged;

[EventHandler(ownerModuleType)]
public sealed class WorkspaceChangedHandler : IEventHandler<WorkspaceChanged>;
```

- `EventContractAttribute` 构造参数为稳定 `contractId` 与 `ownerModuleType`，并提供默认值为 `1` 的 `SchemaVersion`。
- `EventChannelAttribute` 构造参数为 channel name，并提供 `Capacity`、`BackpressurePolicy`、`ExecutionMode`、`MaximumConcurrency` 与 `QueueWaitTimeoutMilliseconds`；timeout 为 `0` 表示不单独限制。
- `EventHandlerAttribute` 构造参数为 `ownerModuleType`，并提供 `ChannelName`、`DispatchPolicy`、`DispatchMode`、`ErrorPolicy`、`HandlerTimeoutMilliseconds` 与 `DisableSubscriptionAfterFailures`；handler timeout 为 `0` 表示关闭。
- constructor 在运行时拒绝 null、空白、首尾空白、控制字符和非法 owner；生成器再次验证 named argument 中的未知 enum 和越界整数。
- generated manifest version 当前固定为 `1`。未知版本必须在 catalog 消费前失败，不能按最新版猜测。
- generated handler descriptor 保存静态元数据和封闭泛型激活 delegate；激活只允许发生在 Host-managed EventBus 的 ApplicationScope 启动事务中。
- contract、channel 和 handler descriptor 都作为 owner contribution 注册；未选中 owner 不产生服务、订阅、队列或类型根。
- 每个 generated handler 的 `IEventHandler<TEvent>` 必须在编译期解析到本次 compilation 或引用程序集中的 generated Shared contract。仅存在 CLR 类型或 `[EventContract]` 外观不构成 catalog 身份；引用程序集还必须同时发布版本匹配、registrar identity 一致的 `GeneratedEventManifestAttribute` 与 Core `GeneratedServiceManifestAttribute`。
- Host Build 必须在创建 Root Provider 前，对实际选中 Module contribution 中的全部 `GeneratedEventHandlerDescriptor.EventType` 与 `EventContractDescriptor.EventType` 执行闭包验证。缺失 contract 必须直接使 Build 失败，不能延迟到 `StartAsync` 激活部分 handler 后才失败。
- frozen Registry 在 subscribe/publish/post 时对未知 event type 的拒绝仍是运行期防御边界；编译期或 Build 验证成功不能移除该防线。

`EventBusMetricsSnapshot` 和 `EventChannelMetricsSnapshot` 是只读公共模型；所有计数和 duration 必须非负，channel snapshot 还必须拒绝 default ContractId、空白 channel、未知 execution mode 和非正 capacity。非法值在构造或 init 边界立即抛标准参数异常，不能制造运行时永远不会产生的状态。

### Subscription public surface

`AUC-EVENTBUS-002` 的目标 public surface 固定为：

```csharp
public interface IEventHandler<TEvent>
{
    ValueTask HandleAsync(EventContext<TEvent> context);
}

public interface IEventSubscriber
{
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);

    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);
}

public static class EventSubscriberExtensions
{
    public static IEventSubscription Subscribe<TEvent>(
        this IEventSubscriber subscriber,
        LifecycleScope owner,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null);

    public static IEventSubscription Subscribe<TEvent>(
        this IEventSubscriber subscriber,
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null);
}

public interface IEventSubscription : IDisposable, IAsyncDisposable
{
    EventSubscriptionId Id { get; }
    EventSubscriptionState State { get; }
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

- `LifecycleScope` 是 Application Plane 动态订阅的 public owner 类型；不引入当前 Core 中不存在的 `ILifecycleScope`。
- `IEventHandler<TEvent>` 保持 invariant。`EventContext<TEvent>` 不能支持安全的 `in TEvent` 合同。
- 核心接口不提供 ownerless overload，也不提供 `Func<TEvent, CancellationToken, ValueTask>` 简化入口。
- 同步 `Action<EventContext<TEvent>>` 只是便利扩展，必须显式传入 owner，并转接到异步核心入口。
- 静态/DI handler 的 owner 由 generated registration 和 EventBus Host controller 绑定为 `ApplicationScope`，不通过 ownerless public API 绕过所有权。
- 插件 handler 由 EventBus 领域 ContributionLease 持有，不允许插件把私有生命周期冒充 Host `LifecycleScope`。
- 不带 `EventChannel<TEvent>` 的 overload 等价于使用 `EventChannel<TEvent>.Default`；带 channel 的发布和订阅只在该强类型 channel 内匹配。

`AUC-EVENTBUS-007` 的 channel public surface 固定为：

```csharp
public readonly record struct EventChannel<TEvent>
{
    public const string DefaultName = "default";
    public static EventChannel<TEvent> Default { get; }
    public EventChannel(string name);
    public string Name { get; }
}

public enum EventChannelExecutionMode
{
    Serialized = 0,
    Partitioned = 1,
    Concurrent = 2
}

public enum EventChannelBackpressurePolicy
{
    Wait = 0,
    Reject = 1,
    DropOldest = 2,
    DropNewest = 3,
    CoalesceLatest = 4
}
```

- Channel identity 是精确 `TEvent` 与区分大小写 `Name` 的组合；不同事件类型或不同名称没有顺序、订阅或容量共享。
- `default(EventChannel<TEvent>)` 是非法值，不能进入发布、订阅或配置入口。
- `Partitioned` 要求每次发布提供非空 `PartitionKey`；其他执行模式拒绝 partition key，防止调用方误以为存在未实现的分区保证。
- `PublishAsync`、`PostAsync` 共享同一 contract/channel admission；它们之间的接受顺序不能因入口不同而被改写。

`EventSubscriptionState` 的 1.0 值固定为：

| Value | Name | Meaning |
| ---: | --- | --- |
| 0 | `Created` | runtime 已分配，尚未提交到 active snapshot。 |
| 1 | `Active` | 可以获取新的 delivery。 |
| 2 | `Quiescing` | 已从新 snapshot 移除并触发取消。 |
| 3 | `Draining` | 等待已获取的 delivery 和 handler 清理。 |
| 4 | `Disposed` | handler、队列、注册和引用已经释放。 |
| 5 | `Faulted` | 已停止接收 delivery，但终止或清理失败。 |

Timeout 或调用方取消只结束当前等待，不形成永久 `StopTimedOut` 状态；唯一终止事务继续执行并最终进入 `Disposed` 或 `Faulted`。

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IEventPublisher.PublishAsync | 发布事件。 | event 实例非 null，options 可选。 | EventPublishResult。 | event 为 null 抛 `ArgumentNullException`；disposed bus 抛 `ObjectDisposedException`；contract 非法、handler 失败、channel 拒绝或 runtime 总量达到上限时抛明确异常；取消按取消合同传播。 | 进入 contract registry 和 subscription snapshot 前必须观察 token；随后与 PostAsync 进入同一 contract/channel admission，并等待该 publication 的 delivery plan 完成。 | 同一默认 Serialized channel 按原子接受顺序执行；并发 publish 使用各自在 API 入口捕获的 subscription snapshot。 |
| IEventPublisher.PostAsync | 接受异步发布请求。 | event 实例非 null，options 可选。 | EventPostResult。 | event 为 null 抛 `ArgumentNullException`；disposed bus 抛 `ObjectDisposedException`；预取消、等待容量时取消、channel full/closed 或 runtime 总量达到上限时返回 rejected result。 | 接受前必须观察 token；`Wait` 可取消；接受后调用方 token 进入 delivery 组合 token，失败由 EventBus 观察。 | 与 PublishAsync 共享同一 admission；只在事件已经由有界 runtime 接管后返回 Accepted，并沿用同一 EventId。 |
| `IEventSubscriber.Subscribe<TEvent>` | 创建一条绑定单一 owner 的动态订阅。 | owner、handler 非 null；owner 必须为 `Running`；options 可为 null 并采用声明的默认值。 | 独立的 `IEventSubscription`。 | owner/handler 为 null 抛 `ArgumentNullException`；owner 非 Running、EventBus 不再接受订阅或 contract 非法时在提交前失败。 | 注册操作不接受 token；提交后由 owner/subscription token 管理生命周期。 | EventBus accepting 状态、owner 状态、owner token registration、状态转换和 snapshot 提交组成一个原子提交；与 owner/EventBus stop 竞争时不得留下 Active 或半注册订阅。 |
| `IEventHandler<TEvent>.HandleAsync` | 处理强类型 delivery。 | context 非 null，并携带当前 delivery 的组合 token。 | `ValueTask`，完成表示本次 handler delivery 完成。 | 同步或异步异常进入订阅错误策略。 | context token 组合 publisher、owner/subscription 和 EventBus shutdown；handler 必须向下游异步操作传播。 | EventBus 不在内部锁内调用；并发度服从 subscription/channel policy。 |
| `EventSubscriberExtensions.Subscribe<TEvent>` | 为同步 handler 提供有 owner 的便利入口。 | subscriber、owner、handler 非 null；options 可为 null。 | 核心异步 Subscribe 返回的 `IEventSubscription`。 | 非法参数使用标准参数异常；其余失败与核心 Subscribe 一致。 | 与核心 Subscribe 一致。 | 只适配 delegate，不建立第二套注册或生命周期逻辑。 |
| IEventBus.Dispose / DisposeAsync | 结束进程内事件总线生命周期。 | 无。 | Dispose 快速发布终止事务；DisposeAsync 等待同一事务。 | 重复调用不抛异常；Dispose 后 publish、post 和 subscribe API 抛 `ObjectDisposedException`；异步清理失败由 DisposeAsync 传播。 | shutdown token 取消 in-flight delivery；未开始 publication 明确失败，不再调用 handler。 | 两个入口共享唯一终止事务；停止 admission、完成 worker、取消/释放 subscriptions 后才完成异步释放。 |
| EventBusServiceCollectionExtensions.AddEventBus | 注册 EventBus 默认服务。 | services 非 null；可选非 null defaultChannelOptions。 | 原 IServiceCollection。 | services/options 为 null 抛 `ArgumentNullException`；非法 capacity/policy 抛 `ArgumentOutOfRangeException`。 | 无。 | 使用 TryAdd 语义注册默认 diagnostics、contract registry、channel options、runtime options 和 EventBus 接口；显式 channel options overload 替换 global fallback，调用方预注册配置和 diagnostics 不被覆盖。 |
| `EventBusModule` | 把 EventBus 服务接入 City Host。 | 由 `UseModule<EventBusModule>`、generated module dependency 或上层 Module 的 `DependsOn` 选择。 | 无状态 `ModuleBase` adapter。 | Host-managed runtime 在 pre-initialization 前收到 publish/post/subscribe 时抛 `InvalidOperationException`；初始化 Scope 非 Running 时启动失败；shutdown 清理失败继续遵循 Host 聚合合同。 | pre-initialization 和 shutdown token 分别来自 Core Host transaction；调用方取消等待不得创建第二个终止事务。 | ConfigureServices 调用 `AddEventBus` 并标记 Host-managed；pre-initialization 把同一个 `ApplicationScope` 交给 internal controller，使依赖模块的同阶段 hook 可用；shutdown 加入 runtime 唯一终止事务。Module 不拥有第二套状态。 |
| internal `IEventBusLifecycleController` | 隔离 Host 生命周期控制权。 | 只能由同程序集 `EventBusModule` 解析和调用。 | `StartAsync(ApplicationScope, token)` / `StopAsync(token)`。 | 不允许 null、非 Running Scope、Dispose 后 Start 或用不同 Scope 重复 Start。 | Stop token 只控制当前等待；已经发布的终止事务继续运行。 | controller 由 `InMemoryEventBus` 的同一实例实现，不注册为公共业务能力，不使用 `IHostedService`。 |
| `EventChannel<TEvent>` | 声明强类型顺序、隔离和容量域。 | name 非空白、无首尾空白和控制字符；default struct 非法。 | 保存稳定 channel name；`Default.Name="default"`。 | 非法 name 抛 `ArgumentException`；default struct 在 API 边界抛 `ArgumentException`。 | 无。 | identity 为精确事件类型 + ordinal channel name；不同 identity 使用不同 runtime。 |
| EventChannelOptions | 配置未被 contract/channel descriptor 覆盖的默认 runtime 边界。 | Capacity > 0；两个 enum 必须为已定义值；MaximumConcurrency > 0；Serialized 必须等于 1；QueueWaitTimeout 为 null 或 `0 < timeout <= Int32.MaxValue ms`。 | immutable init-only options；默认 Capacity=256、Wait、Serialized、MaximumConcurrency=1、无限等待。 | 构造 EventBus、descriptor 或调用配置入口时立即验证非法边界。 | Wait admission 同时观察发布方 token、EventBus shutdown token和可选 timeout。 | 每个精确 event contract/channel 拥有独立 runtime；options 是 fallback，不把不同 identity 合并为全局顺序队列。 |
| `EventBusRuntimeOptions` | 限制单个 EventBus 实例可以创建的精确 channel runtime 总量。 | `MaximumChannelRuntimes` 必须在 1..65536；默认 256。 | immutable init-only options。 | 非法值在 EventBus 构造或 DI 配置时抛 `ArgumentOutOfRangeException`；配置 descriptor 数已经超过上限时构造失败；运行期新 identity 达到上限时 Publish 抛 `EventPublicationRejectedException`，Post 返回 rejected result，并写 `EventRejected`。 | 无。 | 上限检查、已有 runtime 查找和新 runtime 提交在同一 EventBus 锁内原子完成；达到上限不影响已经存在的 runtime。 |
| `ConfigureEventBusRuntime` | 在 DI 中设置 EventBus 全局 runtime 资源预算。 | services/options 非 null 且 options 合法。 | 返回原 IServiceCollection。 | null 使用 `ArgumentNullException`；非法上限使用 `ArgumentOutOfRangeException`。 | 无。 | 使用 Replace 固定当前 Root Provider 的唯一配置，不支持运行期热更新。 |
| `ConfigureEventChannel<TEvent>` | 为默认或命名 channel 覆盖 fallback。 | services/options 非 null；channel 非 default；options 合法。 | 返回原 IServiceCollection 并登记一个 descriptor。 | 同一精确 event type/channel 重复配置在 EventBus 构造时抛 `InvalidOperationException`，不得 last-write-wins。 | 无。 | 配置在 runtime 首次创建前固定；已创建 runtime 不热更新。 |
| `IEventChannelMonitor.GetChannelSnapshots` | 获取已经实例化的 channel runtime 瞬时指标。 | 无。 | 按 ContractId、ChannelName ordinal 排序的只读快照。 | disposed bus 返回终止事务清理后的现有快照；不返回内部可变集合。 | 无。 | Pending/InFlight 在同一 runtime 锁内采样；计数器是并发安全的单调累计值，跨字段不承诺全局原子时刻。 |
| EventPublicationRejectedException | 表达 PublishAsync 未被 channel 接受或排队后被显式替换。 | EventId 非空、ContractId 非 default、reason 非空白。 | 携带 EventId、ContractId 和稳定可诊断原因。 | constructor 非法输入使用标准参数异常。 | queue closed/canceled 的具体规则由发布入口决定。 | 不把拒绝伪装成成功的空 delivery result。 |
| `EventPluginQuotas` | 限制单个插件 EventBus 领域资源和终止等待。 | 四个数量配额为正；MaximumPrivateChannelRuntimes 不超过 65536；DrainTimeout 必须大于零且不超过 `Int32.MaxValue` 毫秒，默认 30 秒。 | immutable init-only 配额快照。 | 非法值在 request 构造阶段抛 `ArgumentOutOfRangeException`，不得等到 lease 激活后失败。 | DrainTimeout 是领域内部 deadline，不等同于 StopAsync 调用方 token。 | 一个 lease 只读取 request 已验证的固定配额，不支持运行期热更新。 |
| `IEventBusContributionLease.StopAsync / DisposeAsync` | 关闭插件 EventBus admission 并确定性等待领域终止。 | StopAsync token 只控制当前调用方等待。 | 正常进入 Disposed；普通清理失败进入 Faulted 并传播；超过总 deadline 进入 Faulted 并抛 timeout exception。 | timeout 不得被包装成成功或普通 cancellation；迟到异常必须被观察和诊断。 | 调用方取消不停止后台唯一事务；领域 DrainTimeout 到达后公开事务以 timeout 失败完成。 | Stop/Dispose/DisposeAsync 共享同一 Task；先在锁内发布 Quiescing 和唯一 Task，再在锁外诊断/清理；等待 pending registrations 完整提交或回滚；timeout 后真实清理结束前保留 PluginId。 |
| `EventPluginDrainTimeoutException` | 稳定表达一个 plugin EventBus 领域未在总 deadline 内 drain。 | pluginId 合法；drainTimeout 为合法正值；active operation/subscription、pending registration 数非负。 | `TimeoutException` 子类，公开 PluginId、DrainTimeout、ActiveOperations、ActiveSubscriptions、PendingRegistrations 快照。 | 非法构造参数使用标准参数异常。 | 只表达领域 deadline，不用于调用方 token cancellation。 | 同一 lease 的并发终止调用观察同一个 exception-bearing 终止 Task。 |
| `IEventContractRegistry.Register` | 在 Host 配置阶段注册 shared event contract descriptor。 | descriptor 非 null 且 plane 必须为 Shared。 | 无。 | descriptor 为 null 抛 `ArgumentNullException`；plugin-private descriptor、重复 id/type 或 registry 已冻结时抛 `InvalidOperationException`。 | 无。 | 单次注册原子提交；重复注册不得静默覆盖。 |
| `IEventContractRegistry.Freeze` | 结束 Shared Registry 配置并发布运行时快照。 | 无。 | 无。 | 已冻结后重复调用作为 no-op；与 Register 竞争时，descriptor 要么完整进入快照，要么 Register 明确失败。 | 无。 | 发布按 ContractId ordinal 排序的 immutable snapshot；冻结后不再发生隐式创建。 |
| `IEventContractRegistry.TryGet` | 按稳定 ContractId 或精确 CLR Type 查询 descriptor。 | ContractId 必须已创建；Type 非 null。 | 找到返回 true 和 descriptor，否则返回 false/null。 | default ContractId 抛 `ArgumentException`；Type 为 null 抛 `ArgumentNullException`。 | 无。 | 冻结后可并发无副作用读取，不进行继承、多态或类型名称匹配。 |
| `IEventContractRegistry.GetOrCreate<TEvent>` | 获取精确类型 descriptor；仅允许未冻结 Registry 为 Default ALC 中的应用内部事件建立临时默认 descriptor。 | 泛型事件类型。 | 已登记 descriptor，或配置期默认 descriptor。 | Plugin/collectible ALC 类型始终抛 `InvalidOperationException`；冻结后未知类型抛 `InvalidOperationException`。 | 无。 | 只按精确 Type 匹配；生产 DI Registry 在 EventBus 可解析前已经冻结，因此运行时路径等价于 required lookup。 |
| `IEventSubscription.Dispose` | 快速撤销订阅。 | 可重复调用。 | 无。 | 不同步传播 in-flight handler 的清理失败；失败由共享终止事务和 diagnostics 观察。 | 触发 subscription cancellation，但不等待 handler。 | 原子执行 Active -> Quiescing、移出新 snapshot 并发布唯一终止事务；不得同步等待异步 handler。 |
| `IEventSubscription.DisposeAsync / StopAsync` | 确定性停止并释放订阅。 | 可重复调用；StopAsync token 只控制当前等待。 | 共享终止事务完成时状态为 Disposed，失败时为 Faulted。 | 一个清理失败传播原异常；多个失败使用 `AggregateException`，同时写 diagnostics 并继续清理其余资源。 | owner/Dispose 触发终止后不能由调用方 token 撤销；已 Disposed 后再次 StopAsync 即使 token 已取消也作为 no-op。 | 并发 Dispose、DisposeAsync、StopAsync 和 owner cancellation 共享唯一终止事务；Quiescing 后不得开始新 handler。 |
| EventPublishResult constructor | 创建发布结果。 | eventId 必须非空，contractId 必须已创建，deliveries 不得为 null 且不得包含 null 项；duration 不得为负。 | EventPublishResult，包含 subscription/delivered/failure/cancellation/timeout/skipped 计数和总 Duration。 | eventId 为空、contractId 为 default、deliveries 包含 null 项或 duration 为负时使用标准参数异常；deliveries 为 null 抛 `ArgumentNullException`。 | 无。 | delivery 列表创建后不可由外部 mutation 改变；三参数兼容构造的 Duration 为零，运行时发布使用实测 duration。 |
| EventDeliveryResult constructor/init | 创建单个订阅 delivery 结果。 | subscriptionId 必须已创建，dispatchPolicy 必须是已定义值，Duration 不得为负，成功结果不得同时取消或携带 error message。 | EventDeliveryResult；Status 稳定归类为 Succeeded/Failed/Canceled/TimedOut/Skipped。 | constructor 或 init mutation 中 subscriptionId 为 default、Duration 为负、成功且取消、成功且 error message 非 null 抛出标准参数异常；未知 dispatchPolicy 抛 `ArgumentOutOfRangeException`。 | Canceled 与 TimedOut 分开统计；尚未开始的 delivery 使用 Skipped。 | result 列表保持 snapshot 顺序；TimedOut handler 实际退出前仍属于 subscription in-flight。 |
| EventPostResult constructor/init | 创建 post 接受或拒绝结果。 | eventId 必须非空，contractId 必须已创建；Accepted 为 true 时 rejection reason 必须为 null，Accepted 为 false 时必须有非空白 rejection reason。 | EventPostResult。 | constructor 或 init mutation 中 eventId 为空、contractId 为 default 或 accepted/rejection reason 状态不一致时抛 `ArgumentException`。 | 无。 | constructor 与 init mutation 均保持同一状态一致性。 |
| EventPublishOptions | 描述 publish 上下文。 | `PublishDepth` >= 0；`CorrelationId`/`CausationId`/`PartitionKey` 为 null 或稳定 id，不得为空白、包含首尾空白或控制字符。 | EventPublishOptions。 | publish depth 小于 0 抛 `ArgumentOutOfRangeException`；三个 id/key 非法抛 `ArgumentException`；partition key 与 channel mode 不匹配时发布入口抛 `InvalidOperationException`。 | 无。 | API 入口读取一次并随 publication request 保存，不回写 options。 |
| EventContext<TEvent> constructor | 创建 handler 执行上下文。 | eventData 非 null，contractId/subscriptionId 必须已创建，eventId 必须非空，correlationId 必须为稳定 id，causationId 为 null 或稳定 id，二者不得为空白、包含首尾空白或控制字符，publishDepth 必须大于等于 0，dispatchPolicy 必须是已定义值。 | EventContext<TEvent>。 | eventData 为 null 抛 `ArgumentNullException`；contractId/subscriptionId 为 default、eventId 为空或 correlationId/causationId 无效抛 `ArgumentException`；publishDepth 小于 0 或未知 dispatchPolicy 抛 `ArgumentOutOfRangeException`。 | 仅保存 token，不主动取消。 | 创建后不可变。 |
| EventSubscriptionOptions | 创建或派生单条订阅的 dispatch/failure policy。 | UiThread dispatcher 非 null；dispatch mode/error policy 必须是已定义值；handler timeout 为 null 或 `0 < timeout <= Int32.MaxValue ms`；disable threshold > 0。 | immutable EventSubscriptionOptions；默认 Serialized/Current handler timeout 为 30s，disable threshold 为 3。 | 非法 dispatcher、enum、timeout 或 threshold 使用标准参数异常；FailPublisher 传播异常；StopPublication 不再开始后续 delivery；ContinueAndReport 聚合并继续；DisableSubscription 达到连续失败阈值后进入唯一 quiesce/termination 事务。 | timeout 触发当前 delivery token；忽略 token 的 handler 仍保留真实 in-flight/drain 所有权。 | `WithErrorPolicy`、`WithHandlerTimeout`、`WithDisableSubscriptionAfterFailures` 不修改原 options；1.0 只存在 subscription-level error policy。 |
| EventBusDispatchOptions | 限制单次 publication 内独立 subscription delivery 的并发。 | MaximumConcurrentDeliveriesPerPublication 必须在 1..1024。 | immutable init-only options；默认 16。 | EventBus 构造或 DI 解析时非法值抛 `ArgumentOutOfRangeException`。 | 不替代 handler、owner 或 shutdown cancellation。 | 与 channel publication concurrency 分层；多个独立 handler 分批启动，不能无界 fan-out。 |
| IEventBackgroundScheduler.RunAsync | 在 EventBus 管理的后台执行目标运行 handler 并观察完成。 | callback 非 null；token 可取消。 | callback 完成对应的 ValueTask。 | callback 失败/取消原样进入 EventBus delivery policy；默认 ThreadPool 实现不传播 publisher ExecutionContext。 | 调度前和 callback 执行期间传递同一个 delivery token。 | EventBus 通过 DI/constructor 注入唯一 scheduler；运行时不得绕过它裸用 Task.Run。 |
| EventContractDescriptor.Shared / PluginPrivate | 创建事件 contract descriptor。 | contractId 必须已创建；Shared assembly 必须与事件类型定义程序集一致并由 Default ALC 加载；PluginPrivate 类型必须来自 collectible non-default ALC。 | EventContractDescriptor。 | contractId 为 default 抛 `ArgumentException`；Shared assembly 为 null 抛 `ArgumentNullException`；assembly/type/plane 与 ALC 身份不匹配时抛 `InvalidOperationException`。 | 无。 | descriptor 创建后不可变；只保存 Type/Assembly，不长期保存 AssemblyLoadContext 实例；隐式 DefaultShared factory 不属于 public API。 |
| `EventContractDescriptor.GeneratedShared<TEvent>` | 供生成 registrar 创建已通过封闭对象图验证的 Shared descriptor。 | 仅由 City generated registrar 调用；schema version/fingerprint 合法。 | 带内部 generated graph proof 的 descriptor。 | 输入边界与 Shared 相同。 | 无。 | 标记为 EditorBrowsable.Never；只有该 proof 的 descriptor 可授予插件 Shared capability，普通 Shared/AddEventContract 仍限 Application Plane。 |
| EventContractId constructor | 创建稳定事件 contract id。 | value 非 null、非空白、不得包含首尾空白或控制字符。 | EventContractId。 | value 为 null 抛 `ArgumentNullException`；空白、包含首尾空白或控制字符抛 `ArgumentException`。 | 无。 | 创建结果不可变。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `EventBusServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContext<TEvent>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractDescriptor` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractId` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractPlane` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventChannel<TEvent>` | 关键 contract | identity、默认名称、校验和 overload 匹配规则变化必须更新本文档和 compatibility。 |
| `EventChannelBackpressurePolicy` | 关键 contract | 稳定值、默认值和每种满载行为变化必须更新本文档和 compatibility。 |
| `EventChannelExecutionMode` | 关键 contract | 稳定值、顺序和并发保证变化必须更新本文档和 compatibility。 |
| `EventChannelDescriptor` | 支持类型 | event type/channel identity、options 验证和重复配置行为变化必须更新本文档和 compatibility。 |
| `EventChannelMetricsSnapshot` / `IEventChannelMonitor` | 支持类型 | 指标字段、排序、采样语义变化必须更新本文档和 compatibility。 |
| `EventBusMetricsSnapshot` / `IEventBusMonitor` | 支持类型 | 聚合指标字段和计数语义进入 1.0 兼容承诺。 |
| `EventBusDiagnosticsOptions` | 支持类型 | Trace 采样与 payload 投影默认值的变化必须显式记录。 |
| `EventBusRuntimeOptions` | 关键 contract | runtime 总量默认值、合法边界和达到上限时的拒绝语义必须更新本文档和 compatibility。 |
| `EventPayloadDiagnosticSnapshot` / `IEventPayloadDiagnosticProjector<TEvent>` | 支持类型 | 只承载有界字符串快照，不得放宽为任意对象。 |
| `EventChannelOptions` | 关键 contract | Capacity、policy 和默认值变化必须更新本文档和 compatibility。 |
| `EventDispatchPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDispatchMode` | 支持类型 | Post/InlineIfAllowed 的稳定值和 UI 重入行为变化必须更新本文档和 compatibility。 |
| `EventDeliveryStatus` | 关键 contract | Succeeded/Failed/Canceled/TimedOut/Skipped 的分类和计数变化必须更新本文档和 compatibility。 |
| `EventBusDispatchOptions` / `IEventBackgroundScheduler` | 关键 contract | 默认并发上限、边界、调度完成和 ExecutionContext 行为变化必须更新本文档和 compatibility。 |
| `EventErrorPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPostResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPublicationRejectedException` | 支持类型 | EventId、ContractId 和拒绝传播方式变化必须更新本文档和 compatibility。 |
| `EventPluginQuotas` | 关键跨模块 contract | 数量配额、DrainTimeout 默认值/合法边界及总 deadline 语义变化必须更新本文档和 compatibility。 |
| `EventPluginDrainTimeoutException` | 关键跨模块 contract | PluginId、deadline 和活动计数快照字段或传播方式变化必须更新本文档和 compatibility。 |
| `EventPublishOptions` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPublishResult` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDeliveryResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionId` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventBus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventPublisher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventSubscriber` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriberExtensions` | 支持类型 | 只提供有 owner 的同步 delegate 适配，不建立独立注册或生命周期路径。 |
| `IEventContractRegistry` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventHandler<TEvent>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventSubscription` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryEventBus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryEventContractRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- `IEventBus.Dispose` 幂等并快速发布终止事务；`DisposeAsync` 等待 channel worker、in-flight delivery 和 active subscriptions 完成同一终止事务。
- `IEventBus.PublishAsync`、`IEventBus.PostAsync` 和 `IEventSubscriber.Subscribe` 在 Dispose 后必须失败并抛 `ObjectDisposedException`。
- `IEventSubscription.Dispose` 幂等、快速建立 Quiescing barrier 并触发唯一终止事务，但不等待 in-flight handler。
- `IEventSubscription.StopAsync` 与 `DisposeAsync` 等待同一终止事务；调用方取消等待后，终止和清理继续进行。
- DI 创建的 Shared Contract Registry 在 EventBus 首次解析前冻结；运行期未知 contract 不得通过 `GetOrCreate` 改写 registry。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
