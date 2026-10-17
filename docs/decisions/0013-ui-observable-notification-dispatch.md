# 0013 UI 可观察通知的统一调度与失败隔离

状态：Accepted

日期：2026-10-17

## 背景

绑定到桌面 UI 的异步命令会发布 `CanExecuteChanged` 和运行状态 `PropertyChanged`。旧实现由 Mvvm 先调用 `IUiDispatcher.CheckAccess()`：已经位于 UI 线程时直接调用订阅者，后台线程则通过 dispatcher 投递并观察返回任务。相同的订阅者异常因此可能在 UI 线程穿透命令调用栈，在后台线程却被隔离并写入诊断；命令完成线程成为了未声明的失败策略开关。

## 决策

- 配置 `notificationDispatcher` 后，Mvvm 不再调用 `CheckAccess()`，所有 UI 可观察通知统一调用 `IUiDispatcher.InvokeAsync`。
- `IUiDispatcher` 是 inline 或排队执行的唯一裁决者。调用方不得根据 `CheckAccess()` 复制调度分支；`CheckAccess()` 仅用于必须同步拒绝错误线程的 API 前置条件，例如窗口注册。
- `InvokeAsync` 成功接纳的回调最多执行一次，并从 UI 线程开始；实现可以 inline，也可以排队，调用方不得依赖具体方式。
- `InvokeAsync` 返回的 `ValueTask` 表示回调已经执行完成，而不只是成功入队。回调异常和 dispatcher/runtime 不可用通过该任务传播。
- Command 必须观察通知任务。同步抛出、inline 失败和排队后失败统一记录 `AUCMVVM014`，不得形成未观察任务异常，也不得改变命令的 Completed、Failed 或 Canceled 业务结果。
- 命令最终状态通知使用 `CancellationToken.None`，不能被已经取消的业务 operation token 阻断。
- City 不自动向最终用户展示通知异常。应用通过 `IHostDiagnostics` 观察故障，并自行决定日志、遥测或 UI 呈现。
- 未配置 dispatcher 的 Headless/测试入口保持同步通知语义；UI 组合根必须为绑定到 View 的异步命令提供 Presentation dispatcher。

## 影响

- Core 冻结 `IUiDispatcher` 的执行次数、线程、完成、取消和异常合同，但继续保持 UI 框架无关。
- Mvvm 的命令通知不再自行判断 UI 线程；同线程和跨线程采用同一失败隔离策略。
- Presentation 的 Avalonia dispatcher 负责真正的 inline/排队决策和 runtime 状态检查。
- `RegisterWindow` 等同步 API 仍可使用 `CheckAccess()` 直接拒绝错误线程，这不属于异步通知路径。

## 后续约束

- 测试必须同时覆盖 inline 和排队回调失败，并证明命令结果不受通知异常影响。
- dispatcher fake 必须遵守“返回任务代表回调完成”的合同，不能在只入队后立即返回成功。
- 通知合批、全局 FIFO 或优先级策略不在本 ADR 中承诺；若引入，必须基于性能数据另行裁决。
