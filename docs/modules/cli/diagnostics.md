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

## Repository Governance Gates 诊断码

以下 code 由 `AUC-CLI-008` 生产 evaluator 实现并由 contract tests 固定：

| Planned Code | Name | Required Context |
| --- | --- | --- |
| `AUCCLI0601` | GovernanceWorkspaceNotRecognized | `path` |
| `AUCCLI0602` | DocumentationRootMissing | `path` |
| `AUCCLI0603` | RequiredDocumentMissing | `path`, `module` |
| `AUCCLI0604` | DocumentationLinkInvalid | `path`, `position`, `target` |
| `AUCCLI0605` | FeatureIdentityInvalid | `path`, `position`, `featureId` |
| `AUCCLI0606` | FeatureIdentityDuplicate | `path`, `position`, `featureId` |
| `AUCCLI0607` | FeatureTestMatrixMissing | `path`, `module`, `featureId` |
| `AUCCLI0608` | DocumentationPlaceholderFound | `path`, `position` |
| `AUCCLI0609` | TestRootOrProjectMissing | `path`, `module` |
| `AUCCLI0610` | FeatureTestEvidenceMissing | `path`, `module`, `featureId` |
| `AUCCLI0611` | FeatureTestEvidenceInconsistent | `path`, `module`, `featureId` |
| `AUCCLI0612` | TestTypeInvalid | `path`, `featureId`, `testType` |
| `AUCCLI0613` | SpecializedTestEvidenceMissing | `path`, `featureId`, `testType` |
| `AUCCLI0614` | TestLayerInvalid | `path`, `position`, `testType` |
| `AUCCLI0615` | GovernanceFileReadFailed | `path`, `operation` |
| `AUCCLI0616` | GovernanceCheckCancelled | `path`, `operation` |

`AUCCLI0201` 已稳定表示 `DotnetCommandFailed`，不得再用于文档或测试门禁失败。读取失败与取消是不同故障，禁止合并为同一个诊断语义。

## Plan / Apply 诊断码

以下 code 由 `AUC-CLI-009` 的 Plan 1.0 与 create-only transaction 实现：

| Planned Code | Name | Exit Class | Required Context |
| --- | --- | --- | --- |
| `AUCCLI0701` | PlanArgumentsRequired | ArgumentError | `operation` |
| `AUCCLI0702` | PlanOperationUnsupported | ArgumentError | `operation`, `kind` |
| `AUCCLI0703` | PlanWorkspaceInvalid | Failure | `path`, `reason` |
| `AUCCLI0704` | PlanPathInvalid | Failure | `path`, `reason` |
| `AUCCLI0705` | PlanWriteFailed | Failure | `planFile`, `operationId`, `stage` |
| `AUCCLI0706` | PlanFileReadFailed | Failure | `planFile`, `reason` |
| `AUCCLI0707` | PlanSchemaInvalid | ArgumentError | `planFile`, `schemaVersion`, `reason` |
| `AUCCLI0708` | PlanWorkspaceMismatch | ArgumentError | `planFile`, `expectedWorkspaceId`, `actualWorkspaceId` |
| `AUCCLI0709` | PlanIntegrityInvalid | ArgumentError | `operationId`, `changeId`, `expectedHash`, `actualHash` |
| `AUCCLI0710` | ApplyPreconditionConflict | Failure | `operationId`, `path`, `expectedState`, `actualState` |
| `AUCCLI0711` | ApplyLockUnavailable | Failure | `operationId`, `path` |
| `AUCCLI0712` | ApplyCommitFailed | Failure | `operationId`, `path`, `stage` |
| `AUCCLI0713` | ApplyRollbackFailed | Failure | `operationId`, `path`, `originalFailure`, `rollbackFailure` |
| `AUCCLI0714` | PlanApplyCancelled | Failure | `operationId`, `stage`, `transactionState` |
| `AUCCLI0715` | PlanPreviewCleanupFailed | Failure | `operationId`, `temporaryPath`, `stage` |

模板自身拒绝 generation 输入时保留原始 `AUCTPL...` code。若 commit 与 rollback 同时失败，envelope 必须同时包含 `AUCCLI0712` 和一个或多个 `AUCCLI0713`，不能用 rollback failure 覆盖原始故障。

## Explain Catalog 诊断码

以下 code 由 `AUC-CLI-010` immutable explanation catalog 实现：

| Planned Code | Name | Exit Class | Required Context |
| --- | --- | --- | --- |
| `AUCCLI0801` | DiagnosticCodeRequired | ArgumentError | `argument` |
| `AUCCLI0802` | DiagnosticCodeUnknown | ArgumentError | `code` |
| `AUCCLI0803` | DiagnosticCatalogInvalid | Failure | `code`, `reason` |

`AUCCLI0802` 必须具有不依赖动态目录查找的静态 fallback 说明。目录重复、空 required field、未知 severity 或非法链接均属于 `AUCCLI0803`，并由 contract test 阻止进入发布产物。

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
