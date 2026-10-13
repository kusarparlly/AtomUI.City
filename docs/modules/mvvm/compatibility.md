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
- `ActivationScope.Id`、`ActivationScope.IsDisposed`、`IActivatable` cancellation overload、`DeactivationGuard` 执行顺序和 `DeactivationStatus.Failed` 进入 1.0 兼容承诺。
- `CommandExecutionState` 的 command name、owner type、rejected execution 统计、`OperationResult.OperationId` 和 `OperationStatus.Rejected` 进入 1.0 兼容承诺。
- `OperationScope` 的 `IDisposable`、`Status`、`Result`、`Error`、`Elapsed`、`IsDisposed`、`OperationStatus.Running`、首次终态胜出和取消先提交状态再通知 token callback 进入 1.0 兼容承诺。
- `InteractionContext` 的 request id、request type、activation scope id、handler type，以及 Interaction 取消后不提交 handler result 的行为进入 1.0 兼容承诺。
- `ValidationScope.SetMessages`、`ValidationChanged`、`ValidationChangedEventArgs`、message 去重和 Dispose 后 mutation 拒绝进入 1.0 兼容承诺。

## 已批准的 Preview 迁移

- `OperationResult` 将在 `AUC-MVVM-008` 中重命名为 `OperationExecutionResult`，明确它是命令/operation 运行快照，不是领域业务结果。实现提交必须同时更新 public API baseline、示例和迁移说明。
- `CommandFactory.CreateAsync` 的固定 `IActivationScope?` 捕获不再作为 ViewModel 默认合同；新的 activation owner 入口每次执行解析当前 Scope。显式固定 Scope 只保留为标注清晰的低级一次性入口。
- 旧的“异步命令异常只记录 Failed 且不向 awaited caller 抛出”行为将由 `AUC-MVVM-010` 替换。调用方必须迁移为 await/catch，UI `ICommand` 路径必须提供应用异常 handler。
- 上述能力完成前保持 Planned，不得把文档目标误认为当前二进制兼容面。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
