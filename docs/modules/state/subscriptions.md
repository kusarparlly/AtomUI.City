# AtomUI.City.State Subscriptions 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Subscriptions` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.State` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-STATE-001 | Writable State | WritableStateTests |
| AUC-STATE-002 | Application State | ApplicationStateTests |
| AUC-STATE-003 | Computed State | ComputedStateTests |
| AUC-STATE-004 | State Subscription | StateScopeTests; StateThreadingTests |
| AUC-STATE-005 | State Snapshot | StateSnapshotTests |
| AUC-STATE-006 | Collection State | StateCollectionTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 原子观察合同（AUC-STATE-009）

`OnChange(...)` 继续只表示“从订阅建立后接收变化”，保持兼容。需要同步建立页面或服务投影的消费者必须使用 `Observe(...)`：

```text
state.Observe(handler)
  -> 在 state 锁内捕获当前值与版本
  -> 在同一锁内注册 inactive subscription
  -> 离开 state 锁
  -> 先投递 IsInitial=true 的当前快照
  -> 再按版本顺序投递后续变化
```

- 初始通知恰好一次，`OldValue` 与 `NewValue` 都是注册点的当前值，`Version` 是同一注册点版本。
- `IsInitial=false` 表示正常变化，保留原有 old/new/version 语义。
- 集合观察的初始通知通过不可变 `StateCollectionSnapshot` 表达，后续通知通过 change list 表达。
- inactive/activating barrier 必须保证并发写入只能排在初始通知之后；不得在注册与首次投递之间产生漏失窗口。
- handler、Dispatcher 和用户代码均在 state 锁外执行。
- 观察句柄释放必须幂等；若在首次派发前释放，初始通知和所有排队变化都必须被抑制。
- 有界队列溢出策略可以丢弃普通历史变化并写入 `AUCSTA011`，但不得丢弃尚未投递的初始通知。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.State 订阅与 Reaction 设计

适用范围：状态订阅、State Reaction、生命周期绑定、释放、错误策略和插件卸载

### 1. 定位

状态副作用不命名为 `Effect`。公共 API 优先使用 `IStateSubscription` 或 State Reaction 语义。

```text
state.OnChange(...)
-> returns IStateSubscription
-> registered in StateScope / ActivationScope
```

### 2. 生命周期绑定

所有 subscription 必须绑定 Scope。

常见绑定：

| 创建位置 | 默认绑定 |
|---|---|
| Application service | ApplicationScope |
| Route resolver | RouteScope |
| ViewModel activation | ActivationScope |
| Operation callback | OperationScope |
| Plugin contribution | Plugin contribution lease |

ViewModel 构造函数不得建立长期订阅。长期订阅必须在 Activation 阶段创建，并随 ActivationScope 停用释放。

### 3. 释放规则

规则：

- subscription 释放必须幂等。
- Scope 停止时按反向顺序释放 subscription。
- StateScope 释放时释放所有 state subscriptions。
- Dispatcher、Queued 和 Background 等延迟回调在实际执行前必须重新检查 subscription Dispose 状态。
- 插件 subscription 必须可被插件卸载流程找到并释放。
- 释放失败进入错误聚合，但不能阻断其他释放。

### 4. 错误策略

subscription 抛异常时：

- 进入 State ErrorPolicy。
- 写入 Diagnostics。
- 不杀死 state。
- 不阻止其他订阅者接收通知，除非策略显式要求 fail-fast。
- UI 线程订阅异常不得逃逸到 Dispatcher 形成未处理异常。

### 5. 调度

订阅必须声明或继承调度策略。

| 策略 | 说明 |
|---|---|
| Immediate | 当前线程通知。 |
| Queued | 每个 subscription 通过有界串行 FIFO 队列通知。 |
| Dispatcher | 通过有界串行队列投递到 UI dispatcher。 |
| Background | 通过有界串行 FIFO 后台通知。 |

调度语义见：[threading-and-dispatch.md](threading-and-dispatch.md)。

`Queued()`、`Dispatcher(...)` 和 `Background()` 的默认等待容量为 1024。容量必须大于 0；溢出时丢弃最旧通知并记录 `AUCSTA011`。

`Changed` CLR event 与 `OnChange` 不承担相同角色：`Changed` 在提交线程同步执行，面向低层 .NET 事件互操作；`OnChange` 返回可释放句柄，支持调度策略和 Scope 生命周期。一次变更先调用 `Changed` handlers，再按注册顺序投递 `OnChange` subscriptions。

### 6. 插件卸载

插件停用时必须：

```text
Stop new plugin state subscriptions
-> cancel plugin operations
-> drain or reject pending notifications
-> dispose plugin subscriptions
-> revoke contribution leases
-> release plugin state objects
```

Host 不允许长期持有插件私有 subscription。

### 7. AOT 和 Source Generator

Generator/Analyzer 负责：

- 生成 subscription descriptor。
- 诊断未绑定 Scope 的订阅。
- 诊断插件 subscription 泄漏。
- 诊断 UI 订阅缺少 Dispatcher 策略。

### 8. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| OnChange 通知 | Unit | 状态提交后收到通知。 |
| 相等值不通知 | Unit | 相等提交不触发 handler。 |
| 手动释放 | Unit | Dispose 后不再收到通知。 |
| Scope 自动释放 | Unit | Scope 停止后不再收到通知。 |
| handler 异常 | Unit | 诊断记录，不杀死 state。 |
| UI Dispatcher 策略 | Unit | 通知投递到 fake UI dispatcher。 |
| 插件停用 | Unit | 插件 subscription 被释放。 |
