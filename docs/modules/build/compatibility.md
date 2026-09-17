# AtomUI.City.Build Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 所有构建输出集中到 output。
- pack warning 必须失败。
- 运行时包不得依赖 Testing 或 Roslyn。
- generator 包输出到 analyzers/dotnet/cs。
- 仓库项目清单和依赖边界只扫描真实 src/tests 项目；`src/AtomUI.City.Templates/templates` 下的模板 payload `.csproj` 不属于真实项目清单。
- CLI 到 PluginSystem 的项目引用属于 1.0 允许依赖，用于插件 manifest 和 package layout 检查。

## API 兼容规则

- Build 包不发布 CLR public API，也不得包含 `lib/` 资产；应用通过 `PrivateAssets=all` 与 `IncludeAssets=build;buildTransitive;analyzers` 消费构建能力。
- `buildTransitive/AtomUI.City.Build.contract.json` 是 MSBuild Property、Item、Target、默认值、可见性和 package asset 的机器可读兼容性基线。
- public MSBuild 名称、默认值、允许值、诊断码和 package path 默认视为兼容性承诺；infrastructure 名称不供开发者调用，但仍禁止无审阅漂移。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AtomUI.City.Build.props` 必须把 `IsTestProject` 暴露给 Analyzer，确保 `AUCANL0001` 不影响测试项目的独立 DI 装配。
- `AUCANL0001` 的 Root Provider 所有权约束覆盖 Microsoft Generic Host 构建/启动入口；生产 City 项目必须通过 City `ApplicationHost` 构建应用 Host。

## 1.0 Preview 动态发现属性撤回

MSBuild 属性 `AtomUICityAllowDynamicDiscovery` 被移除。该属性此前未连接 generator、Analyzer 或 runtime，设置任何值都不会改变构建结果；运行时动态发现未来必须作为独立 Feature 完整交付后才能重新公开。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
