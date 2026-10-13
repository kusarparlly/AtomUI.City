# AtomUI.City.Mvvm Structured Concurrency And Commands V2

状态：Implemented / Release Validation Pending

关联决策：[ADR 0010](../../decisions/0010-activation-scoped-structured-concurrency.md)

## 功能范围

本文定义以下已实现 Feature 的行为合同；全解决方案发布门禁通过前仍保持 Release Validation Pending：

| Feature ID | 名称 | 解决的问题 |
| --- | --- | --- |
| `AUC-MVVM-007` | Activation-scoped Structured Tasks | 激活域任务准入、监控、唯一 worker、取消和 drain。 |
| `AUC-MVVM-008` | Typed Commands and CanExecute | 泛型参数、业务谓词、通知和执行结果命名。 |
| `AUC-MVVM-009` | Command Concurrency and Activation Ownership | 原子并发策略、按 key 单飞、动态 Scope A/B。 |
| `AUC-MVVM-010` | Command Exception Delivery | awaited 异常传播和 `ICommand.Execute` 应用 handler。 |

## AUC-MVVM-007 Activation-scoped Structured Tasks

### Public contract

- `IActivationScope` / `ActivationScope` 增加 `StartAsync` 和 `StartUniqueAsync`。
- 准入参数至少包含稳定任务名；unique 入口还包含非空、非空白 key；委托统一接收 Scope 链接后的 `CancellationToken`。
- 返回 Task 就是该次工作的 completion：成功完成、合作取消或保留原异常失败。
- Scope 暴露只读活动任务数或等价 snapshot，不能暴露内部可变集合。
- `IActivationScope.DisposeAsync` 是异步任务存在时的规范关闭入口。

### 准入和状态

```text
Open
  -> admit StartAsync / unique key
  -> Running(0..n)
  -> Closing (拒绝新准入)
  -> Cancelling
  -> Draining
  -> Disposing resources
  -> Disposed 或 DisposedWithFailures
```

- Task 必须在调用用户委托前完成注册，避免“任务已经完成但 registry 尚未可见”的竞态。
- `StartUniqueAsync` 的 key 从成功准入持续占用到 Task 进入终态；成功、取消和异常都释放 key。
- 重复 key、Closing/Disposed 后准入和空白任务名稳定失败，且不调用用户委托。
- 外部 token 与 Scope token 链接；任一取消都会到达工作委托，但不会取消其他同 Scope 工作。
- Scope 关闭会取消全部活动工作并等待 registry 变空；用户委托和 continuation 不在框架锁内执行。
- drain timeout 是诊断和等待边界，不代表 CLR Task 被终止。

### 释放

- 释放顺序固定为：关闭准入、取消任务、等待任务、逆序释放 `IAsyncDisposable`、逆序释放 `IDisposable`。
- 所有阶段 best-effort 执行；非取消失败聚合，不能因一个失败跳过其他资源。
- 同步 `Dispose` 不对异步 Task 或 `IAsyncDisposable` 做 `.Wait()` / `.GetResult()`；检测到异步工作时必须稳定拒绝并指引调用 `DisposeAsync`，避免 UI 线程死锁。
- 重复 `DisposeAsync` 共享或等待同一关闭事务并保持幂等。

## AUC-MVVM-008 Typed Commands and CanExecute

### Public contract

- `CommandFactory.Create<T>` 接受 `Action<T?>`、可选 `Predicate<T?>`、state 和 diagnostics，返回 `IRelayCommand<T>`。
- `CommandFactory.CreateAsync<T>` 接受 `Func<T?, CancellationToken, Task>`、可选 `Predicate<T?>`、并发/activation/exception 选项、state 和 diagnostics，返回 `IAsyncRelayCommand<T>`。
- 非泛型重载继续作为 `T` 不存在时的便利入口，其运行语义必须与泛型版本一致。
- 参数类型转换遵循 CommunityToolkit command 合同；City 不做字符串到业务类型的反射转换。
- 业务 `CanExecute` 是同步纯判断。需要异步授权时，应用先更新可观察状态，再调用 `NotifyCanExecuteChanged`。
- City 负责 activation、执行计数、取消和并发变化产生的通知；开发者负责业务属性依赖，可使用 `[NotifyCanExecuteChangedFor]`。

### 结果命名

- 框架运行快照命名为 `OperationExecutionResult`，包含 operation id、status、elapsed 和 exception。
- 它只描述执行运行时，不表示领域业务成功；应用可继续拥有自己的 `OperationResult<T>`。
- 旧 `OperationResult` 在 Preview 阶段直接重命名，不提供同名兼容壳，避免两个公开类型长期并存。

## AUC-MVVM-009 Command Concurrency and Activation Ownership

### 并发策略

| 策略 | 准入规则 | `CanExecute` 规则 |
| --- | --- | --- |
| `Reject` | 整个 Command 同时只准入一个执行。 | 运行期间为 false。 |
| `Allow` | 每次调用独立准入并独立取消/结果。 | 只由业务 predicate 和 activation 决定。 |
| `RejectByKey` | 同一 key 单飞，不同 key 可并行。 | 对传入参数计算 key 后判断；最终准入仍在 execute 入口原子完成。 |

- `Reject` 是默认策略。
- key selector 由开发者提供；null/空白或 selector 异常按参数/执行失败合同处理，不能隐式退化为全局 key。
- `CancelPrevious` 和队列语义不在本 Feature 范围内。
- `CommandExecutionState` 必须能表达 active count、最近完成结果和 rejected count；不能再假设最多一个 running operation。

### Activation owner

- 新增只读 activation owner contract，由 `ViewModelBase` 实现，至少暴露当前 Scope 和 active/disposed 状态。
- ViewModel 默认 Command 创建入口接收 owner，不接收构造时固定 Scope。
- 每次执行按一个原子快照解析当前 Scope，先完成并发和 Scope 准入，再调用用户委托。
- inactive、deactivating 或 disposed owner 均拒绝新执行，并产生 `CommandRejected`/专用诊断；不会自动等待未来激活。
- Scope A 已关闭后，同一 Command 对象在重新激活后的 Scope B 执行；任何执行都不能跨 Scope 转移。

## AUC-MVVM-010 Command Exception Delivery

### 两条调用路径

| 调用路径 | 异常合同 |
| --- | --- |
| 调用者 await `ExecuteAsync` | 更新 Failed 状态和诊断后，重新抛出原异常；保留堆栈。 |
| UI 通过 `ICommand.Execute` | 更新 Failed 状态和诊断后，把原异常恰好一次提交给 `ICommandExceptionHandler`。 |

- `OperationCanceledException` 只有在本次 operation token 已请求取消时分类为 Canceled；它不进入异常 handler。
- `ICommandExceptionHandler` 是应用策略扩展点。context 包含原异常、command name、owner type 和 operation id；框架不自动复制命令参数，避免把秘密或完整凭据带入异常链路。
- handler 失败产生独立诊断，不能覆盖原命令异常，也不能再次递归调用 handler。
- City 只保证可捕获托管异常链路；StackOverflow、进程终止等 CLR 致命情况不在保证范围内。
- City.Mvvm 不提供 UI、磁盘日志或 file sink。应用可以在 handler 中选择展示、遥测或自己的日志 provider。

## 诊断合同

| Code | Name | 必需上下文 |
| --- | --- | --- |
| `AUCMVVM009` | ActivationTaskRejected | `scopeId`, `taskName`, `taskKey`, `reason` |
| `AUCMVVM010` | ActivationTaskFailed | `scopeId`, `taskName`, `taskKey`, `exceptionType` |
| `AUCMVVM011` | ActivationTaskDrainTimedOut | `scopeId`, `activeTaskCount`, `timeout` |
| `AUCMVVM012` | CommandInactive | `commandName`, `ownerType`, `activationState` |
| `AUCMVVM013` | CommandExceptionHandlerFailed | `commandName`, `ownerType`, `operationId`, `exceptionType` |

这些 code 已由 `MvvmDiagnosticIds` 公开，并有 failure-path 断言测试。

## 测试矩阵

- Task：正常、同步抛错、异步抛错、预取消、运行中取消、重复 key、完成后复用 key、关闭后准入、Dispose 竞态、释放顺序、超时和 registry 回收。
- Typed command：值类型、引用类型、null、业务 predicate true/false、业务属性通知和框架状态通知。
- Concurrency：Reject、Allow、RejectByKey，同 key 竞态和不同 key 并行，完成/取消/失败后准入恢复。
- Activation：构造期无 Scope、Scope A、停用、inactive rejection、Scope B 和 dispose。
- Exception：awaited 原异常、ICommand handler 恰好一次、取消排除、handler 失败和无磁盘副作用。
- Compatibility：PublicAPI baseline、`OperationResult` 重命名和迁移编译样例。

## 完成标准

- 四个 Feature 的源码、测试、API baseline 和模块文档在同一变更中完成。
- 全量 MVVM 测试、solution build/test 和文档门禁通过后，Feature 状态才可由 Planned 改为 Completed。
