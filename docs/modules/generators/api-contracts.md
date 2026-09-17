# AtomUI.City.Generators API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块不提供第三方 Generator SDK。Public surface 只包含 Roslyn 从 analyzer 程序集外部发现并激活的 `AtomUICityIncrementalGenerator` 与 `BuildServiceProviderUsageAnalyzer`；它们属于 `Preview` Tooling Entry，不是应用开发者扩展点。diagnostic id、generated name、manifest shape 和生成行为是外部可观察合同，Reader、Metadata、Manifest、Builder、SourceBuilder 与诊断辅助模型均为 `InternalContract`。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Tooling Entry | AtomUICityIncrementalGenerator, BuildServiceProviderUsageAnalyzer | Roslyn incremental generator 与 analyzer 激活入口。 | 必须能从 analyzer 包发现并实例化；不引用运行时包；不构成第三方扩展 SDK。 |
| Metadata Readers | ModuleMetadataReader, ServiceRegistrationMetadataReader, RouteMetadataReader, PluginMetadataReader, LocalizationMetadataReader, PresentationViewMetadataReader | 从 syntax/semantic model 读取声明。 | reader 不做业务生成；非法声明输出 diagnostic metadata；`LocalizationMetadata.Diagnostics` 保存 attribute 读取阶段错误。 |
| Manifest Builders | ModuleDependencyGraphBuilder, ServiceRegistrationManifestBuilder, RouteManifestBuilder, PluginManifestBuilder, LocalizationManifestBuilder, PresentationViewManifestBuilder | 校验 metadata 并生成 manifest result。 | result 不可变；排序确定；失败不生成不完整 success manifest。 |
| Source Builders | ModuleRegistrarSourceBuilder, ServiceRegistrarSourceBuilder, DataClientRegistrarSourceBuilder, RouteSourceBuilder, LocalizationRegistrarSourceBuilder, PresentationViewRegistrarSourceBuilder | 生成 C# 注册和强类型入口代码。 | hint name 稳定；生成代码不依赖 runtime reflection；公开声明只能来自 reviewed visibility allowlist；Localization registrar 通过单次 `RegisterRange` 原子注册 manifest。 |
| Diagnostics | GeneratorDiagnosticIds, GeneratorDiagnostics, GeneratorDiagnosticDefinition | 编译期诊断定义和创建。 | diagnostic id、severity、category 和 message args 稳定。 |

## Generated Source 可见性合同

生成器实现类型的 `internal` 不自动意味着生成结果也是内部实现。生成源码被编译进声明方程序集，因此每一项 `public` 生成声明都必须属于以下经过 review 的公开面；除此之外不得新增 `public` 类型或成员：

| Generated surface | 可见性 | 原因 |
| --- | --- | --- |
| Module registrar 与 `Register` | `public` | 引用方程序集的 generated registrar 必须能够静态串联依赖程序集 registrar；应用代码不直接调用。 |
| Service registrar 与 `Register` | `public` | 引用方程序集必须能够按 registrar type 聚合依赖程序集服务；应用代码不直接调用。 |
| Data client registrar 与 `Register` | `public` | 应用或组合层通过 `RegisterGenerated<TRegistrar>` 显式装载声明程序集 descriptor。 |
| 声明方 Route Map partial 类型和方法 | 保持用户声明的 `public` | Generator 只补齐用户定义的强类型 route API。 |
| `GeneratedRoutingRouteManifest` | `public` | 应用或组合层显式取得 route descriptors/snapshot。 |
| `GeneratedLocalizationManifest` 及其 manifest、key、registration 成员 | `public` | 应用与模块代码显式读取强类型 key/manifest 并注册 package。 |
| `GeneratedPresentationViewRegistrar` 与 `RegisterViews` | `public` | 应用或组合层显式把声明程序集的 View 注册到目标 registry。 |

生成源码中的 helper、backing key 和仅供单个生成类型使用的实现方法必须保持 `private`。Generator contract tests 必须解析各 SourceBuilder 的输出并对 `public` declaration 使用精确白名单；仅验证生成源码能够编译不足以阻止公开面意外扩大。

## Presentation View Metadata 合同

- `PresentationViewMetadata.HasAmbiguousConstructors` 表示 View 类型存在多个同最大参数数量的 public constructor。
- `PresentationViewMetadataReader` 必须用确定性构造签名排序选择参数列表，并保留 ambiguity 标记。
- `PresentationViewManifestBuilder` 必须拒绝 ambiguity 标记为 true 的 View metadata，输出 stable diagnostic，且不得生成 registrar entry。
- `PresentationViewRegistrarSourceBuilder` 必须把 constructor parameter metadata 传入 `ViewDescriptor.ConstructorParameterTypes`。

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| AtomUICityIncrementalGenerator.Initialize | 注册 incremental pipeline。 | IncrementalGeneratorInitializationContext。 | void。 | 初始化不得抛出非 Roslyn 管控异常；feature pipeline 错误输出 diagnostic。 | 由编译器控制。 | Initialize 可被编译器多次调用，注册必须无全局可变状态。 |
| MetadataReader.Read | 从声明读取 metadata。 | syntax node、semantic model、cancellation token。 | metadata 或 diagnostic result。 | symbol 缺失、attribute 参数非法、类型不可访问输出 diagnostic。 | 必须传递 Roslyn token。 | reader 必须无共享 mutable cache。 |
| ManifestBuilder.Build | 校验 metadata 并生成 manifest。 | metadata collection。 | manifest result。 | 冲突、重复、循环、缺失依赖返回 failed result 和 diagnostics。 | 纯 CPU，批量 build 应观察 token。 | 同一输入输出 byte-stable。 |
| LocalizationMetadataReader.Read | 读取 assembly-level Localization attributes。 | Compilation。 | `LocalizationMetadata`，含 packages、resources 和 reader diagnostics。 | 空 package id/culture/resource key/package reference 产生 diagnostic metadata，不得静默丢弃声明；显式未知 enum 原值交由 manifest builder 拒绝。 | 由 CompilationProvider 控制。 | 无共享 mutable cache；有 reader diagnostic 时主 pipeline 不生成 Localization source。 |
| LocalizationRegistrarSourceBuilder.Build | 生成 Localization manifest、key 和 registrar source。 | 成功的 LocalizationManifest。 | C# source text。 | failed manifest 不得调用；运行时任一注册冲突由生成入口抛 `InvalidOperationException`，Registry 保持整批未发布。 | 纯 CPU。 | source ordering 稳定；registrar 单次调用 `RegisterRange`。 |
| PresentationViewRegistrarSourceBuilder.Build | 生成 view registrar source。 | PresentationViewManifest。 | Generated source text 和 hint name。 | manifest failed 时不得生成 registrar source。 | 纯 CPU。 | hint name 和 source ordering 稳定。 |
| GeneratorDiagnostics.CreateRoslynDiagnostic | 创建 Roslyn diagnostic。 | feature、generator diagnostic、location、message args。 | Diagnostic。 | 参数数量不匹配按 Roslyn Diagnostic 格式化规则稳定失败。 | 同步 API 无 token。 | diagnostic definition 不可变，可并发读取。 |

## Tooling Entry 与 Internal Pipeline 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AtomUICityIncrementalGenerator` | Public Tooling Entry | Roslyn 激活入口；新增、删除、重命名或激活行为变化必须更新本文档和 compatibility。 |
| `BuildServiceProviderUsageAnalyzer` | Public Tooling Entry | Roslyn 激活入口；新增、删除、重命名或激活行为变化必须更新本文档和 compatibility。 |

以下类型是可白盒测试的生成器内部流水线，不属于开发者 Public API；其对外可观察输出仍受上文合同约束。

### Internal Pipeline 类型

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `GeneratedCodeNames` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratedTypeName` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorFeature` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorFeatureNames` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationLifetime` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifest` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifestBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifestResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnostic` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticDefinition` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticIds` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticSeverity` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnostics` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `CultureFallbackManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifest` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifestBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifestResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizationRegistrarSourceBuilder` | InternalContract | 生成类型名、成员、排序或原子注册行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceMetadataKind` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ResourceScopeMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyGraphBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyGraphResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ModuleMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `ModuleMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginContributionManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginContributionManifestMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginManifest` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginManifestBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginManifestResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PluginMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewConstructorParameter` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifest` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestEntry` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `PresentationViewRegistrarSourceBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionMetadataKind` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteManifest` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteManifestBuilder` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteManifestResult` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteManifestRoute` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteMapMetadata` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |
| `RouteMetadataReader` | InternalContract | 实现可演进；外部可观察行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
