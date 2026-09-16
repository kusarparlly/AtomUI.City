# AtomUI.City.Generators Compatibility

## 兼容性范围

本模块兼容面包括 Roslyn Tooling Entry、diagnostics code、manifest/schema、generated output 以及构建集成中实际适用的部分。Reader、Metadata、Manifest、Builder、SourceBuilder 与诊断辅助模型是 `InternalContract`，不提供第三方 Generator SDK。

## 模块兼容性硬边界

- Generator target 为 netstandard2.0 并作为 analyzer 分发。
- Generator 不引用 AtomUI.City 运行时包。
- 输出确定性排序。
- 诊断 id 稳定。

## API 兼容规则

- `AtomUICityIncrementalGenerator` 与 `BuildServiceProviderUsageAnalyzer` 是 Public Tooling Entry；其 Roslyn 发现、实例化和执行行为属于兼容性承诺。
- Internal Pipeline 类型及其成员不形成二进制兼容承诺，可以在保持生成结果、诊断和 schema 合同的前提下演进。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `PresentationViewMetadata.HasAmbiguousConstructors` 是 1.0 前新增 metadata 成员；后续不能删除或改为允许 ambiguous registrar generation。
- generated presentation view registrar 输出 `constructorParameterTypes` 传递属于 1.0 兼容 contract。
- `GeneratorDiagnostics.CreateRoslynDiagnostic` 是 1.0 前新增 diagnostic factory；category、severity、message formatting 和 location fallback 属于兼容行为。
- `LocalizationMetadata.Diagnostics`、空 attribute 参数/显式未知 enum 的 build diagnostic，以及有 reader diagnostic 时不生成 Localization source 的行为进入 1.0 兼容承诺。
- `LocalizationRegistrarSourceBuilder` 生成的 `GeneratedLocalizationManifest` 成员、稳定排序和单次 `LanguagePackageRegistry.RegisterRange` 原子注册行为进入 1.0 兼容承诺。
- `AUCANL0001` 默认以 Error 阻止非测试 City 项目调用或引用 `BuildServiceProvider`、主动调用 `IServiceProviderFactory<T>.CreateServiceProvider`，以及调用或引用 Microsoft Generic Host 构建/启动入口；诊断 ID、默认 severity、City ApplicationHost、已有 IHost 启动、测试项目和 generated code 豁免属于兼容行为。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
