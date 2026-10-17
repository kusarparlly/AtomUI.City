# AtomUI.City.Mvvm Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-MVVM-001 | ViewModel Base and Notification | Completed | ViewModelBase | ViewModelBaseTests |
| AUC-MVVM-002 | Activation and Deactivation | Completed | IActivatable, ICanDeactivate, ActivationScope | ActivationScopeTests; DeactivationTests |
| AUC-MVVM-003 | Command Execution | Completed | CommandFactory, OperationScope, OperationExecutionResult, CommandExecutionState, CommandGroup | CommandTests |
| AUC-MVVM-004 | Interaction Requests | Completed | Interaction<TRequest, TResult>, InteractionContext<TRequest> | InteractionTests |
| AUC-MVVM-005 | Validation Model | Completed | ValidationScope, ValidationMessage, ValidationStatus | ValidationScopeTests |
| AUC-MVVM-006 | Operation and Cancellation Scope | Completed | OperationScope, CommandExecutionState | CommandTests |
| AUC-MVVM-007 | Activation-scoped Structured Tasks | Implemented | IActivationScope.StartAsync/StartUniqueAsync, ActivationScope | ActivationScopeTests |
| AUC-MVVM-008 | Typed Commands and CanExecute | Implemented | CommandFactory generic overloads, OperationExecutionResult | CommandTests |
| AUC-MVVM-009 | Command Concurrency and Activation Ownership | Implemented | Command concurrency policy, activation owner contract | CommandTests; ViewModelBaseTests |
| AUC-MVVM-010 | Command Exception Delivery | Implemented | ICommandExceptionHandler, command exception context | CommandTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。 | 必须有实现、测试或工程门禁证据。 |
| Interaction 只表达请求，展示和 handler 注册由 Presentation 承担。 | 必须有实现、测试或工程门禁证据。 |
| Command、Activation、Interaction、Validation 都必须有取消和失败结果语义。 | 必须有实现、测试或工程门禁证据。 |
| ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-MVVM-001 ViewModel Base and Notification

Feature ID: `AUC-MVVM-001`
Status: Completed
Goal: 定义框架推荐 ViewModel 基类和属性变更语义。
Public Contract: ViewModelBase
Runtime / Build Behavior: 提供属性通知、释放入口和可观测状态；不引用 Avalonia visual。
Failure Behavior: Dispose 后 mutating API 抛出 `ObjectDisposedException`；空白 property name 抛出 `ArgumentException`。
Threading / Cancellation: 属性通知发生在调用线程；需要 UI marshal 的工作由 Presentation 处理。
Diagnostics: 重复 notification storm 必须可通过测试定位到 property name。
Tests: `ViewModelBaseTests`
Required Assertions: 断言 PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-002 Activation and Deactivation

Feature ID: `AUC-MVVM-002`
Status: Completed
Goal: 统一 ViewModel 激活、停用、关闭确认和导航中断。
Public Contract: IActivatable, ICanDeactivate, IConfirmDeactivate, ActivationScope
Runtime / Build Behavior: ViewModelBase 管理 Constructed、Activating、Active、Deactivating、Deactivated、Disposed；DeactivationGuard 在 Presentation 替换 View 前执行。
Failure Behavior: CanDeactivate 拒绝时返回 DeactivationResult，不抛业务异常；激活失败不得进入 Active 并释放 ActivationScope；deactivation 异常映射为 Failed result。
Threading / Cancellation: 激活和停用 API 支持 CancellationToken；取消后不得注册新的资源。
Diagnostics: activation exception data 包含 ViewModel type、scope id 和 stage。
Tests: `ActivationScopeTests; DeactivationTests`
Required Assertions: 断言状态机、拒绝停用、取消、异常映射和资源释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-003 Command Execution

Feature ID: `AUC-MVVM-003`
Status: Completed
Goal: 提供 .NET 风格命令执行、CanExecute、并发和错误结果。
Public Contract: CommandFactory, OperationScope, OperationExecutionResult, CommandExecutionState
Runtime / Build Behavior: Command 执行进入 OperationScope；同步和异步命令都写入 CommandExecutionState 和 OperationExecutionResult；CanExecute 变化可被 Presentation 绑定。
Failure Behavior: 执行异常映射为 Failed；并发执行返回 Rejected result；失败不得吞掉诊断。
Threading / Cancellation: 异步命令必须观察 token；取消返回 Cancelled，不混用 Success。
Diagnostics: command diagnostics 包含 command name、owner ViewModel type 和 operation id。
Tests: `CommandTests`
Required Assertions: 断言成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-004 Interaction Requests

Feature ID: `AUC-MVVM-004`
Status: Completed
Goal: 让 ViewModel 声明 UI 交互请求而不依赖具体 UI。
Public Contract: Interaction<TRequest, TResult>, InteractionContext<TRequest>, InteractionResult<TResult>
Runtime / Build Behavior: Interaction 发布 request，由 Presentation 注册 handler；结果回到 ViewModel。
Failure Behavior: 无 handler 返回 NotHandled；handler 抛异常映射为 Failed；重复处理必须由 handler scope 控制。
Threading / Cancellation: Interaction 可以异步取消；取消后 handler result 不得提交。
Diagnostics: InteractionContext 包含 request id、request type、handler type 和 scope id。
Tests: `InteractionTests`
Required Assertions: 断言有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-005 Validation Model

Feature ID: `AUC-MVVM-005`
Status: Completed
Goal: 定义输入验证消息、状态聚合和 Presentation 可绑定合同。
Public Contract: ValidationScope, ValidationMessage, ValidationStatus
Runtime / Build Behavior: ValidationScope 聚合 field/global message；ValidationChanged 事件为 Presentation 提供绑定输入。
Failure Behavior: 空消息清理 field；重复 message 去重；Dispose 后更新抛 `ObjectDisposedException`。
Threading / Cancellation: 验证可以在后台线程计算，最终状态提交必须遵守调用方调度策略。
Diagnostics: ValidationChangedEventArgs 包含 field key、状态、消息、错误和 owner scope id。
Tests: `ValidationScopeTests`
Required Assertions: 断言消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-006 Operation and Cancellation Scope

Feature ID: `AUC-MVVM-006`
Status: Completed
Goal: 为命令、激活、交互提供统一 operation 状态和取消边界。
Public Contract: OperationScope, CommandExecutionState
Runtime / Build Behavior: OperationScope 持有 operation id、Running 或终态 status、取消源、终态 result、error 和 elapsed；结束后只读。
Failure Behavior: 重复 Complete/Fail/Cancel/Reject 按首次终态生效；Dispose 后 mutating API 抛 `ObjectDisposedException`。
Threading / Cancellation: 取消先标记 Canceled 状态再通知执行体，避免状态乱序；外部 token 取消也通过 OperationScope 状态提交。
Diagnostics: operation diagnostics 必须包含 operation id、status 和 elapsed。
Tests: `CommandTests`
Required Assertions: 断言状态转换、取消顺序、重复终态、耗时字段和资源释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-007 Activation-scoped Structured Tasks

Feature ID: `AUC-MVVM-007`
Status: Implemented
Goal: 让一次 ActivationScope 内的异步工作有可控准入、监控、取消、drain 和唯一 worker 语义。
Public Contract: `IActivationScope.StartAsync`, `IActivationScope.StartUniqueAsync`, activity snapshot/count, async disposal.
Runtime / Build Behavior: Scope 在调用用户委托前注册任务；关闭时拒绝新准入、取消、等待任务、再释放资源；unique key 在任务终态后释放。
Failure Behavior: 重复 key、关闭后准入和非法参数稳定拒绝；非取消异常保留在返回 Task 并写诊断；超时不能伪装成强杀成功。
Threading / Cancellation: 用户委托和 continuation 不在框架锁内运行；外部 token 只取消本任务，Scope token 取消全部归属任务。
Diagnostics: `AUCMVVM009-011`。
Tests: `ActivationScopeTests`
Required Assertions: 准入、唯一 key、完成移除、关闭竞态、取消、drain、释放顺序、异常、超时和 registry 回收。
Acceptance Criteria: [结构化并发和 Commands V2](structured-concurrency-and-commands-v2.md) 的全部合同可由测试证明。

## AUC-MVVM-008 Typed Commands and CanExecute

Feature ID: `AUC-MVVM-008`
Status: Implemented
Goal: 提供同步/异步泛型参数命令、开发者业务谓词、准确通知和无领域歧义的执行结果类型。
Public Contract: `CommandFactory.Create<T>`, `CommandFactory.CreateAsync<T>`, `OperationExecutionResult`.
Runtime / Build Behavior: 泛型命令遵守 CommunityToolkit 参数语义；最终 CanExecute 组合业务 predicate、activation 和并发状态。
Failure Behavior: 非法参数按 CommunityToolkit/参数合同失败；业务执行失败进入 AUC-MVVM-010 链路。
Threading / Cancellation: 通知回到命令调用时捕获的同步上下文；异步 execute 接收 operation token。
Diagnostics: 复用 command name、owner type 和 operation id 上下文。
Tests: `CommandTests`
Required Assertions: 泛型值/null 参数、predicate、业务/框架通知、取消以及 `OperationResult` 到 `OperationExecutionResult` 的 API 基线迁移。
Acceptance Criteria: 泛型与非泛型入口行为一致，City 结果不表达领域业务成功。

## AUC-MVVM-009 Command Concurrency and Activation Ownership

Feature ID: `AUC-MVVM-009`
Status: Implemented
Goal: 用原子准入实现 Reject、Allow、RejectByKey，并让稳定 Command 对象在每次执行时绑定当前 ActivationScope。
Public Contract: command concurrency policy/options, activation owner contract, `CommandExecutionState` active count/result snapshot.
Runtime / Build Behavior: 默认 Reject；RejectByKey 同 key 单飞、不同 key 并行；每次执行解析当前 Scope 并通过 Scope task admission 进入监督。
Failure Behavior: inactive/deactivating/disposed owner 和原子并发冲突稳定拒绝，不调用用户委托；key selector 异常不隐式退化。
Threading / Cancellation: 只在短临界区修改 admission registry，任何锁不跨 await；Scope A 结束后重新激活使用 Scope B。
Diagnostics: `AUCMVVM004` 和 `AUCMVVM012`。
Tests: `CommandTests; ViewModelBaseTests`
Required Assertions: 三种策略、活动计数、同 key 竞态、完成后恢复、Scope A/B 和 inactive rejection。
Acceptance Criteria: `CanExecute` 仅作提示，执行入口仍能原子保证并发策略。

## AUC-MVVM-010 Command Exception Delivery

Feature ID: `AUC-MVVM-010`
Status: Implemented
Goal: 保证命令异常不被静默吞掉，同时把应用展示/持久化策略留在框架外。
Public Contract: `ICommandExceptionHandler`, command exception context, async CommandFactory exception path.
Runtime / Build Behavior: awaited `ExecuteAsync` 更新状态和诊断后重抛原异常；`ICommand.Execute` 把原异常恰好一次提交应用 handler。
Failure Behavior: cancellation 不进入 handler；handler 失败记录独立诊断且不递归；City 不写磁盘或展示 UI。
Threading / Cancellation: handler 不在框架锁内调用；handler completion 进入 Command ExecutionTask 监督。
Diagnostics: `AUCMVVM003` 和 `AUCMVVM013`。
Tests: `CommandTests`
Required Assertions: awaited 原异常/堆栈、UI handler 恰好一次、取消排除、handler 失败、无文件系统副作用。
Acceptance Criteria: 每个可捕获命令异常都能从调用者、handler 或结构化诊断中被明确观察。

## AUC-MVVM-011 Command Notification Dispatch

Feature ID: `AUC-MVVM-011`
Status: Implemented
Goal: 让绑定到 UI 的异步命令在生命周期由后台串行通道驱动时，仍只在 UI dispatcher 上发布可观察通知。
Public Contract: `CommandFactory.CreateAsync` 的可选 `notificationDispatcher` 参数。
Runtime / Build Behavior: 命令业务和 ViewModel 生命周期保持原线程模型；`CanExecuteChanged` 与命令状态 `PropertyChanged` 统一调用 dispatcher `InvokeAsync`，Mvvm 不自行查询 `CheckAccess`。
Failure Behavior: dispatcher 同步抛错、inline 回调失败或排队回调失败统一记录 `AUCMVVM014`，不得产生未观察 Task 异常或改写命令结果。
Threading / Cancellation: dispatcher 是通知线程的唯一裁决者；不得把命令执行体、Activation 或 Deactivation 整体搬入 UI 线程。最终状态通知使用 `CancellationToken.None`，不受业务 operation 取消阻断。
Diagnostics: `AUCMVVM014`。
Tests: `CommandTests`
Required Assertions: Activation 状态变化从非 UI 线程到达时，通知先排入 dispatcher，drain 后才交付观察者；inline 与排队订阅者异常均被诊断且不改变命令结果；Mvvm 不调用 `CheckAccess`。
Acceptance Criteria: Avalonia 等具有线程亲和性的绑定不会因 City 生命周期后台执行而收到跨线程命令通知，命令结果不再取决于通知发生时的线程。
