# EventBus API

`AtomUI.City.EventBus` 提供进程内、强类型、可取消且有界的事件分发。它不是跨进程消息代理。

完整合同：[EventBus API Contracts](../modules/eventbus/api-contracts.md)

## 主要 API

| API | 用途 |
| --- | --- |
| `EventBusModule` | 将唯一 Host-managed EventBus runtime 接入 City Host |
| `IEventPublisher` | `PublishAsync` 和 `PostAsync` |
| `IEventSubscriber` | 创建绑定 owner 的订阅 |
| `IEventSubscription` | 释放单条订阅 |
| `EventChannel<TEvent>` | 默认或命名强类型 channel |
| `EventPublishOptions` | dispatch、错误和取消策略 |
| `EventPublishResult` | 每个 handler 的可观测交付结果 |
| `EventChannelOptions` | 容量、背压、执行模式和并发上限 |
| `IEventBusMonitor` | 聚合指标快照 |

## Host 集成示例

```csharp
using AtomUI.City.Core.Hosting;
using AtomUI.City.EventBus;
using Microsoft.Extensions.DependencyInjection;

var builder = ApplicationHost.CreateBuilder(args);
builder.UseModule<EventBusModule>();
builder.ConfigureServices(services =>
    services.AddEventContract<OrderSaved>(
        new EventContractId("contoso.orders.saved.v1")));

await using var host = builder.Build();
await host.StartAsync();

var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
var publisher = host.Services.GetRequiredService<IEventPublisher>();
var applicationScope = host.ApplicationScope
    ?? throw new InvalidOperationException("The Host did not create its application scope.");

await using var subscription = subscriber.Subscribe<OrderSaved>(
    applicationScope,
    context =>
    {
        Console.WriteLine(context.Event.OrderId);
        return ValueTask.CompletedTask;
    });

var result = await publisher.PublishAsync(new OrderSaved(Guid.NewGuid()));
```

## Publish 与 Post

| 方法 | 完成语义 | 适用场景 |
| --- | --- | --- |
| `PublishAsync` | 等待当前事件完成分发并返回 handler 结果 | 调用方需要知道本次交付结果 |
| `PostAsync` | 完成统一 admission 后返回接收结果，后续由 worker 处理 | 调用方只需要可靠投递到有界队列 |

两者进入同一 channel admission，不要假设不同 channel 之间存在全局顺序。背压策略决定队列满时等待、拒绝或采取其他明确行为；不得静默丢弃。

## 订阅所有权

动态订阅必须绑定且只能绑定一个 `LifecycleScope` owner。释放某个窗口 scope 只撤销该窗口拥有的订阅，不影响其他窗口或 application scope。

```csharp
await using var subscription = subscriber.Subscribe<WorkspaceChanged>(
    windowScope,
    HandleWorkspaceChangedAsync);
```

即使保留了 `subscription` 引用，owner 终止后也不能继续接收事件。

## Generated catalog

应用可以用 `[EventContract]`、`[EventChannel]` 和 `[EventHandler]` 描述事件与 handler。生成器创建静态清单；Host 只激活已选择 Module 拥有的 contract 和 handler。缺失 contract、manifest 版本不匹配或 owner 未选中会在 Build/启动边界失败，而不是依赖运行时扫描猜测。

## 错误与观测

错误策略包括继续并报告、停止本次发布和使 publisher 失败等显式选择。监控 API 提供发布、交付、失败、队列和活跃订阅计数；payload 投影必须显式 opt-in，并且投影异常不能反向破坏 publisher。
