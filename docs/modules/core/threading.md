# AtomUI.City.Core Threading

## 线程模型范围

执行边界：Host runtime kernel。

## 模块线程硬约束

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 后必须冻结服务注册入口。
- 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。

## 并发冲突策略

- 重复 module id：Build 失败，诊断包含已有 module 和重复 module。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。
- `IUiDispatcher` 是异步 UI work 的唯一线程裁决者：调用方统一调用 `InvokeAsync` / `PostAsync`，不得先用 `CheckAccess()` 复制 inline/排队分支。`CheckAccess()` 只用于必须同步拒绝错误线程的 API 前置条件。
- 成功接纳的 dispatcher 回调最多执行一次；返回任务在回调执行完成后才完成，回调异常通过返回任务传播。实现允许 inline，但调用方不得依赖一定 inline 或一定排队。
- 取消只阻止尚未开始的 work；回调开始后 token 是协作信号。dispatcher/runtime 不可用和回调失败必须是可观察、可诊断的任务失败。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- Host、LifecycleScope 和 ModuleRegistry 只在 lock 内检查状态并发布唯一 lifecycle transaction Task；middleware、module hook、cancellation callback 和 dispose 必须在 lock 外执行。
- ModuleRegistry 的 ConfigureServices、ConfigureContributions 和 Initialize 各自发布唯一阶段事务；同步 ConfigureServices 执行期间的外部并发调用快速失败，完成后的重复调用观察第一次结果，避免同步等待造成线程池饥饿；异步阶段并发调用共享第一次发布的 Task；跨阶段调用必须服从单向状态顺序，不得并行执行同一 module instance 的 hook。
- PreConfigureServices、ConfigureServices 和 PostConfigureServices 是严格同步的 DI 描述阶段，不得执行异步 I/O、UI dispatch 或运行时资源初始化；这些工作进入 StartAsync 驱动的异步初始化 hook，从合同上消除同步 Build 等待用户异步 continuation 的死锁路径。
- Build 失败回滚是同步 API 中唯一允许等待异步清理的边界；Core 必须从默认线程池调度器启动该异步清理，再同步观察结果，不得在调用 Build 的 UI `SynchronizationContext` 上直接启动并等待 `DisposeAsync`。Generic Host、完整 ModuleRegistry 与模块构造中途失败的局部逆序回滚共享一个由 `ShutdownTimeout` 限制的总 deadline，不得为每个资源重新计算完整 timeout。deadline 到期后仍启动其余独立 cleanup 并观察晚到异常，但 Build 不再等待且不得宣称资源已释放。
- ModuleRegistry 的 Shutdown 与 Dispose 使用同一个 terminal transaction Task；Shutdown-first 执行 shutdown + dispose，Dispose-first 只执行 dispose，后续终止调用只能加入先发布的路径。终止事务必须等待已经发布的正向阶段退出，阶段失败不阻止清理。
- ModuleRegistry 正向阶段部分失败后进入 Faulted 且禁止重试；Shutdown/Dispose 始终允许从 Faulted 发起。第一个阶段调用者拥有该共享事务的 context、provider 和 cancellation token。
- ModuleRegistry 生命周期事务的调用所有权属于 Host：public `IModuleRegistry` 仅为模块元数据只读 view；internal `IModuleLifecycleController` 由 Builder 直接交给 Host 且不得注册到 Root DI。只读 view 必须与 controller 实例分离，并且不得实现 disposal 接口，防止业务模块抢占首次事务或提前释放模块。
- LifecycleScope Parent Stop 对快照 child 使用内部 Stop handoff：优先共享 child 的历史 `_stopTask`，允许与并发 Dispose 在 Stop transaction 汇合；不得通过状态预检查或捕获 `ObjectDisposedException` 掩盖竞态，也不得等待完整 `_disposeTask` 形成 parent-child 等待环。
- lifecycle transaction Task 必须先于 Starting、Stopping 或 Disposing 状态对并发调用者可见；外部并发调用共享事务，事务内部对同一 owner 的公共 lifecycle API 重入必须快速失败。
- LifecyclePipeline 的 `next` 是当前 middleware invocation 和 pipeline transaction 共同拥有的受控 continuation：使用原子状态授予一次调用权，middleware 返回时关闭局部调用窗口，transaction 完成时关闭全局窗口。已启动但未被 middleware 等待的下游任务必须由 Pipeline 收拢并观察后再报告合同违规；禁止后台逃逸或在 rollback 后恢复执行。
- IHostDiagnostics 的 Write 与 Complete 使用同一个原子完成边界；完成前取得事务的 Write 必须完整提交，完成后取得事务的 Write 必须抛 ObjectDisposedException，Complete/Dispose 必须幂等。
- 异步调用链使用可嵌套 invocation frame 识别递归；同步 cancellation callback 使用同步 frame 补偿其注册时捕获的 ExecutionContext。
- 释放顺序从 leaf owner 到 parent owner。
