# AtomUI.City.Mvvm Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。
- Interaction 只表达请求，UI 展示由 Presentation handler 完成。
- Command 状态支持完成、取消、异常和并发拒绝结果。
- ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `ViewModelBase` 实现 `IDisposable`、`IsDisposed`、`SetProperty` 释放后拒绝和 `OnDisposed` 继承钩子；这些行为进入 1.0 兼容承诺。
- `ActivationScope.Id`、`ActivationScope.IsDisposed`、`IActivatable` 单一 cancellation 入口、`DeactivationGuard` 执行顺序和 `DeactivationStatus.Failed` 进入 1.0 兼容承诺。Preview 阶段已移除 tokenless activation/deactivation overload；调用方 token 与 Scope token 由框架合成。
- `CommandExecutionState` 的 command name、owner type、active/rejected execution 统计、`OperationExecutionResult.OperationId` 和 `OperationStatus.Rejected` 进入 1.0 兼容承诺。
- `OperationScope` 的 `IDisposable`、`Status`、`Result`、`Error`、`Elapsed`、`IsDisposed`、`OperationStatus.Running`、首次终态胜出和取消先提交状态再通知 token callback 进入 1.0 兼容承诺。
- `InteractionContext` 的 request id、request type、activation scope id、handler type，以及 Interaction 取消后不提交 handler result 的行为进入 1.0 兼容承诺。
- `ValidationScope.SetMessages`、`ValidationChanged`、`ValidationChangedEventArgs`、message 去重和 Dispose 后 mutation 拒绝进入 1.0 兼容承诺。

## 已落地的 Preview 迁移

- `OperationResult` 已在 `AUC-MVVM-008` 中重命名为 `OperationExecutionResult`，明确它是命令/operation 运行快照，不是领域业务结果；Preview 阶段不保留旧名兼容壳。
- `CommandFactory.CreateAsync` 的固定 `IActivationScope?` 捕获不再作为 ViewModel 默认合同；新的 activation owner 入口每次执行解析当前 Scope。显式固定 Scope 只保留为标注清晰的低级一次性入口。
- 旧的“异步命令异常只记录 Failed 且不向 awaited caller 抛出”行为已由 `AUC-MVVM-010` 替换。调用方必须迁移为 await/catch，UI `ICommand` 路径必须提供应用异常 handler。
- `IActivationScope.StartAsync/StartUniqueAsync`、三种命令并发策略和 activation owner 合同已进入 Preview 二进制兼容面。
- `IActivationScopeOwner.ActivationStateChanged` 已进入 Preview 二进制兼容面；自定义 owner 必须在 state 或 current scope 变化后发布通知，以保证 owner-bound command 的 `CanExecute` 可观察。
- `CommandFactory.CreateAsync` 新增可选 `notificationDispatcher`；未传入时保持既有通知线程行为，UI 绑定命令应显式传入 dispatcher。通知投递失败使用新增稳定诊断 `AUCMVVM014`。
- `CommandConcurrencyPolicy` 的 `Reject=0`、`Allow=1`、`RejectByKey=2`、默认 `Reject` 和 RejectByKey 必须提供 selector 的规则属于 Preview public compatibility surface。
- `CommandExceptionContext` 的 Exception、CommandName、OwnerType、OperationId 字段以及向 `ICommandExceptionHandler` 恰好交付一次原始异常的语义属于 Preview public compatibility surface。
- ADR 0013 将已配置 dispatcher 的通知行为收口为唯一 `InvokeAsync` 路径：Mvvm 不再查询 `CheckAccess`；同步、inline 和排队失败均被隔离并记录 `AUCMVVM014`，不得改变命令业务结果。该 Preview 行为修复不修改公开签名。
- ADR 0014 不修改 `CommandFactory`：其 dispatcher 继续可选以保持 CLI/Headless 兼容。Presentation 可以提供 dispatcher 必选的上层 UI 工厂，但不得反向引入 MVVM 对 Presentation/Avalonia 的依赖。
- ADR 0015 新增 domain-first `CommandFactory.CreateAsync` 和 `IUiCommandFactory.CreateAsync` 重载；既有重载、默认 `Reject` 及三种单 Command 并发策略不变。`CommandExecutionDomain` 固定为 latest-wins，不承诺 queue/priority/retry 等策略扩展点。
- `CommandExecutionLease.Generation`、`CancellationToken` 和 `IsCurrent` 进入 Preview 二进制兼容面；City currentness 不替代应用业务提交栅栏。
- `ExecutionTask` 现保证先发布引用、后发出属性通知；观察者不再可能在对应通知中读到旧 Task。
- 同步 `ViewModelBase.Dispose` 遇到异步 Scope 所有权时，在提交 Disposed 前拒绝并保留 ActivationContext；调用方可改用 `DisposeAsync` 完成清理。
- 生命周期事件订阅者异常被隔离并记录 `AUCMVVM015`；诊断承载方自身异常不属于业务控制流。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
