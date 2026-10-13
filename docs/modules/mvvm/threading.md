# AtomUI.City.Mvvm Threading

## 线程模型范围

执行边界：ViewModel programming model。

## 模块线程硬约束

- Interaction 只表达请求，UI 展示由 Presentation handler 完成。
- Command 状态支持取消和异常结果。

## 并发冲突策略

- Interaction 无 handler：返回 `NotHandled`；handler 抛异常返回 `Failed`。
- Command 默认 `Reject`；可显式选择 `Allow` 或 `RejectByKey`。`CanExecute` 不是准入锁，执行入口必须原子校验。
- `RejectByKey` 同 key 单飞、不同 key 可并行；key registry 在成功、取消和失败后都必须释放。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。
- ActivationScope 只监督通过 `StartAsync` / `StartUniqueAsync` 准入的 Task；不能发现任意游离 `Task.Run`。
- CPU 工作可以使用 `Task.Run`，但返回 Task 必须被调用方 await 或交给明确 Scope/应用 scheduler。
- Scope 关闭先拒绝新任务，再取消和 drain；不合作的 Task 只能超时诊断，不能被强杀。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 不在 lock 内调用 task factory、command execute、CanExecute、key selector、异常 handler 或 diagnostics provider。
- 不使用 `.Wait()` / `.Result` / `GetAwaiter().GetResult()` 把 ActivationScope 异步关闭伪装成同步 Dispose。
- 释放顺序从 leaf owner 到 parent owner。
