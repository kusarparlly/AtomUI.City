# AtomUI.City.Generators Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Generator target 为 `netstandard2.0` 并作为 analyzer 分发。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| Generator 不引用 AtomUI.City 运行时包。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 输出确定性排序。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| Generated source 只能公开经过 review 的类型和成员。 | 必须解析全部 SourceBuilder 输出并与 public declaration 白名单精确比较；新增公开声明必须先更新 API contract。 |
| 诊断 id 稳定，不能复用。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-GENERATORS-001 | Generator | IncrementalGeneratorInfrastructureTests; GeneratedSourceVisibilityTests | 断言 incremental 输入隔离、hint name 稳定、无 runtime 依赖，并精确限制 generated public declarations。 | 无关输入导致全量重算、hint name 不稳定、runtime dependency 或未审阅的 generated public declaration 出现必须失败。 | Implemented |
| AUC-GENERATORS-002 | Generator | ModuleDependencyGraphBuilderTests; ModuleMetadataReaderTests | 断言 DependsOn 图、循环诊断、默认 module id。 | 循环依赖、重复 module、缺失依赖输出 diagnostic。 | Implemented |
| AUC-GENERATORS-003 | Generator | ServiceRegistrationManifestBuilderTests; ServiceRegistrationMetadataReaderTests | 断言 lifetime、ExposeServices、显式注册和冲突诊断。 | lifetime 冲突、重复服务、不可构造类型输出 diagnostic。 | Implemented |
| AUC-GENERATORS-004 | Generator | RouteManifestBuilderTests; RouteMetadataReaderTests | 断言 route attribute、template、target、排序和诊断。 | 模板非法、route 冲突、target 缺失输出 diagnostic。 | Implemented |
| AUC-GENERATORS-005 | Generator | PluginManifestBuilderTests; PluginMetadataReaderTests | 断言 plugin metadata、capability、dependency、contribution。 | metadata 缺失、dependency 格式错误、重复 capability 输出 diagnostic。 | Implemented |
| AUC-GENERATORS-006 | Generator | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests; LocalizationMetadataReaderTests | 断言 culture、resource、fallback、稳定 key、单次 `RegisterRange` 原子 registrar 和重复 key 诊断。 | 空 attribute 参数、culture 非法、显式未知 enum、重复 key、resource 缺失输出 diagnostic 且不生成 Localization source。 | Implemented |
| AUC-GENERATORS-007 | Generator | PresentationViewManifestBuilderTests; PresentationViewRegistrarSourceBuilderTests | 断言 ViewFor、constructor、registrar source 和诊断。 | 构造函数不明确、ViewModel 缺失、重复 mapping 输出 diagnostic。 | Implemented |
| AUC-GENERATORS-008 | Generator | GeneratorDiagnosticTests | 断言 diagnostic id、severity、message args 和 source location。 | id 复用、severity 漂移、缺少 location 必须测试失败。 | Implemented |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
