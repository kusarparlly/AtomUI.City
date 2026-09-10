# AtomUI.City.Localization API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Culture | CultureState, LocalizationOptions, IReadOnlyState<CultureState> | 当前 culture 和 fallback 链。 | 状态通过 City.State 原子发布；集合和 `CultureInfo` 均以只读快照发布；fallback descriptor 支持多节点递归并拒绝任意长度的环；重复设置当前 culture 幂等。 |
| Package Provider | ILanguagePackageProvider, LanguagePackageDescriptor, LanguagePackageRegistry | 语言包来源。 | Registry 是运行时 descriptor 的唯一来源；package identity 是 `(culture, packageId)`；File/Assembly/InMemory 均有默认 provider；registry 必须绑定 owner，并在 owner revoke 后拒绝新贡献；批量注册必须全有或全无。 |
| Lookup | ILocalizationService, LocalizedString, LocalizedMessage, LocalizedText | 文本查找、参数格式化和订阅更新。 | descriptor scope priority 稳定；缺失 key 和格式化失败必须诊断。 |
| Assembly Package | AssemblyLanguagePackageProvider, LanguagePackageAttribute | 独立 assembly 语言包。 | `Discover(Assembly)` 从 assembly 属性声明生成 descriptor；运行时按 culture 懒加载 embedded locpack。 |
| Optional Application Bridge | IPresentationLocalizationBridge | culture commit 后通知应用或独立 UI 适配包。 | 默认 no-op；Localization 不操作 VisualTree，Presentation 主模块不负责实现。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ILocalizationService.SetCultureAsync | 切换当前 culture。 | culture 不得为空；fallback 来自 descriptor 与 `LocalizationOptions`。 | LocalizationResult。 | 非法 culture、fallback cycle、load 部分失败按 result/diagnostics 表达；Presentation bridge 失败不回滚已提交 state，返回失败 result 并继续本地文本刷新；mutation callback 重入返回 `ReentrantOperation`。 | 提交前观察调用方 token，取消不得发布新 state；state 提交是不可逆边界，提交后的 bridge 与文本刷新改由 service lifetime token 驱动并完成本次事务。 | 与 revoke 共用 FIFO mutation queue；不在锁内调用 provider/bridge/handler；重复设置当前 culture 不重新加载 package 或递增 revision。 |
| ILanguagePackageProvider.LoadAsync | 加载指定 culture 的语言包。 | descriptor、cancellationToken。File descriptor 必须声明 AllowedRootPath；locpack 必须显式声明整数 `schemaVersion: 1`，且 UTF-8 JSON 总大小不得超过 16 MiB。 | LanguagePackageLoadResult。 | schema/version/culture/id/checksum/path/resource 格式错误、重复根属性和超限分别返回稳定 error kind。Provider 必须拒绝与自身 `Kind` 不匹配的 descriptor。 | 取消后不得缓存 partial package。 | 同一 culture/package 并发 load 合并；共享 load 只受 service lifetime 控制，每个 waiter 独立观察自己的 token。 |
| ILocalizationService.ActivateScope | 激活一个或多个模块、插件、路由或窗口资源范围。 | `LocalizationLookupContext` 至少包含一个 scope id。 | 可释放的 `ILocalizationScopeLease`。 | 空上下文抛 `ArgumentException`；重复激活采用引用计数。 | 同步执行。 | lease Dispose 幂等；最后一个 lease 释放后该 scope 不再参与后续查找或语言切换预加载。 |
| ILocalizationService.GetStringAsync | 查找字符串。 | key、可选 `LocalizationLookupContext`、cancellationToken；culture 来自当前 state，fallback chain 按本次 lookup 可见 descriptor 动态计算。 | LocalizedString。 | package load 失败继续 fallback；缺失 key 返回 missing marker 并诊断；运行时新增 descriptor 形成 fallback cycle 时诊断并停止继续遍历该环。 | 必须观察 token。 | 无 context 重载只查 Host/Presentation；带 context 重载只允许全局 scope 和已激活且 id 匹配的 scope；同一 scope 内保持注册顺序；不允许其他活动 scope 的 fallback 污染本次 lookup。 |
| ILocalizationService.GetMessageAsync | 查找并格式化字符串。 | key、arguments、cancellationToken。 | LocalizedMessage。 | 缺失 key 返回 missing marker；格式化失败返回 raw template 并诊断。 | 必须观察 token。 | 格式化使用命中资源的 culture。 |
| ILocalizedText.Changed / RefreshAsync / Dispose | 订阅或主动刷新动态文本。 | event handler；RefreshAsync token。 | event/ValueTask/void。 | handler 失败被隔离并诊断；handler 内重入 Refresh 为幂等 no-op。 | Refresh 观察 token；Dispose 后 Refresh 为 no-op。 | 每个 text 的 refresh 与通知 FIFO；Dispose 从外部调用时等待在途通知，返回后不得再开始 callback；handler 可自释放而不死锁。 |
| AssemblyLanguagePackageProvider.Discover | 发现 assembly 内语言包。 | assembly 不得为 null；读取 `LanguagePackageAttribute`。 | descriptor 列表，包含 assembly location、resource base name、fallback culture、version、checksum 和 contribution id。 | invalid culture 由 `CultureInfo` 拒绝；缺失资源在 `LoadAsync` 阶段返回失败 result。 | 发现同步执行，加载异步。 | descriptor 发布后不可变；owner revoke 由 registry 承接。 |
| LanguagePackageRegistry.Register / RegisterRange / RevokeOwner | 注册、原子批量注册或撤销 owner 的语言包 descriptor。 | descriptor 集合和 owner id 不得为空；集合不得含 null；Module/Plugin/Route/Window descriptor 必须提供 `ScopeId`；`LocalizationOptions.LanguagePackages` 作为 `host` owner 的初始注册。 | 注册返回 LocalizationResult；撤销返回 descriptor 数量。 | 缺少 scope id 返回 `InvalidDescriptor`；相同 culture/package id 在 Registry 或同一批次内重复时返回 `PackageAlreadyRegistered` 且整批不发布；已撤销 owner 后续注册返回 `OwnerRevoked`。 | 同步执行。 | Registry 变更立即影响后续 lookup；`RegisterRange` 在单一锁事务中预检并发布；owner 撤销同步清理对应 runtime package cache。 |
| ILocalizationService.RevokePackagesByContributionIdAsync | 撤销插件或模块 contribution 的语言包。 | contributionId 不得为空；cancellationToken。 | revoked package count。 | 找不到 contribution 返回 0；已撤销 package 从后续 lookup、loaded package cache 和 current state 中移除；mutation callback 重入抛 `InvalidOperationException`。 | 提交前观察调用方 token；descriptor/cache/state 撤销提交后，bridge 与文本刷新由 service lifetime token 驱动完成。 | 与 culture switch 共用 FIFO mutation queue；重复 revoke 幂等；并发 load 在 commit 时重验 descriptor，lookup 使用开始时 descriptor snapshot。 |
| ILocalizationService.Dispose / DisposeAsync | 结束 Localization runtime。 | 无；不得从当前 Localization mutation callback 内重入释放。 | void / ValueTask。 | Dispose 后新 mutation/lookup 抛 ObjectDisposedException；已排队 mutation 返回 ServiceDisposed 或取消；callback 内重入释放抛 InvalidOperationException，避免等待自身。 | 取消 service-owned package load。 | 并发调用共享同一释放事务；取消并等待异步 load/mutation，释放 tracked text、Culture State、cache package 并解除 Registry 事件。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AssemblyLanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CultureState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `FileLanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizationDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizationService` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizationScopeLease` | 关键 contract | Dispose 和引用计数语义变化必须更新本文档和 compatibility。 |
| `LocalizationLookupContext` | 关键 contract | scope id 匹配和默认全局语义变化必须更新本文档和 compatibility。 |
| `ILocalizedText` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationLocalizationBridge` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryLocalizationDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryLanguagePackageProvider` | 支持类型 | 默认 provider、资源复制和取消语义变化必须更新本文档和 compatibility。 |
| `LanguagePackage` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageLoadResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageProviderKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageRegistration` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageRegistry` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticIds` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticRecord` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationErrorKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationService` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedMessage` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedString` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedTextChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ResourceScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。
- `Pluralization`、`ResourceObject`、`FlowDirection`、`CultureMetadata` 在 1.0 没有运行时执行合同，Generator 必须以 build error 拒绝；其余 resource kind 统一使用字符串/格式化字符串运行时。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在可取消阶段的 IO、dispatcher work、插件代码和 handler 调用前后观察取消。
- 调用方取消发生在 mutation 提交前时，不得提交状态、缓存、事件或 UI；提交后已无法回滚，剩余一致性工作改由 owner lifetime token 完成。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
