# AtomUI.City.Mvvm Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。
- `IHostDiagnostics.Write` 是纯观察性出口；即使自定义实现抛异常，也不得覆盖业务异常、回滚状态提交或阻断 Task 完成。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCMVVM001` | ActivationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM002` | DeactivationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM003` | CommandFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM004` | CommandRejected | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM005` | InteractionNotHandled | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM006` | InteractionFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM007` | ActivationScopeDisposeFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM008` | ValidationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM009` | ActivationTaskRejected | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM010` | ActivationTaskFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM011` | ActivationTaskDrainTimedOut | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM012` | CommandInactive | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM013` | CommandExceptionHandlerFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM014` | CommandNotificationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM015` | ActivationStateNotificationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM016` | CommandExecutionDomainCancellationFailed | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |
| `AUCMVVM017` | CommandExecutionDomainDrainTimedOut | `src/AtomUI.City.Mvvm/MvvmDiagnosticIds.cs` |

## 上下文 key 合同

| Code | Required Context |
| --- | --- |
| `AUCMVVM001` | `viewModelType`、`scopeId`、`stage`。 |
| `AUCMVVM002` | `viewModelType`。 |
| `AUCMVVM003`/`AUCMVVM004` | `commandName`、`ownerType`、`operationId`。 |
| `AUCMVVM005`/`AUCMVVM006` | `requestType`、`resultType`、`activationScopeId`、`exceptionType`。 |
| `AUCMVVM007` | `scopeId`。 |
| `AUCMVVM008` | `ownerScopeId`、`exceptionType`。 |
| `AUCMVVM009` | `scopeId`、`taskName`、`taskKey`、`reason`。 |
| `AUCMVVM010` | `scopeId`、`taskName`、`taskKey`、`exceptionType`。 |
| `AUCMVVM011` | `scopeId`、`activeTaskCount`、`timeout`。 |
| `AUCMVVM012` | `commandName`、`ownerType`、`activationState`。 |
| `AUCMVVM013` | `commandName`、`ownerType`、`operationId`、`exceptionType`。 |
| `AUCMVVM014` | `commandName`、`ownerType`、`dispatcherType`、`exceptionType`。 |
| `AUCMVVM015` | `viewModelType`、`activationState`、`exceptionType`。 |
| `AUCMVVM016` | `domainId`、`generation`、`exceptionType`。 |
| `AUCMVVM017` | `domainId`、`activeExecutionCount`、`timeout`。 |

## 产品级必须诊断的失败

- Activation 失败或取消：写 `AUCMVVM001`（取消为 OCE，不作为失败统计）。
- Deactivation handler 异常：写 `AUCMVVM002`，释放继续。
- Command 执行异常：写 `AUCMVVM003`；并发拒绝：写 `AUCMVVM004`（Warning 级语义由承载方决定）。
- Interaction 无 handler：写 `AUCMVVM005`；handler 异常：写 `AUCMVVM006`。
- ActivationScope 资源释放失败：写 `AUCMVVM007`。
- Validation 逻辑异常：写 `AUCMVVM008`。
- Activation task 重复/关闭后准入、执行失败或 drain 超时：分别写 `AUCMVVM009-011`。
- inactive Command、应用异常 handler 失败和命令 UI 通知投递失败：分别写 `AUCMVVM012-014`。
- ActivationStateChanged 订阅者失败：写 `AUCMVVM015`，继续通知其他订阅者且不回滚生命周期状态。
- CommandExecutionDomain 取消回调失败和 drain 超时：分别写 `AUCMVVM016-017`；不得阻塞后发 Command 准入，也不得报告为已强杀用户代码。
- Command/Activation 诊断只输出结构化上下文，不创建或写入任何磁盘日志文件。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Mvvm.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
