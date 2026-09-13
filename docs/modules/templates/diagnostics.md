# AtomUI.City.Templates Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Code | Name | Source |
| --- | --- | --- |
| `AUCTPL0001` | InvalidTemplateVariable | `src/AtomUI.City.Templates/ApplicationTemplateOptions.cs` |
| `AUCTPL0002` | ReservedFrameworkNamespace | `src/AtomUI.City.Templates/ApplicationTemplateOptions.cs` |
| `AUCTPL0301` | AotDynamicPluginConflict | `src/AtomUI.City.Templates/ApplicationTemplateOptions.cs` |
| `AUCTPL1001` | InvalidTemplatePath | `src/AtomUI.City.Templates/TemplatePlan.cs` |
| `AUCTPL1002` | DuplicateTemplatePath | `src/AtomUI.City.Templates/TemplatePlan.cs` |
| `AUCTPL1003` | UnsupportedTemplateChangeType | `src/AtomUI.City.Templates/TemplatePlan.cs` |
| `AUCTPL1004` | TemplateOutputConflict | `ApplicationTemplateRenderer`, `GenerationTemplateRenderer` |
| `AUCTPL1005` | TemplateOutputFailure | `ApplicationTemplateRenderer`, `GenerationTemplateRenderer` |
| `AUCTPL1006` | TemplateRollbackFailure | `ApplicationTemplateRenderer`, `GenerationTemplateRenderer` |

## Generation 当前诊断码

| Code | Name | Required Context |
| --- | --- | --- |
| `AUCTPL2001` | InvalidGenerationName | `templateId`, `variable`, `rawValue`, `rule` |
| `AUCTPL2002` | InvalidGenerationProject | `templateId`, `variable`, `rawValue`, `rule` |
| `AUCTPL2003` | InvalidGenerationNamespace | `templateId`, `variable`, `rawValue`, `rule` |
| `AUCTPL2004` | GenerationRouteRequired | `templateId`, `variable`, `rawValue`, `rule` |
| `AUCTPL2005` | InvalidGenerationCulture | `templateId`, `variable`, `rawValue`, `rule` |

## 产品级必须诊断的失败

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 上下文字段

模板执行诊断必须优先携带 `templateId`、`operationId`、`targetPath` 和 `path`；变量诊断携带 `variable`、`rawValue`、`rule`；plan 诊断携带 `firstPath`、`normalizedPath` 和 `type`；IO/rollback 诊断携带 `errorType`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.TemplateSmokeTests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
