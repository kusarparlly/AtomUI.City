# AtomUI.City.Generators Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Contract Boundary | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-GENERATORS-001 | Incremental Infrastructure | 已实现并通过产品合同测试 | Public Tooling Entry: AtomUICityIncrementalGenerator; Internal: GeneratorFeature; Generated public surface allowlist | IncrementalGeneratorInfrastructureTests; GeneratedSourceVisibilityTests |
| AUC-GENERATORS-002 | Module Graph | 已实现并通过产品合同测试 | Internal: ModuleMetadataReader, ModuleDependencyGraphBuilder | ModuleDependencyGraphBuilderTests; ModuleMetadataReaderTests |
| AUC-GENERATORS-003 | DI Manifest | 已实现并通过产品合同测试 | Internal: ServiceRegistrationMetadataReader, ServiceRegistrationManifestBuilder | ServiceRegistrationManifestBuilderTests; ServiceRegistrationMetadataReaderTests |
| AUC-GENERATORS-004 | Route Manifest | 已实现并通过产品合同测试 | Internal: RouteMetadataReader, RouteManifestBuilder | RouteManifestBuilderTests; RouteMetadataReaderTests |
| AUC-GENERATORS-005 | Plugin Manifest | 已实现并通过产品合同测试 | Internal: PluginMetadataReader, PluginManifestBuilder | PluginManifestBuilderTests; PluginMetadataReaderTests |
| AUC-GENERATORS-006 | Localization Manifest | 已实现并通过产品合同测试 | Internal: LocalizationMetadataReader, LocalizationManifestBuilder, LocalizationRegistrarSourceBuilder | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests; LocalizationMetadataReaderTests |
| AUC-GENERATORS-007 | Presentation View Manifest | 已实现并通过产品合同测试 | Internal: PresentationViewMetadataReader, PresentationViewManifestBuilder | PresentationViewManifestBuilderTests; PresentationViewRegistrarSourceBuilderTests |
| AUC-GENERATORS-008 | Diagnostics | 已实现并通过产品合同测试 | Public Tooling Entry: BuildServiceProviderUsageAnalyzer; Internal: GeneratorDiagnosticIds, GeneratorDiagnostics | GeneratorDiagnosticTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Generator target 为 `netstandard2.0` 并作为 analyzer 分发。 | 必须有实现、测试或工程门禁证据。 |
| Generator 不引用 AtomUI.City 运行时包。 | 必须有实现、测试或工程门禁证据。 |
| 输出确定性排序。 | 必须有实现、测试或工程门禁证据。 |
| Generated source 不得产生未审阅的 `public` 类型或成员。 | 必须解析所有 SourceBuilder 的代表性输出并与精确白名单比较。 |
| 诊断 id 稳定，不能复用。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-GENERATORS-001 Incremental Infrastructure

Feature ID: `AUC-GENERATORS-001`
Status: 已实现并通过产品合同测试
Goal: 提供 Roslyn incremental generator 入口和 feature pipeline。
Tooling Entry: AtomUICityIncrementalGenerator; Internal Contract: GeneratorFeature
Runtime / Build Behavior: 按 feature 组合 syntax provider、metadata reader、builder 和 source output；generated public declaration 仅允许 API contract 已 review 的跨程序集或应用组合入口。
Failure Behavior: 无关输入导致全量重算、hint name 不稳定、runtime dependency 或未审阅的 generated public declaration 出现必须失败。
Threading / Cancellation: generator 由编译器取消；pipeline 不启动长时后台任务。
Diagnostics: diagnostic 必须包含 feature name 和 source location。
Tests: `IncrementalGeneratorInfrastructureTests; GeneratedSourceVisibilityTests`
Required Assertions: 断言 incremental 输入隔离、hint name 稳定、无 runtime 依赖，并精确比较六类 SourceBuilder 的 generated public declaration 白名单。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-002 Module Graph

Feature ID: `AUC-GENERATORS-002`
Status: 已实现并通过产品合同测试
Goal: 编译期建立模块依赖图。
Internal Contract: ModuleMetadataReader, ModuleDependencyGraphBuilder
Runtime / Build Behavior: 读取 Module/DependsOn metadata，默认 module id 为类型全名，输出拓扑排序。
Failure Behavior: 循环依赖、重复 module、缺失依赖输出 diagnostic。
Threading / Cancellation: 纯编译期 CPU；不访问文件系统。
Diagnostics: diagnostic 必须包含 cycle path 和 module type。
Tests: `ModuleDependencyGraphBuilderTests; ModuleMetadataReaderTests`
Required Assertions: 断言 DependsOn 图、循环诊断、默认 module id。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-003 DI Manifest

Feature ID: `AUC-GENERATORS-003`
Status: 已实现并通过产品合同测试
Goal: 编译期生成 AOT 友好的服务注册 manifest。
Internal Contract: ServiceRegistrationMetadataReader, ServiceRegistrationManifestBuilder
Runtime / Build Behavior: 读取 ServiceAttribute、lifetime marker、ExposeServices，输出稳定服务注册清单。
Failure Behavior: lifetime 冲突、重复服务、不可构造类型输出 diagnostic。
Threading / Cancellation: 纯编译期 CPU。
Diagnostics: diagnostic 必须包含 service type、implementation type、lifetime。
Tests: `ServiceRegistrationManifestBuilderTests; ServiceRegistrationMetadataReaderTests`
Required Assertions: 断言 lifetime、ExposeServices、显式注册和冲突诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-004 Route Manifest

Feature ID: `AUC-GENERATORS-004`
Status: 已实现并通过产品合同测试
Goal: 编译期生成 route manifest。
Internal Contract: RouteMetadataReader, RouteManifestBuilder
Runtime / Build Behavior: 读取 Route/RouteMap/Layout/Index/Redirect 属性，输出 route descriptor 和 ViewModel target metadata。
Failure Behavior: 模板非法、route 冲突、target 缺失输出 diagnostic。
Threading / Cancellation: 纯编译期 CPU。
Diagnostics: diagnostic 必须包含 route pattern、declaring type、segment。
Tests: `RouteManifestBuilderTests; RouteMetadataReaderTests`
Required Assertions: 断言 route attribute、template、target、排序和诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-005 Plugin Manifest

Feature ID: `AUC-GENERATORS-005`
Status: 已实现并通过产品合同测试
Goal: 编译期生成插件 manifest。
Internal Contract: PluginMetadataReader, PluginManifestBuilder
Runtime / Build Behavior: 读取 Plugin、capability、dependency、contribution 属性，输出 plugin manifest model。
Failure Behavior: metadata 缺失、dependency 格式错误、重复 capability 输出 diagnostic。
Threading / Cancellation: 纯编译期 CPU。
Diagnostics: diagnostic 必须包含 plugin id、field、declaring type。
Tests: `PluginManifestBuilderTests; PluginMetadataReaderTests`
Required Assertions: 断言 plugin metadata、capability、dependency、contribution。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-006 Localization Manifest

Feature ID: `AUC-GENERATORS-006`
Status: 已实现并通过产品合同测试
Goal: 编译期生成 localization resource manifest。
Internal Contract: LocalizationMetadataReader, LocalizationMetadata.Diagnostics, LocalizationManifestBuilder, LocalizationRegistrarSourceBuilder
Runtime / Build Behavior: 读取 LanguagePackage、LocalizedResource 和 fallback metadata，输出 culture/resource 清单、稳定 key 与通过单次 `RegisterRange` 原子注册的 registrar source。
Failure Behavior: 空 attribute 参数、culture 非法、显式未知 enum、重复 key、resource 缺失输出 diagnostic；reader 或 builder 有错误时不得生成 Localization source。
Threading / Cancellation: additional files 读取由 Roslyn 输入提供。
Diagnostics: diagnostic 必须包含 culture、resource key、scope。
Tests: `AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests; LocalizationMetadataReaderTests`
Required Assertions: 断言 culture、resource、fallback、原子 registrar、空 attribute 参数、未知 enum 和重复 key 诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-007 Presentation View Manifest

Feature ID: `AUC-GENERATORS-007`
Status: 已实现并通过产品合同测试
Goal: 编译期生成 ViewModel -> View 注册代码。
Internal Contract: PresentationViewMetadataReader, PresentationViewManifestBuilder, PresentationViewRegistrarSourceBuilder
Runtime / Build Behavior: 读取 ViewFor metadata，验证 View 构造函数，生成 registrar source。
Failure Behavior: 构造函数不明确、ViewModel 缺失、重复 mapping 输出 diagnostic。
Threading / Cancellation: 纯编译期 CPU。
Diagnostics: diagnostic 必须包含 view type、ViewModel type、constructor。
Tests: `PresentationViewManifestBuilderTests; PresentationViewRegistrarSourceBuilderTests`
Required Assertions: 断言 ViewFor、constructor、registrar source 和诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-GENERATORS-008 Diagnostics

Feature ID: `AUC-GENERATORS-008`
Status: 已实现并通过产品合同测试
Goal: 统一 generator diagnostic 定义和输出。
Tooling Entry: BuildServiceProviderUsageAnalyzer; Internal Contract: GeneratorDiagnosticIds, GeneratorDiagnostics
Runtime / Build Behavior: 每个 diagnostic 有 stable id、severity、message args 和 category。
Failure Behavior: id 复用、severity 漂移、缺少 location 必须测试失败。
Threading / Cancellation: 诊断创建无副作用。
Diagnostics: diagnostic 定义自身必须可枚举验证。
Tests: `GeneratorDiagnosticTests`
Required Assertions: 断言 diagnostic id、severity、message args 和 source location。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
