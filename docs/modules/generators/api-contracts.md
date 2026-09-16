# AtomUI.City.Generators API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块当前全部 public 类型、diagnostic、generated name 和 manifest shape 均为 `Preview`。`PublicAPI.Unshipped.txt` 仅用于在 API 边界审计期间阻止静默变化；它不决定某个 Roslyn reader/builder 是开发者 API 还是 `InternalContract`，该归类必须经过单独审查后写入本文。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Incremental Entry | AtomUICityIncrementalGenerator, GeneratorFeature, GeneratorFeatureNames | Roslyn incremental generator 入口和 feature 选择。 | 不引用运行时包；输出稳定；无关输入不触发无关输出变化。 |
| Metadata Readers | ModuleMetadataReader, ServiceRegistrationMetadataReader, RouteMetadataReader, PluginMetadataReader, LocalizationMetadataReader, PresentationViewMetadataReader | 从 syntax/semantic model 读取声明。 | reader 不做业务生成；非法声明输出 diagnostic metadata；`LocalizationMetadata.Diagnostics` 保存 attribute 读取阶段错误。 |
| Manifest Builders | ModuleDependencyGraphBuilder, ServiceRegistrationManifestBuilder, RouteManifestBuilder, PluginManifestBuilder, LocalizationManifestBuilder, PresentationViewManifestBuilder | 校验 metadata 并生成 manifest result。 | result 不可变；排序确定；失败不生成不完整 success manifest。 |
| Source Builders | LocalizationRegistrarSourceBuilder, PresentationViewRegistrarSourceBuilder | 生成 C# 注册代码。 | hint name 稳定；生成代码不依赖 runtime reflection；Localization registrar 通过单次 `RegisterRange` 原子注册 manifest。 |
| Diagnostics | GeneratorDiagnosticIds, GeneratorDiagnostics, GeneratorDiagnosticDefinition | 编译期诊断定义和创建。 | diagnostic id、severity、category 和 message args 稳定。 |

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

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AtomUICityIncrementalGenerator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratedCodeNames` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratedTypeName` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorFeature` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorFeatureNames` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationLifetime` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationManifestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnostic` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticDefinition` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticIds` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratorDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CultureFallbackManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationManifestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationRegistrarSourceBuilder` | 支持类型 | 生成类型名、成员、排序或原子注册行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceMetadataKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ResourceScopeMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyGraphBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyGraphResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginContributionManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginContributionManifestMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewConstructorParameter` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewManifestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationViewRegistrarSourceBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionMetadataKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteManifest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteManifestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteManifestRoute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMapMetadata` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMetadataReader` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
