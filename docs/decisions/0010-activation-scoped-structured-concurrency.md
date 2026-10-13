# 0010 ActivationScope 采用结构化并发并约束 Command 执行所有权

状态：Accepted

日期：2026-10-13

## 背景

`ActivationScope` 当前只能暴露取消令牌并保存 `IDisposable` / `IAsyncDisposable`。调用方仍可在激活期间创建 `_ = Task.Run(...)`、异步 timer callback 或未等待的刷新任务；Scope 结束时无法阻止新任务准入、统计活动任务、等待退出或报告顽固任务。

现有 `CommandFactory.CreateAsync` 还在创建命令时捕获一个固定的可选 Scope。命令通常在 ViewModel 构造阶段创建，而 Scope 只在激活阶段存在；同一个 ViewModel 重新激活时又会得到新的 Scope。因此固定捕获会产生未托管命令或把命令永久绑定到已经结束的 Scope。

AtomBox dogfood 审计进一步暴露了参数化命令、业务 `CanExecute`、按 key 并发、异常链路和执行结果命名等缺口。这些是通用 MVVM 运行时合同，不能由每个应用分别实现。

## 决策

### ActivationScope 监督任务

- `ActivationScope` 成为一次激活会话的结构化异步 owner，并提供 `StartAsync` 和 `StartUniqueAsync`。
- 只有通过上述入口准入的 Task 才属于 Scope；框架不尝试发现任意 `Task.Run`。
- `StartUniqueAsync` 在同一 Scope 内按非空 key 单飞。重复 key 稳定拒绝并诊断；原任务终止后释放 key。v1 不隐式取消或替换原任务。
- Scope 结束时按“关闭新准入 → 请求取消 → 等待已准入任务 drain → 释放异步/同步资源”的顺序执行。
- `DisposeAsync` 是完整路径。ViewModel 停用、激活失败回滚以及 Presentation Entry 清理必须 await 它；同步 `Dispose` 只用于没有异步工作时的兼容路径，不能在 UI 线程用同步等待模拟 `DisposeAsync`。
- 任务异常既保留在返回 Task 上，也写结构化诊断；Scope 清理必须观察全部 Task，不能产生未观察异常。取消不计为普通失败。
- .NET 不能强杀不合作的 Task。超过 drain 期限时只记录包含 Scope、任务名和 key 的诊断并按合同结束等待；该任务的业务代码仍有责任观察 token。

### Command 合同

- 增加泛型同步/异步工厂，直接返回 CommunityToolkit `IRelayCommand<T>` / `IAsyncRelayCommand<T>`。
- 开发者提供同步业务 `CanExecute`；City 把它与激活状态和并发准入状态组合。City 不反射猜测业务依赖。
- 并发策略第一版包括 `Reject`（默认）、`Allow` 和 `RejectByKey`。真正准入在执行入口原子完成，不能只依赖 `CanExecute`。
- `Reject` 使用短原子临界区；`Allow` 维护活动执行注册；`RejectByKey` 使用线程安全 key 注册。任何框架锁不得跨 `await`，也不得在锁内调用用户代码。
- Command 绑定 activation owner，而不是构造期固定 Scope。每次执行时解析当前 Scope，并通过 `StartAsync` 进入监督；inactive 时 UI 不可执行，直接调用稳定拒绝并诊断；重新激活自动绑定新 Scope。
- 固定 Scope 重载只作为显式低级一次性 API 保留，不作为 ViewModel 默认入口。
- awaited `ExecuteAsync` 保留原始异常对象和堆栈并向调用者抛出。`ICommand.Execute` 没有 await caller，其异常必须恰好一次交给应用提供的 `ICommandExceptionHandler`，同时更新执行状态和结构化诊断。取消不进入异常 handler。
- City.Mvvm 不显示对话框，也不实现文件日志、日志目录、保留周期、轮转或磁盘 sink。承载应用决定异常如何展示和持久化。
- City 命令执行快照从 `OperationResult` 重命名为 `OperationExecutionResult`。它不与应用业务结果类型合并。

## 影响

- 新增 `AUC-MVVM-007` 至 `AUC-MVVM-010`。
- `IActivationScope`、`ActivationScope`、`ViewModelBase`、`CommandFactory`、`CommandExecutionState` 和命令结果类型的 public contract 会变化。
- `ViewModelBase.DeactivateAsync` 必须 await Scope 的异步清理，不能继续只调用同步 `Dispose`。
- 旧的“异步命令异常只写 Failed 结果且不抛出”行为被替换；旧测试和迁移说明必须同步更新。
- `OperationResult` 重命名发生在 1.0 Preview 冻结期，仍必须更新 API baseline 和 compatibility 文档。

## 后续约束

- 实现前必须更新 MVVM Feature、API、Lifecycle、Threading、Diagnostics、Testing、Compatibility 和全局开发跟踪。
- `StartAsync` / `StartUniqueAsync` 必须有关闭准入、完成移除、Dispose 竞态、取消、异常、超时和资源回收测试。
- Command 必须有泛型参数、业务 CanExecute、通知、三种并发策略、Scope A/B、inactive rejection、异常恰好一次和取消排除测试。
- 禁止在框架锁内调用用户委托、异常 handler 或诊断 provider。
- 新增异常链路只能使用结构化诊断和应用扩展点；不得在 MVVM 包中引入磁盘日志实现。
