# AtomUI.City.Generators Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCGEN002` | DuplicateModuleName | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN003` | CircularModuleDependency | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN004` | DuplicateRoute | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN005` | InvalidManifestInput | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN006` | DuplicatePresentationView | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN007` | MultipleApplicationModules | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCGEN008` | InvalidGeneratedModule | `src/AtomUI.City.Generators/Diagnostics/GeneratorDiagnosticIds.cs` |
| `AUCANL0001` | BuildServiceProviderNotAllowed | `src/AtomUI.City.Generators/Diagnostics/AnalyzerDiagnosticIds.cs` |

`AUCGEN001` 在 1.0 Preview 中保留但不公开定义。原 `DynamicDiscoveryNotAllowed` 从未有对应输入和触发链；运行时动态发现未来作为独立 Feature 实现时，必须重新评审诊断合同。

## Feature 诊断归属

| Feature | Category | 当前失败码 | 阻断行为 |
| --- | --- | --- | --- |
| EventBus generation | `AtomUI.City.Generators.EventBus` | `AUCGEN005` | 任一 contract、channel、handler、owner、共享对象图或跨程序集 manifest 输入非法时，不得发布包含不完整 EventBus metadata 的 service registrar。 |
| Data generation | `AtomUI.City.Generators.Data` | `AUCGEN005` | 任一 client/operation 输入非法或 client id 重复时，不得生成该 compilation 的 Data registrar。 |

EventBus 与 Data 当前复用 `InvalidManifestInput` 是既有公开诊断合同；不得仅因内部 reader 重构改变 code、默认 severity、feature category 或 source location。若未来需要更细诊断码，必须先更新 compatibility、测试与迁移说明。

## 产品级必须诊断的失败

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Generators.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
`GeneratorDiagnostics.CreateRoslynDiagnostic` 必须断言 severity、category、message args 和 source location。
