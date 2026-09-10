# AtomUI.City.Presentation Dispatcher

`AvaloniaUiDispatcher` 是 Core `IUiDispatcher` 的 Avalonia 实现，不建立第二套 priority 或 SynchronizationContext。

## 行为

- UI 线程调用 inline，后台调用 marshal 到 `Dispatcher.UIThread`。
- callback 异常原样传播并记录 `AUCPRS004`；调用方取消原样传播。
- Runtime NotReady 返回 `RuntimeNotReady`，Stopped/Faulted 返回 `RuntimeStopping`，dispatcher 平台异常映射 `DispatcherUnavailable`。
- Runtime Stopping 允许已经接受的 UI 工作和资源清理；新业务 admission 由 Runtime/Window/Outlet gate 拒绝。
- `PostAsync` 等待内部 async callback 完成，不只等待投递完成。
- 使用 Avalonia `DispatcherPriority.Default`，1.0 不公开 City priority abstraction。

## 禁止行为

不在 UI 线程 `.Wait/.Result`，不在框架 lock 内调用 dispatcher，不把 route prepare、DI、guard 或 activation 整体搬到 UI。

## 测试

覆盖 inline/background、异步 callback、pre-cancel、callback failure、platform cancellation、NotReady/Stopping/Stopped 以及结构化线程诊断。
