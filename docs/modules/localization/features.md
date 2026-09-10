# AtomUI.City.Localization Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-LOCALIZATION-001 | Culture State and Fallback | Completed | CultureState, LocalizationOptions | CultureStateTests |
| AUC-LOCALIZATION-002 | Language Package Provider | Completed | ILanguagePackageProvider, LanguagePackageDescriptor, LanguagePackageRegistry | LanguagePackageProviderTests |
| AUC-LOCALIZATION-003 | Lazy Package Loading | Completed | LocalizationService, LanguagePackageLoadResult | LocalizationServiceTests |
| AUC-LOCALIZATION-004 | Lookup and Missing Key Fallback | Completed | LocalizedString, LocalizedText, LocalizationResult | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Assembly Language Packages | Completed | AssemblyLanguagePackageProvider, LanguagePackageAttribute | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-006 | Optional Application Refresh Hook | Completed | IPresentationLocalizationBridge, LocalizedTextChangedEventArgs | LocalizationServiceTests |
| AUC-LOCALIZATION-007 | Plugin Package Revocation | Completed | ILocalizationService, ResourceScope, LanguagePackageProviderKind | LocalizationServiceTests |
| AUC-LOCALIZATION-008 | Generated Localization Manifest | Completed | LanguagePackageAttribute, LocalizedResourceAttribute, generated manifest | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 语言包按当前 culture 和 fallback chain 懒加载，不在启动时全量加载。 | 必须有实现、测试或工程门禁证据。 |
| 每种语言自己的语言包独立缓存、独立诊断、独立撤销。 | 必须有实现、测试或工程门禁证据。 |
| 语言包可以来自 Host、模块、插件或独立 assembly，但都必须有 owner。 | 必须有实现、测试或工程门禁证据。 |
| 缺失 key 必须诊断并走 fallback，不允许静默返回空字符串。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-LOCALIZATION-001 Culture State and Fallback

Feature ID: `AUC-LOCALIZATION-001`
Status: Completed
Goal: 定义当前 culture、fallback 链和切换事务。
Public Contract: CultureState, LocalizationOptions, ILocalizationService.CultureState
Runtime / Build Behavior: SetCulture 递归计算 descriptor fallback graph，再追加当前 culture parent chain，并通过 City.State `IReadOnlyState<CultureState>` 原子发布深只读 snapshot；集合和 `CultureInfo` 与调用方输入隔离；fallback lazy load 同步更新 LoadedPackageIds；重复设置当前 culture 不重新加载 package 或递增 revision。
Failure Behavior: 非法 culture、任意长度 fallback cycle、当前 culture 重复设置必须稳定返回。
Threading / Cancellation: culture 切换可以异步加载包；状态提交必须一次性发布。
Diagnostics: culture diagnostics 必须包含 requested/effective culture 和 fallback chain。
Tests: `CultureStateTests`
Required Assertions: 断言 City.State 订阅、默认 culture、多节点 fallback 顺序、任意长度 cycle、非法 culture、LoadedPackageIds 和重复切换。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-002 Language Package Provider

Feature ID: `AUC-LOCALIZATION-002`
Status: Completed
Goal: 抽象文件、程序集和插件语言包来源。
Public Contract: ILanguagePackageProvider, LanguagePackageDescriptor, LanguagePackageRegistry
Runtime / Build Behavior: provider 声明 culture、scope、owner 和 lazy load 函数；registry 是 LocalizationService 的唯一 descriptor 来源并按 owner 管理 descriptors；identity 为 `(culture, packageId)`；`RegisterRange` 对整批 descriptor 原子预检并发布；File、Assembly、InMemory 均提供默认 provider，自定义同 kind provider 覆盖默认 provider，重复自定义 provider 明确拒绝。
Failure Behavior: provider kind 不匹配、格式错误、schema/version/id/culture/checksum/path-root 不匹配、重复 JSON 根属性、超过 16 MiB、取消、重复 package、owner 已撤销必须返回稳定 Result；批量注册失败不得留下部分 descriptor。
Threading / Cancellation: load 支持 CancellationToken；取消后不得缓存 partial package。
Diagnostics: provider diagnostics 必须包含 provider kind、assembly/path、culture 和 scope。
Tests: `LanguagePackageProviderTests`
Required Assertions: 断言 provider 注册与 kind mismatch、原子批量注册、相同 culture/package id 重复拒绝、跨 culture 同 package id、取消、16 MiB 上限、重复根属性、格式错误、动态注册进入 lookup 和 owner revoke 后 fallback。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-003 Lazy Package Loading

Feature ID: `AUC-LOCALIZATION-003`
Status: Completed
Goal: 只按当前 culture 和 fallback chain 懒加载所需语言包。
Public Contract: LocalizationService, LanguagePackageLoadResult
Runtime / Build Behavior: 第一次 lookup 或 culture 切换触发 package load；cache key 包含 culture 和 package id；不同 culture 有独立缓存。
Failure Behavior: load 失败诊断后跳过该 provider，继续 fallback；不能阻塞整个应用启动。
Threading / Cancellation: 同一 culture/package 并发 load 合并为一个 service-owned task；单个 waiter 取消不污染共享 load，失败或已撤销结果不写入共享缓存；Service Dispose 取消并释放全部 load/cache。
Diagnostics: lazy-load diagnostics 必须包含 package id、culture、attempt 和 elapsed。
Tests: `LocalizationServiceTests`
Required Assertions: 断言按需加载、并发合并、失败 fallback、不同 culture 独立缓存。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-004 Lookup and Missing Key Fallback

Feature ID: `AUC-LOCALIZATION-004`
Status: Completed
Goal: 提供稳定文本查找、活动作用域隔离、缺失 key 行为和订阅式 LocalizedText。
Public Contract: ILocalizationService, LocalizationLookupContext, ILocalizationScopeLease, LocalizedString, LocalizedText, LocalizationResult
Runtime / Build Behavior: Host/Presentation 是全局 scope；Module/Plugin/Route/Window descriptor 必须声明 ScopeId，并且只有 id 同时匹配 lookup context 且存在活动 lease 时才参与查找。无 context API 只查全局 scope。每次 lookup 根据其可见 descriptor 计算 fallback chain，不能继承其他活动 scope 的 fallback；culture switch 只预加载全局和当前活动 scope；同一 scope 内保持 Host 注册顺序；LocalizedText 捕获创建时 context 并在 culture change 后更新。
Failure Behavior: 缺失 key 返回 missing marker 并记录诊断；任何格式化执行异常均返回 raw template 并记录格式化诊断。
Threading / Cancellation: lookup 允许并发读取；每个 LocalizedText 的 refresh/notification 严格 FIFO，只发布稳定 culture revision；handler 失败被隔离，Dispose 返回后不再开始 callback。
Diagnostics: missing-key diagnostics 必须包含 key 和 culture；format diagnostics 必须包含 key、culture 和 error kind。
Tests: `LocalizationServiceTests`
Required Assertions: 断言 inactive scope 不加载、lease 激活/释放、context 隔离、动态 scoped fallback、其他活动 scope 不污染 lookup、缺失 key、任意 formatter 异常 raw fallback 和订阅更新。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-005 Assembly Language Packages

Feature ID: `AUC-LOCALIZATION-005`
Status: Completed
Goal: 支持语言包放在独立 assembly 并运行时加载。
Public Contract: AssemblyLanguagePackageProvider, LanguagePackageAttribute, LocalizedResourceAttribute
Runtime / Build Behavior: assembly provider 通过 `Discover(Assembly)` 读取 `LanguagePackageAttribute` 并生成 assembly descriptor；descriptor 记录原 assembly 的 AssemblyLoadContext、path、resource base name、fallback culture、version、checksum 和 contribution id，随后在同一 ALC 加载 embedded locpack，禁止把 collectible plugin package 加入 Default ALC。
Failure Behavior: assembly load 失败、资源缺失和 locpack 格式错误只影响该 package；owner revoke 后相同 owner 不可重新注册已发现 descriptor。
Threading / Cancellation: assembly load 可以在后台线程；加载完成后通过 service 发布。
Diagnostics: assembly package diagnostics 必须包含 assembly name、resource name 和 culture。
Tests: `LanguagePackageProviderTests; LocalizationDeclarationAttributeTests`
Required Assertions: 断言独立 assembly、属性声明、资源读取、缺失资源和 unload owner。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-006 Optional Application Refresh Hook

Feature ID: `AUC-LOCALIZATION-006`
Status: Completed
Goal: culture 提交后通知可选的应用组合层或独立 UI 适配包，并刷新本地文本句柄。
Public Contract: IPresentationLocalizationBridge, LocalizedTextChangedEventArgs
Runtime / Build Behavior: Localization 在语言包加载成功后提交 `CultureState`，调用可选 `IPresentationLocalizationBridge`，随后刷新已注册 `ILocalizedText`。默认 bridge 是 no-op；实现者是应用或独立适配包，不是 `AtomUI.City.Presentation`。
Failure Behavior: 可选 bridge 失败不能回滚 culture state；失败 result 返回给调用方，本地文本刷新继续执行。
Threading / Cancellation: 调用方取消仅在 culture state 提交前生效；提交后 bridge 与文本刷新由 service lifetime token 完成本次事务。Localization 不引用 Avalonia，也不保证 UI 线程；适配实现自行 dispatch。
Diagnostics: bridge diagnostics 必须包含 culture 和 error kind；具体 UI target 的诊断由应用或独立适配包负责。
Tests: `LocalizationServiceTests`
Required Assertions: 断言 bridge 调用、局部失败、批量刷新和不依赖 Avalonia 类型。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-007 Plugin Package Revocation

Feature ID: `AUC-LOCALIZATION-007`
Status: Completed
Goal: 插件卸载时撤销语言包 descriptor 和 provider cache，并刷新仍由调用方持有的文本句柄。
Public Contract: ILocalizationService, ResourceScope, LanguagePackageProviderKind
Runtime / Build Behavior: `RevokePackagesByContributionIdAsync` 按 contribution id 移除 descriptor、清理 loaded package cache、更新 `CultureState.LoadedPackageIds` 并刷新已注册 `ILocalizedText`。
Failure Behavior: 重复 revoke 返回 0 且不递增 revision；撤销后 lookup 不再返回插件资源；撤销期间完成的 load 返回 `ResourceRevoked` 并立即释放 package。
Threading / Cancellation: unload 与 lookup/culture switch 并发时，commit 重验 descriptor；撤销与 culture switch 共用 mutation queue，Registry 直接撤销也不能复活 cache/state；撤销提交后 bridge 与文本刷新由 service lifetime token 完成。
Diagnostics: plugin localization diagnostics 必须包含 contribution id、culture、revoked package count 和 `ResourceRevoked` error kind。
Tests: `LocalizationServiceTests`
Required Assertions: 断言撤销后不可 lookup、在途 load 不发布、订阅释放、重复 revoke 和 mutation 重入快速失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-008 Generated Localization Manifest

Feature ID: `AUC-LOCALIZATION-008`
Status: Completed
Goal: 在编译期生成稳定的 culture/resource manifest、强类型 key 常量和无反射 package 注册入口。
Public Contract: LanguagePackageAttribute, LocalizedResourceAttribute, generated `GeneratedLocalizationManifest`
Runtime / Build Behavior: Incremental Generator 主入口读取 assembly attributes，经 `LocalizationManifestBuilder` 校验后生成稳定排序的 `SupportedCultures`、`ResourceKeys`、`Keys` 和 `RegisterPackages`；registrar 使用一次 `RegisterRange` 原子发布全部 descriptor；生成的 descriptor 保留 scope id、fallback、version、checksum、contribution id、critical keys 和声明 assembly 的 load context；culture 规范化后 package identity 为 `(culture, packageId)`。
Failure Behavior: attribute 必填字符串为空、invalid culture、未知 enum、缺失或歧义 package、重复 identity/key、scope 不匹配、缺失 scoped id/resource base name、不完整或成环 fallback，以及无运行时合同的 resource kind 产生 build error，且不生成 Localization source。
Threading / Cancellation: 纯编译期确定性处理；运行时注册是同步操作。
Diagnostics: 使用 Generator 稳定诊断码并归类到 Localization feature。
Tests: `AtomUICityIncrementalGeneratorLocalizationTests; LocalizationMetadataReaderTests; LocalizationManifestBuilderTests`
Required Assertions: 断言主 Generator 确实输出可编译 source、原子注册入口、culture、key、scope id、contribution id 和 critical keys；空 attribute 参数与显式未知 enum 阻断生成。
Acceptance Criteria: 生成输出可直接注册到 `LanguagePackageRegistry`，不依赖运行时程序集扫描。
