# AtomUI.City.Presentation Threading

## UI 线程白名单

只有以下工作必须进入 Presentation dispatcher：

- Avalonia View/Window/Control 创建、读取、写入和 DataContext。
- Outlet target content 读取与替换。
- Avalonia 事件订阅/撤销和 VisualTree 操作。
- 可见 Interaction handler 和 Validation/Command visual target。

ViewModel 获取、DI scope、route prepare、leave/confirm guard、activation/deactivation、诊断组装和非 UI 资源清理必须在线程池/调用线程执行，不得为了方便整体搬到 UI 线程。

绑定到 Avalonia View 的 City async command 必须在组合根通过 `CommandFactory.CreateAsync(..., notificationDispatcher: dispatcher)` 接入 Presentation/Core UI dispatcher。这里只调度 `CanExecuteChanged` 与命令状态属性通知；不能把 ViewModel 生命周期整体包装进 dispatcher。

Avalonia dispatcher 是命令通知 inline/排队执行的唯一裁决者。Mvvm 始终调用 `InvokeAsync`；dispatcher 在 UI 线程可以 inline，在其他线程排队，但两条路径都必须以“返回任务代表回调完成、回调异常通过任务传播”的合同结束。Command 层观察失败并写 `AUCMVVM014`，Presentation 不自动弹出最终用户错误 UI。

## 并发纪律

- `CommitAsync` 可从任意线程调用；同一 Outlet FIFO，不同 Outlet 可并行。
- 同一 Window 的模态 Interaction FIFO；不同 Window 可并行；非模态不排队。
- Stop、Close、Dispose 通过先发布 transaction task 再在锁外执行实现合并。
- 任何 `lock` 内禁止调用用户代码、dispatcher、DI factory、插件 callback 或异步方法。
- 公开快照在锁内复制，在锁外枚举和回调。

## 取消

- Outlet commit 的 caller token 与 Window owner token 合成为唯一有效令牌；admission 后取消真实事务，事务先回滚并释放候选资源，再向调用方报告取消。
- final commit 完成所有权移交后不再回滚已提交 entry，后续关闭由 Window/Outlet 生命周期负责。
- Interaction caller/Activation owner token 会取消排队或正在显示的请求，迟到结果不得回写 ViewModel。
- Presentation 不设置隐藏 timeout；Host 提供 shutdown deadline。

## Dispatcher 停止语义

Runtime Stopping 时，外层 Runtime/Window/Outlet admission gate 拒绝新业务工作；dispatcher 仍服务已接受事务和清理。Runtime Stopped/Faulted 后 dispatcher 拒绝工作。该区分避免 shutdown 先关闭 dispatcher 再无法清理 Avalonia 对象。

## 死锁规避

不在 UI 线程同步等待 async，不用 `.Result/.Wait/GetAwaiter().GetResult()` 桥接生命周期；`ViewModelLease` 只支持异步释放。资源按 leaf 到 parent 释放，单项失败继续后续清理。
