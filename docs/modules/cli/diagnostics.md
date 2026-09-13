# AtomUI.City.Cli Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCCLI0001` | CommandPrefixInvalid | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0002` | UnknownCommand | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0003` | CommandRequired | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0004` | UnknownOption | `src/AtomUI.City.Cli/CliCommandLine.cs` |
| `AUCCLI0005` | OptionValueRequired | `src/AtomUI.City.Cli/CliCommandLine.cs` |
| `AUCCLI0101` | AppNameRequired | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0102` | RootNamespaceReserved | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0103` | AotDynamicPluginConflict | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0104` | AppNameInvalid | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0105` | TargetAlreadyExists | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0106` | NewAppCancelled | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0201` | DotnetCommandFailed | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0202` | DotnetCommandCancelled | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0203` | DotnetWorkingDirectoryMissing | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0301` | PluginPackagePathRequired | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0302` | PluginInspectPathRequired | `src/AtomUI.City.Cli/CliApplication.cs` |
| `AUCCLI0401` | ExplicitConfirmationRequired | `src/AtomUI.City.Cli/CliApplication.cs` |

## Generation 当前诊断码

| Code | Name | Required Context |
| --- | --- | --- |
| `AUCCLI0501` | GenerationArgumentsRequired | `kind`, `name` |
| `AUCCLI0502` | GenerationKindUnsupported | `kind` |
| `AUCCLI0503` | GenerationProjectResolutionFailed | `workingDirectory`, `candidates` |
| `AUCCLI0504` | GenerationCancelled | `kind`, `name`, `operationId` |

模板验证与事务失败保持原始 `AUCTPL...` code，不包装成新的 CLI code。

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

`tests/AtomUI.City.Cli.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
