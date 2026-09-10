# AtomUI.City.Presentation Threading

## UI 线程白名单

只有以下工作必须进入 Presentation dispatcher：

- Avalonia View/Window/Control 创建、读取、写入和 DataContext。
- Outlet target content 读取与替换。
- Avalonia 事件订阅/撤销和 VisualTree 操作。
- 可见 Interaction handler 和 Validation/Command visual target。

ViewModel 获取、DI scope、route prepare、leave/confirm guard、activation/deactivation、诊断组装和非 UI 资源清理必须在线程池/调用线程执行，不得为了方便整体搬到 UI 线程。

## 并发纪律

- `CommitAsync` 可从任意线程调用；同一 Outlet FIFO，不同 Outlet 可并行。
- 同一 Window 的模态 Interaction FIFO；不同 Window 可并行；非模态不排队。
- Stop、Close、Dispose 通过先发布 transaction task 再在锁外执行实现合并。
- 任何 `lock` 内禁止调用用户代码、dispatcher、DI factory、插件 callback 或异步方法。
- 公开快照在锁内复制，在锁外枚举和回调。

## 取消

- caller token 在 admission 前取消会拒绝；admission 后只取消等待，不移除已接受的 Outlet commit。
- plan lifecycle token 在 final commit 前可触发 rollback；final commit 后忽略该 token 并完成最小清理。
- Interaction caller/Activation owner token 会取消排队或正在显示的请求，迟到结果不得回写 ViewModel。
- Presentation 不设置隐藏 timeout；Host 提供 shutdown deadline。

## Dispatcher 停止语义

Runtime Stopping 时，外层 Runtime/Window/Outlet admission gate 拒绝新业务工作；dispatcher 仍服务已接受事务和清理。Runtime Stopped/Faulted 后 dispatcher 拒绝工作。该区分避免 shutdown 先关闭 dispatcher 再无法清理 Avalonia 对象。

## 死锁规避

不在 UI 线程同步等待 async，不用 `.Result/.Wait/GetAwaiter().GetResult()` 桥接生命周期；`ViewModelLease` 只支持异步释放。资源按 leaf 到 parent 释放，单项失败继续后续清理。
