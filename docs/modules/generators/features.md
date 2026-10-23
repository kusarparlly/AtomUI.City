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
| AUC-GENERATORS-009 | EventBus Contract and Handler Manifest | 已实现并通过产品合同测试 | Internal: EventMetadataReader, EventGenerationManifest; Generated Output: service registrar 中的 EventBus manifest | EventBusGeneratorTests; GeneratedSourceVisibilityTests |
| AUC-GENERATORS-010 | Data Client Descriptor Manifest | 已实现并通过产品合同测试 | Internal: DataClientMetadataReader, DataClientGenerationMetadata, DataClientRegistrarSourceBuilder; Generated Output: data client registrar | AtomUICityIncrementalGeneratorDataTests; GeneratedSourceVisibilityTests |
| AUC-GENERATORS-011 | ViewModel Validation Generation | Implemented and NativeAOT Verified | Internal validation metadata/manifest/source pipeline; Generated Output: private/protected validation implementation | AtomUICityIncrementalGeneratorMvvmTests; GeneratedValidationTests; NativeAOT Runtime Matrix |

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

## AUC-GENERATORS-009 EventBus Contract and Handler Manifest

Feature ID: `AUC-GENERATORS-009`
Status: 已实现并通过产品合同测试
Goal: 在编译期把 EventBus contract、channel 与 handler 声明转换为 AOT 友好的静态注册代码，运行时不扫描程序集发现事件元数据。
Internal Contract: EventMetadataReader, EventGenerationCandidate, EventGenerationManifest, GeneratedEventContractMetadata, GeneratedEventChannelMetadata, GeneratedEventHandlerMetadata
Generated Output Contract: EventBus 注册语句随当前程序集的 `GeneratedServiceRegistrar_*` 输出；存在事件清单时写入 `GeneratedEventManifestAttribute`，并生成 `EventContractDescriptor.GeneratedShared<TEvent>` 与 `GeneratedEventHandlerDescriptor.Create<TEvent, THandler>` 注册语句。引用程序集的 registrar 按稳定 registrar type 聚合且只聚合一次。
Runtime / Build Behavior: 读取 `EventContractAttribute`、`EventChannelAttribute`、`EventHandlerAttribute` 与 `IEventHandler<TEvent>`；验证 owner 位于声明程序集并实现 `IModule`，验证 contract id/schema/channel/handler policy、唯一 public constructor、共享事件闭合不可变对象图、跨程序集 generated manifest 可达性及当前 compilation 内 contract id 唯一性；输出按稳定键排序。
Failure Behavior: 非法 owner、重复 contract id/channel、未知 enum、非法容量/并发/超时、不可构造 handler、缺失或不可达的 generated contract、开放或可变共享对象图统一输出 `AUCGEN005`，且不得生成包含不完整 EventBus 注册的 service registrar。
Threading / Cancellation: 纯编译期 CPU；不启动线程或后台任务，Roslyn compilation 生命周期负责取消和失效。
Diagnostics: diagnostic category 使用 `AtomUI.City.Generators.EventBus`，定位到声明 syntax；当前非法 manifest 输入使用稳定码 `AUCGEN005`。
Tests: `EventBusGeneratorTests; GeneratedSourceVisibilityTests; GeneratorNamingTests`
Required Assertions: 断言 contract/channel/handler 全量元数据、重复 id、无 contract handler、跨程序集 generated manifest reachability、未生成 manifest 的引用拒绝、对象图 allowlist、registrar 单次聚合、可编译输出和 generated public surface 白名单。
Acceptance Criteria: EventBus attribute 声明能够生成确定、可编译、无运行时扫描的注册代码；任一非法声明必须在编译期阻断该批 EventBus 输出。

## AUC-GENERATORS-010 Data Client Descriptor Manifest

Feature ID: `AUC-GENERATORS-010`
Status: 已实现并通过产品合同测试
Goal: 在编译期把 Data client 与 operation 声明转换为强类型 descriptor registrar，运行时不扫描程序集发现 Data client。
Internal Contract: DataClientMetadataReader, DataClientGenerationMetadata, DataOperationGenerationMetadata, DataClientRegistrarSourceBuilder
Generated Output Contract: 为声明程序集生成稳定命名的 public sealed `GeneratedDataClientRegistrar_*`，实现 `IDataClientDescriptorRegistrar`；程序集通过 `GeneratedDataClientManifestAttribute` 指向 registrar；`Register` 向 `DataClientDescriptorCatalog` 注册按 client id 和 operation name 稳定排序的 descriptor。
Runtime / Build Behavior: 读取 `DataClientAttribute` 与 `DataOperationAttribute`，包含继承接口中的 operation；保留 transport、version、request/response type、access/concurrency policy、timeout、retry、cache 与 authentication policy；支持约定内的 `DataResult<T>`、异步包装和 `IDataStream<T>` 返回形态。
Failure Behavior: 非 public/泛型或嵌套于泛型类型的 client、空或重复 client id、未知 transport/policy、非法 operation 签名或返回值、重复 operation name、负超时/重试、Mutation cache、空认证策略统一输出 `AUCGEN005`，且该 compilation 不生成 Data registrar。
Threading / Cancellation: 纯编译期 CPU；不启动线程或后台任务，Roslyn compilation 生命周期负责取消和失效。
Diagnostics: diagnostic category 使用 `AtomUI.City.Generators.Data`，定位到 client 声明；当前非法 manifest 输入使用稳定码 `AUCGEN005`。
Tests: `AtomUICityIncrementalGeneratorDataTests; GeneratedSourceVisibilityTests; GeneratorNamingTests`
Required Assertions: 断言强类型 descriptor、manifest attribute、稳定 registrar/hint name、重复 client id、签名和 policy 拒绝、tuple type reference、泛型容器拒绝、继承 operation、可编译输出及 generated public surface 白名单。
Acceptance Criteria: Data attribute 声明能够生成确定、可编译、无运行时扫描的 descriptor registrar；任一非法声明必须在编译期阻断该批 Data 输出。

## AUC-GENERATORS-011 ViewModel Validation Generation

Feature ID: `AUC-GENERATORS-011`
Status: `Implemented and NativeAOT Verified`
Goal: 为 `ValidatingViewModelBase` 派生类生成无运行时反射的 DataAnnotations 验证实现。
Internal Contract: validation metadata reader、validation manifest builder、validation source builder；实现类型名称可在施工时确定，不构成开发者 SDK。
Generated Output Contract: 在开发者 `partial` ViewModel 中生成 private/protected property validation 分派和全量验证实现；不得生成新的 public 类型或成员。hint name、属性顺序和同一属性的规则顺序必须确定。
Runtime / Build Behavior: 编译期读取受支持标准 Attribute；`Compare` 生成直接属性访问；`CustomValidation` 解析为确定的静态方法调用；typed custom validator contract 在 API card 冻结后接入。生成代码通过 `ValidatingViewModelBase` 的受控 hook 原子替换字段错误。
Failure Behavior: 非 partial 派生类、未知 Attribute、非法 Attribute 参数、不可访问/歧义 `CustomValidation` 方法、开放泛型或无法生成的类型统一输出 `AUCGEN009` Error，并且不生成不完整验证实现。
Threading / Cancellation: 纯编译期 CPU，传递 Roslyn cancellation token，不使用全局 mutable cache。
Diagnostics: `AUCGEN009 UnsupportedGeneratedValidation`，category 为 `AtomUI.City.Generators.MvvmValidation`，定位到 Attribute、目标属性或非法方法参数中最精确的位置。
Tests: `AtomUICityIncrementalGeneratorMvvmTests; GeneratedValidationTests; GeneratorNamingTests; AtomUI.City.NativeAot.RuntimeMatrix`
Required Assertions: 覆盖受支持 Attribute、多个属性/规则稳定排序、Compare、CustomValidation、非法输入、增量输入隔离、可编译输出、public surface 白名单和双 TFM NativeAOT 原生验证行为。
Acceptance Criteria: 验证型 ViewModel 在 net8/net10 NativeAOT 下不产生本 Feature 引起的 IL2xxx/IL3xxx，运行时不枚举 Attribute、不按字符串反射方法且错误通知行为与 Mvvm 合同一致。
