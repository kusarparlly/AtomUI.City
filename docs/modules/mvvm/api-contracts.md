# AtomUI.City.Mvvm API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块当前全部 public 类型和成员均为 `Preview`。`PublicAPI.Unshipped.txt` 只冻结待审签名，不表示 `Stable`；升为稳定 API 必须逐项补齐 API Card、兼容规则和测试证据。

## Commands V2 已实现 API

以下 contract 由 [ADR 0010](../../decisions/0010-activation-scoped-structured-concurrency.md) 批准并已落地；发布验证状态以全局跟踪文档为准：

| API | 目标合同 |
| --- | --- |
| `IActivationScope.StartAsync` | 先注册再调用用户委托；返回原始 completion；Scope 关闭时取消并 drain。 |
| `IActivationScope.StartUniqueAsync` | 同 Scope 同 key 单飞，重复准入稳定拒绝，终态释放 key。 |
| activation owner contract | 每次命令执行读取当前 Scope；inactive/disposing/disposed 稳定拒绝。 |
| `CommandFactory.Create<T>` / `CreateAsync<T>` | CommunityToolkit 泛型 command、同步业务 CanExecute、operation token，以及可选 UI 通知 dispatcher。 |
| `IActivationScopeOwner.ActivationStateChanged` | 当前 activation state 或 current scope 变化通知；owner-bound command 用它刷新 `CanExecute`；单个订阅者失败不得阻断其他订阅者或生命周期提交。 |
| command concurrency policy | `Reject`（默认）、`Allow`、`RejectByKey`；执行入口原子准入。 |
| `ICommandExceptionHandler` | `ICommand.Execute` 失败时接收原异常恰好一次；取消排除；handler 失败独立诊断。 |
| `OperationExecutionResult` | 替换框架 `OperationResult` 名称，只描述执行运行时，不表示领域结果。 |
| `CommandExecutionDomain` | 多个显式加入的 Command 共享 latest-wins 所有权；新执行取得 generation/current lease 后取消旧执行且不等待旧执行退出。 |

完整准入、生命周期、失败和测试合同见 [structured-concurrency-and-commands-v2.md](structured-concurrency-and-commands-v2.md)。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| ViewModel Base | ViewModelBase | City 自有属性通知、生命周期和释放入口。 | 不引用具体 View/Avalonia visual，也不公开继承 CommunityToolkit 具体基类。 |
| Generated Validation | ValidatingViewModelBase | `INotifyDataErrorInfo`、错误集合以及 generated validation 调度入口。 | 仅验证型 ViewModel 显式继承；不运行时反射发现属性或 Attribute。 |
| Activation | IActivatable, ICanDeactivate, IConfirmDeactivate, ActivationScope, DeactivationGuard | ViewModel 激活和停用。 | 状态机必须可测试，拒绝停用不抛业务异常。 |
| Command | CommandFactory, CommandExecutionDomain, CommandExecutionLease, OperationScope, OperationExecutionResult | 命令执行、跨命令 latest-wins 所有权和操作结果。 | 异常、取消、并发拒绝必须有稳定结果；domain 不承载业务提交策略。 |
| Interaction | Interaction<TRequest, TResult> | ViewModel 到 UI 的请求 contract。 | 无 handler 返回 NotHandled，不直接依赖 Presentation 类型。 |
| Validation | ValidationScope, ValidationMessage, ValidationChangedEventArgs | 验证状态和消息聚合。 | 消息变化可被 Presentation 绑定。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ViewModelBase.SetProperty | 更新属性并触发通知。 | propertyName 必须稳定且非空白，比较器可选。 | bool 表示是否变化。 | Dispose 后抛 `ObjectDisposedException`；空白 propertyName 抛 `ArgumentException`。 | 同步 API 无 token。 | 相等值不重复通知；调用线程发布通知，UI marshal 由 Presentation 负责。 |
| ValidatingViewModelBase.SetProperty(validate) | 更新属性，并在值改变后进入该属性的 generated validator。 | field、value、validate、稳定 propertyName；类型必须具有可生成规则或无规则。 | bool 表示属性是否变化；错误通过 `INotifyDataErrorInfo` 查询。 | 未生成/不支持的验证声明在编译期失败；运行时规则异常提交 Failed/诊断，不伪装为普通 Invalid。 | 同步 Attribute 规则无 token；异步业务验证使用 ValidationScope。 | 同一 mutation 只提交一次最终错误快照；事件在状态提交后、框架锁外发布。 |
| ValidatingViewModelBase.ValidateAllProperties | 执行 Generator 已知的全部属性规则。 | 无。 | void；结果经 HasErrors/GetErrors 读取。 | 无 generated rule 时成功 no-op；规则异常进入 Failed/诊断。 | 同步 API 无 token。 | 属性顺序和同一属性规则顺序由 Generator 确定且 byte-stable。 |
| Interaction<TRequest, TResult>.RegisterHandler | 注册 Interaction handler。 | handler 不得为 null；activationScope 可选。 | IDisposable（随 ActivationScope 释放或手动解除）。 | handler 为 null 抛 `ArgumentNullException`；scope 已释放时立即释放 handler。 | handler 持 activationScope token，停用时取消。 | 多 handler 时最后注册者优先（last-wins）；释放后不再接收请求。 |
| ViewModelBase.Dispose | 释放 ViewModel 生命周期资源。 | 无。 | void。 | 重复 Dispose 幂等；当前 ActivationScope 含异步任务或资源时，同步释放在提交 Disposed 前抛 `InvalidOperationException`，并保留 ActivationContext 供 `DisposeAsync` 恢复。 | 同步 API 无 token。 | 成功后进入 Disposed 终态，后续 mutation 被拒绝；**不执行 Deactivating/OnDeactivatedAsync 钩子**——需要停用语义时先调 DeactivateAsync 再 Dispose。 |
| ViewModelBase.ActivateAsync | 激活 ViewModel。 | ActivationContext 不得为 null；scope 不得为 null；调用方 token 可选。 | ValueTask。 | 激活异常不进入 Active，释放 ActivationScope，并在 exception data 写入 ViewModel type、scope id 和 stage。 | 调用方 token 与 ActivationScope token 由基类合成为唯一有效令牌；预取消释放候选 scope 后抛 `OperationCanceledException`。 | Active 状态下重复 Activate 幂等返回。 |
| ViewModelBase.DeactivateAsync | 停用 ViewModel。 | CancellationToken 可选。 | ValueTask。 | 取消在进入 Deactivating 前抛出并保持 Active scope；已进入停用后释放当前 scope；Disposed 后调用抛 `ObjectDisposedException`。 | 调用方 token 与当前 ActivationScope token 合成后传给唯一停用钩子。 | Constructed、Deactivated 或 Deactivating 状态下幂等返回。 |
| DeactivationGuard.CanDeactivateAsync | 离开前确认。 | viewModel 不得为 null；可实现 ICanDeactivate 或 IConfirmDeactivate。 | DeactivationResult。 | 拒绝返回 Reject；取消返回 Cancel；异常映射 Failed，不抛业务异常。 | 预取消跳过 viewModel 并返回 Cancel。 | 先执行 ICanDeactivate，Allow 后再执行 IConfirmDeactivate。 |
| CommandFactory.Create | 创建同步命令。 | execute 不得为 null；canExecute 和 state 可选。 | IRelayCommand。 | execute 异常写入 `OperationExecutionResult` Failed 后重抛原异常。 | 同步 API 无 token。 | 写入 CommandExecutionState；CanExecuteChanged 可通知 Presentation。 |
| CommandFactory.CreateAsync | 创建异步命令。 | execute 不得为 null；state、activation owner/fixed scope、并发策略、异常 handler 和 notification dispatcher 按重载可选。 | IAsyncRelayCommand / IAsyncRelayCommand<T>。 | 直接 await 写入 Failed 后重抛原异常；`ICommand.Execute` 提交应用 handler；并发冲突映射 Rejected；同步、inline 或排队后的通知失败统一记录 `AUCMVVM014`，不改变命令结果。 | 命令 token 必须传递到 execute，并链接当前 ActivationScope token；UI 可观察通知统一调用配置 dispatcher 的 `InvokeAsync(..., CancellationToken.None)`。 | 默认 Reject；可选 Allow/RejectByKey；owner 重载每次执行解析当前 Scope；`ExecutionTask` 必须在对应属性通知前发布；dispatcher 是通知线程的唯一裁决者，不改变业务执行线程。 |
| CommandFactory.CreateAsync (domain-first) | 创建显式加入共享 latest-wins domain 的异步命令。 | domain 和 execute 不得为 null；其余 owner、state、单 Command 并发策略、handler、diagnostics/dispatcher 与既有重载一致。 | IAsyncRelayCommand / IAsyncRelayCommand<T>；execute 额外收到框架创建的 `CommandExecutionLease`。 | domain closing/disposed 竞态映射 Rejected，不调用用户委托；被后发执行替换时 Operation 进入 Canceled；业务异常沿用既有异常链路。 | domain lease token 链接命令/Activation token；新执行请求取消旧 current execution，但不等待旧执行退出。 | 单 Command 门禁先于 domain；generation 单调递增；同一时刻只有一个 lease 的 `IsCurrent` 为 true；应用仍负责提交结果前的业务版本检查。 |
| CommandExecutionDomain.DisposeAsync | 永久关闭共享执行 domain。 | 无；drain timeout 由构造参数配置。 | ValueTask。 | 重复 Dispose 幂等；取消回调失败记录 `AUCMVVM016` 后继续；超时记录 `AUCMVVM017`，不强杀用户任务。 | 关闭准入后请求取消全部 active execution，并等待其完成或达到 timeout。 | 释放期间和释放后的新准入均被拒绝；只读 generation/count/state 可继续查询。 |
| OperationScope.Start | 创建一次可观测 operation。 | cancellationToken 可为 None。 | OperationScope，包含 Id、Status、Result、Error、Elapsed 和 CancellationToken。 | 外部 token 取消会先标记 Canceled result，再通知 operation token callback。 | OperationScope 持有自己的取消源，并链接外部 token。 | 初始 Status 为 Running；Result 在终态前为 null；Dispose 释放取消源和外部注册。 |
| OperationScope.Complete/Cancel/Fail/Reject | 结束 operation。 | Fail 的 exception 不得为 null。 | OperationExecutionResult。 | Dispose 后调用抛 `ObjectDisposedException`；Fail 空 exception 抛 `ArgumentNullException`。 | Cancel 先提交 Canceled result，再取消 operation token。 | 首次终态胜出；重复终态返回同一个 OperationExecutionResult，Elapsed 保持稳定。 |
| OperationScope.Dispose | 释放 operation scope。 | 无。 | void。 | 重复 Dispose 幂等；Dispose 后 mutating API 被拒绝。 | 未终止 operation 在 Dispose 时进入 Canceled 并通知 token callback。 | Dispose 后 Id、Status、Result、Error、Elapsed 仍可读取。 |
| Interaction.RequestAsync | 请求 UI interaction。 | request 为 TRequest；handler 通过 ActivationScope 可选绑定。 | InteractionResult<TResult>。 | 无 handler 返回 NotHandled；handler 异常返回 Failed。 | 取消后不提交 handler result。 | 每次调用独立 InteractionContext，包含 request id、request type、handler type 和 scope id。 |
| ValidationScope.SetMessages / BindTo | 替换字段验证消息并可绑定 ActivationScope。 | field key 可为空表示 global；messages 不得含 null；activationScope 不得为 null。 | void；状态通过 Status 读取。 | Dispose 后 mutation 抛 `ObjectDisposedException`；空 messages 清理 field；重复 message 去重；ValidationScope 先释放后，父 ActivationScope 关闭必须幂等。 | 父 ActivationScope 关闭时把尚存活的 ValidationScope 置为 Canceled。 | 提交后触发 ValidationChanged，事件包含 field key、Status、Errors、Messages 和 owner scope id。 |

### `CommandConcurrencyPolicy`

| Field | Contract |
| --- | --- |
| Type | `CommandConcurrencyPolicy` |
| Stability / Feature | Preview / AUC-MVVM-003、AUC-MVVM-012 |
| Purpose | 为单个异步 Command 定义原子准入策略；它与跨 Command 的 latest-wins `CommandExecutionDomain` 分层。 |
| Values | `Reject=0`：任一执行 active 时拒绝；`Allow=1`：允许独立并发；`RejectByKey=2`：只拒绝同一开发者 key。 |
| Validation | 未知枚举值立即抛 `ArgumentOutOfRangeException`；`RejectByKey` 缺少 `concurrencyKeySelector` 抛 `ArgumentNullException`。 |
| Concurrency | 检查与占有 active count/key 在同一 gate 内原子完成；执行终结时释放占有。默认值固定为 `Reject`。 |
| Compatibility / Tests | 数值、默认值、准入与 key 释放语义属于 Preview public contract；`CommandTests` 覆盖 Reject、Allow、RejectByKey 和非法输入。 |

### `CommandExceptionContext`

| Field | Contract |
| --- | --- |
| Type | `CommandExceptionContext` |
| Stability / Feature | Preview / AUC-MVVM-003 |
| Purpose | 向 `ICommandExceptionHandler` 传递 `ICommand.Execute` 路径中的原始异常和非秘密命令身份。 |
| Fields | `Exception`、nullable `CommandName`、nullable `OwnerType`、`OperationId`；框架创建时 Exception 是原始业务异常，OperationId 优先来自执行异常数据。 |
| Ownership / Lifetime | 每次异常处理创建的不可变 record；handler 不应长期持有命令或 ViewModel 引用。 |
| Cancellation / Failure | handler 使用独立显式 token；业务取消不进入 exception handler。handler 自身失败被 `AUCMVVM013` 隔离和诊断，不替换原命令结果，也不二次调用 handler。 |
| Compatibility / Tests | 字段及“原始异常恰好一次”语义属于 Preview public contract；`CommandTests` 覆盖 ICommand.Execute、handler failure 与 cancellation exclusion。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ActivationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationScope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationScopeAccessor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandExecutionState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandExecutionDomain` | 关键 contract | latest-wins、generation、drain、关闭后行为或默认 timeout 变化必须更新 ADR、本文档和 compatibility。 |
| `CommandExecutionLease` | 支持类型 | Generation、CancellationToken 或 IsCurrent 语义变化必须更新 ADR、本文档和 compatibility。 |
| `CommandConcurrencyPolicy` | 关键 contract | 枚举数值、默认准入、key 行为或非法值验证变化必须更新本文档和 compatibility。 |
| `CommandExceptionContext` | 支持类型 | 字段、原始异常身份或 handler 交付语义变化必须更新本文档和 compatibility。 |
| `CommandFactory` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandGroup` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DeactivationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DeactivationStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DeactivationGuard` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IActivatable` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IActivationScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICanDeactivate` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IConfirmDeactivate` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `Interaction<TRequest, TResult>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionContext<TRequest>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionResult<TResult>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `MvvmDiagnosticIds` | 诊断 contract | AUCMVVM001-017 的删除、重命名或语义变化必须更新本文档、diagnostics 和 compatibility。 |
| `OperationExecutionResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `OperationScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `OperationStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationMessage` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewModelBase` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidatingViewModelBase` | 关键 contract（Designed） | 继承边界、INotifyDataErrorInfo、错误提交、generated hook 或 Dispose 后行为变化必须更新 ADR、本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- `IActivatable`、`ViewModelBase` 和 protected activation hooks 不保留 tokenless/tokenful 双入口；开发者只实现或覆盖一个带末尾 token 的方法。
- Scope token 是框架所有权信号，不要求业务代码额外读取；回调只向下传递收到的有效令牌。
- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。

`AUC-MVVM-007` 至 `010` 已通过模块测试证明：同步 Dispose 不等待异步任务；用户回调不在框架锁内；泛型/非泛型命令语义一致；`OperationResult` 重命名已同步 API baseline 和迁移说明。`AUC-MVVM-012` 已按 [ADR 0015](../../decisions/0015-shared-command-execution-domain.md) 实现，并通过 MVVM/Presentation 模块测试和 Public API build/pack 门禁；全套 package consumer 与发布门禁仍待执行。
