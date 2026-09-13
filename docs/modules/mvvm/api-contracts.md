# AtomUI.City.Mvvm API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| ViewModel Base | ViewModelBase | 属性通知和释放入口。 | 不引用具体 View 或 Avalonia visual。 |
| Activation | IActivatable, ICanDeactivate, IConfirmDeactivate, ActivationScope, DeactivationGuard | ViewModel 激活和停用。 | 状态机必须可测试，拒绝停用不抛业务异常。 |
| Command | CommandFactory, OperationScope, OperationResult | 命令执行和操作结果。 | 异常、取消、并发拒绝必须有稳定结果。 |
| Interaction | Interaction<TRequest, TResult> | ViewModel 到 UI 的请求 contract。 | 无 handler 返回 NotHandled，不直接依赖 Presentation 类型。 |
| Validation | ValidationScope, ValidationMessage, ValidationChangedEventArgs | 验证状态和消息聚合。 | 消息变化可被 Presentation 绑定。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ViewModelBase.SetProperty | 更新属性并触发通知。 | propertyName 必须稳定且非空白，比较器可选。 | bool 表示是否变化。 | Dispose 后抛 `ObjectDisposedException`；空白 propertyName 抛 `ArgumentException`。 | 同步 API 无 token。 | 相等值不重复通知；调用线程发布通知，UI marshal 由 Presentation 负责。 |
| Interaction<TRequest, TResult>.RegisterHandler | 注册 Interaction handler。 | handler 不得为 null；activationScope 可选。 | IDisposable（随 ActivationScope 释放或手动解除）。 | handler 为 null 抛 `ArgumentNullException`；scope 已释放时立即释放 handler。 | handler 持 activationScope token，停用时取消。 | 多 handler 时最后注册者优先（last-wins）；释放后不再接收请求。 |
| ViewModelBase.Dispose | 释放 ViewModel 生命周期资源。 | 无。 | void。 | 重复 Dispose 幂等；释放当前 ActivationScope。 | 同步 API 无 token。 | 进入 Disposed 终态，后续 mutation 被拒绝；**不执行 Deactivating/OnDeactivatedAsync 钩子**——需要停用语义时先调 DeactivateAsync 再 Dispose。 |
| ViewModelBase.ActivateAsync | 激活 ViewModel。 | ActivationContext 不得为 null；scope 不得为 null。 | ValueTask。 | 激活异常不进入 Active，释放 ActivationScope，并在 exception data 写入 ViewModel type、scope id 和 stage。 | 必须观察 token；预取消释放候选 scope 后抛 `OperationCanceledException`。 | Active 状态下重复 Activate 幂等返回。 |
| ViewModelBase.DeactivateAsync | 停用 ViewModel。 | CancellationToken 可选。 | ValueTask。 | 取消在进入 Deactivating 前抛出并保持 Active scope；已进入停用后释放当前 scope。 | 必须观察 token。 | Constructed、Deactivated 或 Disposed 状态下幂等返回。 |
| DeactivationGuard.CanDeactivateAsync | 离开前确认。 | viewModel 不得为 null；可实现 ICanDeactivate 或 IConfirmDeactivate。 | DeactivationResult。 | 拒绝返回 Reject；取消返回 Cancel；异常映射 Failed，不抛业务异常。 | 预取消跳过 viewModel 并返回 Cancel。 | 先执行 ICanDeactivate，Allow 后再执行 IConfirmDeactivate。 |
| CommandFactory.Create | 创建同步命令。 | execute 不得为 null；canExecute 和 state 可选。 | IRelayCommand。 | execute 异常映射 OperationResult Failed，不外抛到 UI。 | 同步 API 无 token。 | 写入 CommandExecutionState；CanExecuteChanged 可通知 Presentation。 |
| CommandFactory.CreateAsync | 创建异步命令。 | execute 不得为 null；state 和 activationScope 可选。 | IAsyncRelayCommand。 | execute 异常映射 OperationResult Failed；并发执行映射 Rejected。 | 命令 token 必须传递到 execute，并链接 ActivationScope token。 | 单一 command state 同时只允许一个 running operation；执行完成后的 `CanExecuteChanged`/`PropertyChanged` 必须回到调用 `ExecuteAsync` 时捕获的同步上下文。 |
| OperationScope.Start | 创建一次可观测 operation。 | cancellationToken 可为 None。 | OperationScope，包含 Id、Status、Result、Error、Elapsed 和 CancellationToken。 | 外部 token 取消会先标记 Canceled result，再通知 operation token callback。 | OperationScope 持有自己的取消源，并链接外部 token。 | 初始 Status 为 Running；Result 在终态前为 null；Dispose 释放取消源和外部注册。 |
| OperationScope.Complete/Cancel/Fail/Reject | 结束 operation。 | Fail 的 exception 不得为 null。 | OperationResult。 | Dispose 后调用抛 `ObjectDisposedException`；Fail 空 exception 抛 `ArgumentNullException`。 | Cancel 先提交 Canceled result，再取消 operation token。 | 首次终态胜出；重复终态返回同一个 OperationResult，Elapsed 保持稳定。 |
| OperationScope.Dispose | 释放 operation scope。 | 无。 | void。 | 重复 Dispose 幂等；Dispose 后 mutating API 被拒绝。 | 未终止 operation 在 Dispose 时进入 Canceled 并通知 token callback。 | Dispose 后 Id、Status、Result、Error、Elapsed 仍可读取。 |
| Interaction.RequestAsync | 请求 UI interaction。 | request 为 TRequest；handler 通过 ActivationScope 可选绑定。 | InteractionResult<TResult>。 | 无 handler 返回 NotHandled；handler 异常返回 Failed。 | 取消后不提交 handler result。 | 每次调用独立 InteractionContext，包含 request id、request type、handler type 和 scope id。 |
| ValidationScope.SetMessages | 替换字段验证消息。 | field key 可为空表示 global；messages 不得含 null。 | void；状态通过 Status 读取。 | Dispose 后抛 `ObjectDisposedException`；空 messages 清理 field；重复 message 去重。 | 同步 API 无 token。 | 提交后触发 ValidationChanged，事件包含 field key、Status、Errors、Messages 和 owner scope id。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ActivationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationScope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationScopeAccessor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandExecutionState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
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
| `OperationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `OperationScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `OperationStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationMessage` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewModelBase` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
