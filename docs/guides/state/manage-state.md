# 管理应用状态

State 用于表达可观察、可约束的进程内状态。它不替代数据库；持久化 snapshot 也不等同于业务事务存储。

## 1. 注册基础设施

```csharp
builder.ConfigureServices(services => services.AddState());
```

随后可以注入 `IStateFactory` 创建局部状态，或使用 `IStateRegistry`/`IApplicationStateWriter` 管理带稳定 key 的应用状态。

## 2. Writable state

```csharp
var count = stateFactory.CreateWritable(
    initialValue: 0,
    stateName: "orders.pending");

using var subscription = count.OnChange(change =>
    Console.WriteLine($"{change.OldValue} -> {change.NewValue}"));

count.SetValue(1);
count.Update(current => current + 1);
```

相等值是否产生变化由 comparer 决定。并发更新应使用 `Update` 表达基于当前值的变换，不要在调用方执行“先读再写”。

## 3. Computed state

```csharp
var subtotal = stateFactory.CreateWritable(100m, stateName: "cart.subtotal");
var taxRate = stateFactory.CreateWritable(0.13m, stateName: "cart.tax-rate");

using var total = stateFactory.CreateComputed(
    () => subtotal.Value * (1 + taxRate.Value),
    subtotal,
    taxRate);
```

Computed state 只从依赖派生，不应在计算函数内执行 IO、写入其他 state 或发布事件。依赖变化会使其重新计算；不再使用时必须释放。

## 4. 选择通知策略

```csharp
using var uiSubscription = status.OnChange(
    change => view.ShowStatus(change.NewValue),
    StateSubscriptionOptions.Dispatcher(
        uiDispatcher,
        maxPendingNotifications: 32));
```

| 策略 | 使用场景 |
| --- | --- |
| Immediate | 快速、同步、无 UI 亲和性的逻辑 |
| Dispatcher | 必须回到显式 UI dispatcher 的更新 |
| Background | 可在后台处理的独立工作 |
| Queued | 需要有界排队、避免阻塞写入者的通知 |

异步或排队通知必须设置合理上限。若业务要求观察每次变化，就不能使用允许合并或丢弃中间通知的策略。

## 5. 应用状态和写权限

```csharp
var registry = (ApplicationStateRegistry)
    services.GetRequiredService<IStateRegistry>();
var key = new StateKey<int>("contoso.orders.pending");
registry.Add(StateDefinition.Create(
    key,
    defaultValue: 0,
    lifetime: StateLifetime.Application,
    access: StateAccessPolicy.OwnerWrite,
    snapshotPolicy: StateSnapshotPolicy.Persisted,
    schemaVersion: 1,
    ownerModule: "Orders"));

var ordersWriter = registry.CreateWriter(StateWriteAuthority.Module("Orders"));
ordersWriter.Set(key, 3);
```

读取共享不等于允许任意写入。对跨 Module 状态使用 owner、capability 或其他明确 authority，避免把全局 writer 当成 service locator。

## 6. Scope 和集合

窗口、页面和编辑会话的短期状态放进 `StateScope`。scope 释放后，其状态必须不可再写。集合场景使用 `StateCollection<TKey,TItem>`，以稳定 key 更新条目，不要通过外部引用修改内部 snapshot。

完整的 application、computed、collection、scoped、dispatcher 和压力示例见 [Dogfood State workload](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/State/DogfoodStateWorkload.cs)。
