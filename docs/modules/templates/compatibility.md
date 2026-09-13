# AtomUI.City.Templates Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 生成项目必须 restore、build 和 test。
- 模板变量必须校验。
- 输出不得包含机器绝对路径。
- dry-run 不写文件。
- 应用模板生成的 `<AppName>.slnx`、`Directory.Build.props`、`Directory.Packages.props`、`docs/<AppName>.md`、app project 和 test project 属于 1.0 generated output 兼容面；不得写入机器绝对路径，且必须阻断父目录 CPM 污染。
- 应用模板生成结果必须能通过 AtomUI.City 本地包源或发布包源执行 restore、build 和 test。
- 应用模板生成的测试项目必须引用 `AtomUI.City.Testing`，默认 smoke test 必须带 `TestLayerNames.TemplateSmoke` 标记，生产项目不得引用 Testing。
- `TemplateChange.Create` 的路径规范化语义、`TemplatePlan.Validate` 返回的 `AUCTPL1001`、`AUCTPL1002`、`AUCTPL1003` 诊断码和 context 字段属于 1.0 兼容承诺。
- `ApplicationTemplateOptions` 默认值、`EffectiveRootNamespace` 派生规则、`Validate` 诊断码 `AUCTPL0001`、`AUCTPL0002`、`AUCTPL0301` 及其 `variable`、`rawValue`、`rule` context 字段属于 1.0 兼容承诺。
- 插件模板生成的 plugin csproj、`atomui-city/plugin.json`、module、test project、Plugin MSBuild 属性、NuGet README 和 package metadata 属于 1.0 generated output 兼容面。
- `TemplateRenderResult.AppliedPaths`、成功结果要求 valid plan 且与 plan changes 完全一致、失败结果至少包含一个 diagnostic，属于 1.0 结果不变量。
- `AUCTPL1004`、`AUCTPL1005`、`AUCTPL1006` 及其 `templateId`、`operationId`、`targetPath`、`path`、`errorType` 字段属于 1.0 兼容承诺。
- 插件模板的项目命名 token 与 `PluginId` token 相互独立；`AtomUICityPlugin*` MSBuild 属性名不得随项目名变化。
- 当前应用模板是无 UI Host 工作区；Avalonia `Application` 和 desktop lifetime 只有在 `AUC-TEMPLATES-010` 完成后才进入 generated output 兼容面。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `ApplicationTemplateRenderer.Render(ApplicationTemplateOptions, CancellationToken)` 是 1.0 兼容承诺；预取消 token 必须在写入任何文件前抛 `OperationCanceledException`。
- `GenerationTemplateKind` 的五个枚举值、`GenerationTemplateOptions` 默认 culture/IncludeTests/reload policy、各 kind 的 generated path、`GenerationTemplateRenderer.CreatePlan/Render` 进入 1.0 兼容承诺。
- `AUCTPL2001~2005` 的语义与 context 字段进入 1.0 兼容承诺；增量 renderer 的冲突、IO 和 rollback 继续使用 `AUCTPL1004~1006`。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
