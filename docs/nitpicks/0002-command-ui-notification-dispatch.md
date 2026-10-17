# 0002 Command UI 通知调度边界与异常语义

- 日期：2026-10-16
- 范围：`AtomUI.City.Core.Threading`、`AtomUI.City.Mvvm`、`AtomUI.City.Presentation`，以及下游 `AtomBox.Desktop` 组合根
- 结论：整体分层和依赖方向正确，无需调整架构根基；当前缺口集中在 `City.Mvvm` 命令通知链的运行时一致性、契约完整性和验证闭环。综合 8.0/10（分层边界 9.5，运行时一致性 7.0，API 人体工学 7.5，文档与验证 7.0）。
- 状态：P0 调度与失败隔离合同已由 [ADR 0013](../decisions/0013-ui-observable-notification-dispatch.md) 裁决并进入施工；UI 默认入口、下游重复抽象和通知合批仍按本文优先级后续处理。

## 1. 背景（核实过的事实，带出处）

City 已经形成了清晰的 UI 调度依赖链：

1. `AtomUI.City.Core` 只定义与 UI 框架无关的 `IUiDispatcher` 抽象，提供 `CheckAccess`、同步回调 `InvokeAsync`、返回值回调 `InvokeAsync<T>` 和异步回调 `PostAsync`，没有引用 Avalonia。证据：`src/AtomUI.City.Core/Threading/IUiDispatcher.cs:6-38`。
2. `AtomUI.City.Presentation` 提供 Avalonia 适配实现，在实现内部判断 UI 线程、检查运行时状态、投递回调并传播调度结果。证据：`src/AtomUI.City.Presentation/AvaloniaUiDispatcher.cs:68-205`；该实现通过 DI 注册为 Core 抽象，证据：`src/AtomUI.City.Presentation/AvaloniaUiDispatcherServiceCollectionExtensions.cs:30`。
3. `AtomUI.City.Mvvm` 只依赖 Core 抽象。`CommandFactory.CreateAsync` 的三个重载均可接收 `notificationDispatcher`，证据：`src/AtomUI.City.Mvvm/CommandFactory.cs:60`、`:90`、`:127`。
4. 命令状态通知统一汇入 `DispatchNotification`：`CanExecuteChanged` 以及 `ExecutionTask`、`CanBeCanceled`、`IsCancellationRequested`、`IsRunning` 的属性通知都经过这条路径，证据：`src/AtomUI.City.Mvvm/CommandFactory.cs:638-647`、`:660-703`。
5. 当前文档已明确：只把 UI 可观察通知调度到 UI 线程，不把命令执行体、Activation 或 Deactivation 整体搬到 UI 线程。证据：`docs/modules/mvvm/threading.md:18-23`、`docs/modules/presentation/threading.md:12-14`。
6. 下游 AtomBox 已通过 `DesktopCommandFactory` 强制注入 City 的 `IUiDispatcher`，再传给 `CommandFactory.CreateAsync`。证据（AtomBox 仓库）：`src/AtomBox.Desktop/ViewModels/DesktopCommandFactory.cs:4-20`、`:24-60`。

因此，已经出现过的“异步命令完成后从后台线程发出绑定通知，导致 Avalonia 页面异常或变空白”问题，并不是分层错误，也不是 ViewModel 生命周期设计错误。它属于命令的 UI 可观察通知没有完整遵守 UI 线程边界的问题。

## 2. 好的地方

### 2.1 依赖方向正确

当前依赖方向是：

`Core 抽象 <- Presentation 平台实现 <- 应用组合根注入 -> Mvvm 消费抽象`

`Mvvm` 不直接引用 Avalonia，`Core` 也不知道桌面 UI 框架的存在。这个方向满足依赖倒置，不需要为了修复通知问题而重构模块边界。

### 2.2 只调度 UI 可观察副作用

City 没有把整个异步命令或 ViewModel 生命周期强行切到 UI 线程，只调度绑定系统会观察到的事件和属性通知。这样既保护了 Avalonia 的线程亲和性，也避免网络、磁盘和计算任务占用 UI 线程。现有线程文档的原则是正确的。

### 2.3 已具备失败诊断入口

跨线程投递返回的 `ValueTask` 会被观察，失败会写入 `CommandNotificationFailed` 诊断，而不是形成无人观察的异常。证据：`src/AtomUI.City.Mvvm/CommandFactory.cs:382-396`、`:688-713`。现有测试也覆盖了 Activation 通知经 dispatcher 投递和 dispatcher 失败诊断，证据：`tests/AtomUI.City.Mvvm.Tests/CommandTests.cs:135-180`。

### 2.4 最终状态通知没有复用业务取消令牌

当前通知投递没有携带命令执行令牌，这是正确的。命令即使已经取消，UI 仍然必须收到 `IsRunning = false` 等最终状态；否则界面可能永久停留在“运行中”。

## 3. 欠的地方（带证据）

### 3.1 同一通知的异常语义会随调用线程改变（P0）

`DispatchNotification` 目前自己先调用 `dispatcher.CheckAccess()`。当调用者已经在 UI 线程时，代码直接执行 `notification()`；当调用者不在 UI 线程时，代码改用 `PostAsync`，并通过 `ObserveNotificationAsync` 捕获、记录失败。证据：`src/AtomUI.City.Mvvm/CommandFactory.cs:669-713`。

这导致完全相同的订阅者异常存在两种行为：

- UI 线程路径：异常可能直接穿透当前命令调用栈；
- 后台线程路径：异常通过异步操作被观察并写入诊断。

调用线程不应该改变框架的失败策略。线程判断也不应在调用方和 dispatcher 实现中重复；`AvaloniaUiDispatcher.InvokeAsync` 本身已经处理 inline/dispatch 决策，证据：`src/AtomUI.City.Presentation/AvaloniaUiDispatcher.cs:76-113`。

### 3.2 `IUiDispatcher` 的公开契约过于简略（P0）

接口注释当前只表达“在 UI 线程调用”和“等待期间观察取消”，证据：`src/AtomUI.City.Core/Threading/IUiDispatcher.cs:3-38`。它尚未明确以下工业级语义：

- 回调是否保证至多执行一次；
- 已在 UI 线程时是否允许 inline；
- 返回的 `ValueTask` 在“已入队”还是“回调完成”时完成；
- 取消发生在入队前、等待中和回调开始后的不同语义；
- 回调异常如何传播；
- 多次提交是否承诺 FIFO；
- UI runtime 为 NotReady、Stopped 或 Faulted 时如何失败。

实现虽然已经覆盖了其中一部分，但公开契约没有冻结这些行为，替代实现和调用方就可能作出不同解释。

### 3.3 Headless 可选与 UI 必选只靠人工纪律维持（P1）

`notificationDispatcher = null` 对 headless、测试和非 UI 使用场景是合理的，但 UI 应用一旦漏传，通知就会在完成命令的线程上同步发出。三个异步工厂重载都允许省略 dispatcher，证据：`src/AtomUI.City.Mvvm/CommandFactory.cs:60`、`:90`、`:127`。

AtomBox 通过自己的 `DesktopCommandFactory` 把 dispatcher 变成了构造器必选依赖，这是正确的组合根做法；但 City Presentation 尚未提供同等级的标准入口或运行时防误用诊断。当前是“可以正确使用”，还不是“默认就难以用错”。

### 3.4 下游仍存在第二套同名调度抽象（P1，下游清理项）

AtomBox 仍保留了本地 `AtomBox.Desktop.Services.IUiDispatcher`，它只有 `void Post(Action)`；本地 `AvaloniaUiDispatcher` 也直接调用 `Dispatcher.UIThread.Post`。证据（AtomBox 仓库）：`src/AtomBox.Desktop/Services/IUiDispatcher.cs:3-5`、`src/AtomBox.Desktop/Services/AvaloniaUiDispatcher.cs:5-10`、`src/AtomBox.Desktop/Composition/ServiceCollectionExtensions.cs:28`。

与此同时，真正的命令工厂明确使用了 `AtomUI.City.Core.Threading.IUiDispatcher` 别名，证据（AtomBox 仓库）：`src/AtomBox.Desktop/ViewModels/DesktopCommandFactory.cs:4-20`。两套同名抽象并存容易造成错误注入和错误引用。这不是 City 分层缺陷，但会削弱迁移结果，应在 AtomBox 后续清理。

### 3.5 测试尚未封闭完整状态空间（P1）

现有测试覆盖了“后台来源通知会排队”和“dispatcher 抛错会记诊断”，但还缺少：

- `CheckAccess == true` 时订阅者抛异常；
- 命令开始和完成时所有状态属性都经 dispatcher 交付；
- UI runtime 已停止或故障时通知投递失败；
- 投递失败不会改变命令真实完成、取消或失败结果；
- 不产生 `UnobservedTaskException`；
- 未配置 dispatcher 的 headless 行为保持同步、可预测；
- 基于真实 Avalonia dispatcher 的最小集成测试。

证据：当前相关测试集中在 `tests/AtomUI.City.Mvvm.Tests/CommandTests.cs:135-180`，尚未覆盖上述分支。

### 3.6 通知合并尚未设计，但不是当前正确性阻塞项（P2）

一次状态变化会连续触发多个属性通知。高频命令可能产生较多 dispatcher 工作项。是否做合并应先以性能数据为依据；在契约和正确性稳定前，不应为了减少投递次数引入新的时序复杂度。

## 4. 补强建议

### 4.1 P0：让 dispatcher 成为唯一线程裁决者

`CommandFactory` 不再自行调用 `CheckAccess`，统一调用 `InvokeAsync(notification)`，并统一观察结果：

```csharp
private void DispatchNotification(Action notification)
{
    var dispatcher = _runtime.NotificationDispatcher;
    if (dispatcher is null)
    {
        notification();
        return;
    }

    ValueTask operation;
    try
    {
        operation = dispatcher.InvokeAsync(notification);
    }
    catch (Exception exception)
    {
        _runtime.WriteNotificationFailure(exception);
        return;
    }

    if (!operation.IsCompletedSuccessfully)
    {
        _ = ObserveNotificationAsync(operation);
    }
}
```

这样 inline 与跨线程路径共享同一套调度、运行时检查、异常传播和诊断规则。`CheckAccess` 仍然可以保留为高级调用方能力，但普通通知代码不应据此复制 dispatcher 的内部策略。

### 4.2 P0：冻结 `IUiDispatcher` 契约

在接口 XML 文档和线程设计文档中明确以下不变量：

1. 每次成功接纳的回调至多执行一次，并从 UI 线程开始执行；
2. 已在 UI 线程时允许 inline，但调用方不得依赖一定 inline 或一定异步；
3. 返回任务代表回调执行完成，而不只是成功入队；
4. 回调开始前可取消；回调开始后取消只作为协作信号，不能伪装成强制终止；
5. 回调异常通过返回任务传播；dispatcher 自身不可用时返回可诊断异常；
6. 是否保证 FIFO 必须显式选择并记录，不能留给调用方猜测；
7. NotReady、Stopped、Faulted 三种 runtime 状态必须具有确定行为。

### 4.3 P1：分别优化 headless 与 UI 的默认入口

- 保留 `AtomUI.City.Mvvm.CommandFactory` 的可选 dispatcher，以支持 headless 和测试；
- 在 `AtomUI.City.Presentation` 提供 dispatcher 必选的 UI command factory/扩展入口，或在检测到 UI 绑定生命周期却未配置 dispatcher 时输出结构化诊断；
- 明确禁止将业务执行令牌传给最终状态通知；
- 将 AtomBox 当前 `DesktopCommandFactory` 作为推荐组合根模式的参考实现。

### 4.4 P1：补齐测试矩阵

优先增加同线程订阅者异常、命令开始/结束通知、runtime 停止、失败隔离和无未观察异常测试；随后增加一个真实 Avalonia dispatcher 集成测试。测试必须验证：通知失败可以被看见，但不会回滚或篡改命令的业务结果。

### 4.5 P1：清理下游重复抽象

在 AtomBox 确认本地 dispatcher 没有剩余独立用途后，删除 `AtomBox.Desktop.Services.IUiDispatcher` 及其 Avalonia 实现和注册，统一使用 City Core 抽象与 City Presentation 实现。该动作属于下游迁移收尾，不应反向污染 City 的模块边界。

### 4.6 预期补强后评分

完成 P0 和 P1 后，预期可达到 9.2/10：架构无需重写，主要收益来自线程语义唯一化、公开契约冻结、误用防护和测试状态空间闭合。

## 5. 是否提请 ADR

提请 ADR。

建议标题：`UI 可观察通知调度契约：统一 InvokeAsync、失败隔离与组合根强制注入`。

原因：这不是单一 bug 的局部修补，而是跨越 Core、Mvvm、Presentation 和应用组合根的公共线程契约。ADR 至少应冻结以下决策：

- Mvvm 只调度 UI 可观察通知，不调度整个业务执行体或 ViewModel 生命周期；
- dispatcher 是唯一的 UI 线程裁决者；
- 同线程与跨线程通知采用一致的异常和诊断策略；
- headless 底层 API 可以不配置 dispatcher，UI 上层入口必须显式配置；
- 通知失败不得改写命令业务结果；
- 最终状态通知不受已经取消的业务令牌阻断。
