# 发布和订阅事件

EventBus 用于同一应用进程内的强类型通知。它适合让 Module 解耦，不替代 HTTP、gRPC、数据库 outbox 或跨进程消息代理。

## 1. 接入 EventBus

```csharp
var builder = ApplicationHost.CreateBuilder(args);
builder
    .UseModule<EventBusModule>()
    .UseModule<OrdersModule>();
```

一个 Host 使用一个 Host-managed EventBus runtime。主应用和已接入的功能 Module 共享该 runtime，但 contract、channel 和订阅仍有逻辑 owner。

## 2. 定义稳定事件合同

```csharp
[EventContract("contoso.orders.submitted", typeof(OrdersModule), SchemaVersion = 1)]
[EventChannel(
    "orders",
    Capacity = 128,
    BackpressurePolicy = EventChannelBackpressurePolicy.Wait,
    ExecutionMode = EventChannelExecutionMode.Partitioned,
    MaximumConcurrency = 4)]
public sealed record OrderSubmitted(Guid OrderId, string TenantId);
```

合同名是稳定身份，不应跟随 CLR 类型随意改名。owner Module 决定合同属于谁；容量、背压和执行模式决定过载时的行为。

## 3. 创建有生命周期的订阅

```csharp
var subscriber = services.GetRequiredService<IEventSubscriber>();

await using var subscription = subscriber.Subscribe<OrderSubmitted>(
    windowScope,
    context =>
    {
        RefreshOrder(context.Event.OrderId);
        return ValueTask.CompletedTask;
    });
```

每条动态订阅必须绑定一个 `LifecycleScope`。窗口关闭会撤销该窗口拥有的订阅，不影响 application scope 或其他窗口。仍建议释放返回的 subscription；owner scope 是防泄漏的第二道保证。

不要把 handler 或 subscription 存入比 owner 更长寿的静态对象。

## 4. 选择 Publish 或 Post

```csharp
var publisher = services.GetRequiredService<IEventPublisher>();

var delivered = await publisher.PublishAsync(
    new OrderSubmitted(orderId, tenantId),
    new EventPublishOptions
    {
        CorrelationId = operationId.ToString("N"),
        PartitionKey = tenantId,
    },
    cancellationToken);

var admitted = await publisher.PostAsync(
    new OrderSubmitted(orderId, tenantId),
    cancellationToken: cancellationToken);
```

| 调用 | 返回时已经保证 | 调用方应检查 |
| --- | --- | --- |
| `PublishAsync` | 本事件的 handler 已到达终态 | publication 与逐 handler 结果 |
| `PostAsync` | 事件已完成 admission 或被明确拒绝 | `Accepted` 与 `RejectionReason` |

两者进入同一 channel admission，因此同一 channel 内不会因为 API 不同而绕开队列。不同 channel 之间不存在全局顺序保证。

## 5. 设计背压

| 策略 | 适合 | 风险 |
| --- | --- | --- |
| `Wait` | 订单、审计等不可静默丢失的工作 | 发布方会感受到队列压力 |
| `Reject` | 调用方能够重试或降级 | 必须处理明确拒绝 |
| `DropOldest` / `DropNewest` | 高频、允许采样的遥测或 UI 提示 | 不得用于需要逐条处理的业务事实 |
| `CoalesceLatest` | 只关心最新值的 UI 状态 | 中间变化会被合并 |

容量不是越大越好。先根据允许的内存、峰值和最长处理时间设置边界，再用 `IEventBusMonitor` 与 `IEventChannelMonitor` 观察队列和失败。

## 6. 错误和线程

- 需要 UI 线程的订阅显式使用 UI dispatch 选项，不依赖订阅创建时偶然捕获的 `SynchronizationContext`；
- handler 应等待自己的异步工作，不要在内部使用未观察的 fire-and-forget；
- cancellation 表示本次发布或等待被取消，不代表可以留下半提交的业务状态；
- payload 诊断必须显式 opt-in，禁止把凭据和敏感业务数据写入普通 diagnostics。

生产规模示例见 [Dogfood events](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Contracts/DogfoodEvents.cs) 和 [EventBus workload](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Events/DogfoodEventWorkload.cs)。精确结果模型见 [EventBus API 文档](../../API/eventbus.md)。
