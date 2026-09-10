# AtomUI.City.Localization Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 语言包按当前 culture 懒加载。
- provider 必须可撤销。
- 缺失 key 必须诊断并走 fallback。
- 插件语言包卸载后不得出现在 lookup。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `LocalizationOptions.DefaultCulture`、`DefaultUICulture` 和 `FallbackCultures` 的默认值与校验行为进入 1.0 兼容承诺。
- `LocalizationService.SetCultureAsync` 的 context-specific fallback chain 顺序、非法 culture 失败 result、任意长度 fallback cycle 拒绝、重复 culture 幂等，以及 state 提交后的 service-owned 完成语义进入 1.0 兼容承诺。
- `LanguagePackageRegistry` 作为 runtime descriptor 唯一来源、options descriptor 的 Host owner 归属、`culture + package id` 身份、重复 package 拒绝、`RegisterRange` 全有或全无、动态注册可见性和 owner revoke 后拒绝新注册/清理 lookup cache 行为进入 1.0 兼容承诺。
- `FileLanguagePackageProvider` 与 `AssemblyLanguagePackageProvider` 的取消结果、provider kind mismatch、格式错误 result、culture mismatch result、16 MiB locpack 上限和重复根属性拒绝行为进入 1.0 兼容承诺。
- `AssemblyLanguagePackageProvider.Discover(Assembly)` 的 attribute-to-descriptor 映射、assembly location、resource base name、fallback culture、version、checksum 和 contribution id 行为进入 1.0 兼容承诺。
- `LocalizationService` 的 culture/package cache key、同一 culture/package in-flight load 合并和 lookup load failure fallback 行为进入 1.0 兼容承诺。
- `LocalizationService` 的 descriptor scope lookup priority、Host/Presentation 全局可见性、scoped descriptor 的 `ScopeId + LocalizationLookupContext + active lease` 可见性、culture switch 活动集合预加载、missing marker、message format failure raw-template fallback 和 `LocalizedText` revision refresh 行为进入 1.0 兼容承诺。
- `LocalizationService.SetCultureAsync` 在可选 application-owned bridge 失败时不回滚已提交 culture state、继续刷新本地 `ILocalizedText` 并返回失败 result 的行为进入 1.0 兼容承诺。
- `ILocalizationService.RevokePackagesByContributionIdAsync` 的 descriptor 撤销、cache 清理、state revision、重复 revoke 幂等、lookup snapshot 隔离和提交后 service-owned 完成行为进入 1.0 兼容承诺。
- Localization mutation 的 FIFO 顺序、锁外 callback、`ReentrantOperation` 快速失败以及 revoke/load commit 重验行为进入 1.0 兼容承诺。
- generated Localization hint folder、`GeneratedLocalizationManifest` 类型名、`RegisterPackages`、`SupportedCultures`、`ResourceKeys`、`Keys` 成员以及生成 descriptor 保留声明 assembly `AssemblyLoadContext` 的行为进入 1.0 兼容承诺。
- locpack 必填 schema version `1`、`sha256:<hex>` checksum、16 MiB 上限、重复 JSON 根属性拒绝、File `AllowedRootPath` 边界以及 id/culture/version/resources 校验错误分类进入 1.0 兼容承诺。
- `ILocalizationService.CultureState` 的 City.State 只读发布、`CultureInfo`/集合深只读快照、fallback lazy load/缓存复用/撤销同步更新 LoadedPackageIds，以及 service Dispose/DisposeAsync 共享完成事务的取消与释放语义进入 1.0 兼容承诺。
- `LocalizationErrorKind.PackageTooLarge` 和 `InvalidDescriptor` 的上述 provider 语义进入 1.0 兼容承诺。
- `LocalizedResourceKind` 中六类 string-like kind 可生成；`Pluralization`、`ResourceObject`、`FlowDirection`、`CultureMetadata` 在获得独立 Feature/runtime contract 前由 Generator 拒绝。
- `LocalizationDiagnosticRecord` 的 operation/culture/resource/package/scope/provider/location/attempt/elapsed/revision/error/contribution 上下文字段和 AUCLOC001-010 code 语义进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
