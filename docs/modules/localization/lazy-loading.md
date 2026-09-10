# AtomUI.City.Localization Lazy Loading 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Lazy Loading` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。

## Public Contract

- 只允许通过 `AtomUI.City.Localization` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-LOCALIZATION-001 | Culture State | CultureStateTests |
| AUC-LOCALIZATION-002 | Language Package Providers | LanguagePackageProviderTests |
| AUC-LOCALIZATION-003 | Lazy Loading | LocalizationServiceTests |
| AUC-LOCALIZATION-004 | Lookup and Fallback | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Assembly Language Packages | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-006 | Optional Application Refresh Hook | LocalizationServiceTests |
| AUC-LOCALIZATION-007 | Plugin Package Revocation | LocalizationServiceTests |
| AUC-LOCALIZATION-008 | Generated Localization Manifest | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation concrete UI types` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Localization Lazy Loading 设计

适用范围：manifest-only startup、按当前语言包懒加载、active scope 资源加载、fallback 按需加载和缓存。

### 1. 定位

Localization 懒加载以语言包为单位。

它不是按单个 key 零散加载，也不是启动时加载所有语言包。启动阶段只加载 manifest，当前选择语言决定实际加载哪些 language package。

### 2. 启动流程

```text
Application startup
-> load localization manifests only
-> register package descriptors
-> select initial culture
-> publish initial CultureState with an empty loaded-package set
```

启动不加载：

- 未选择 culture 的 package。
- 未激活 Route 的 package。
- 未使用插件的 package。
- 所有 fallback package。

构造服务和注册 manifest 均不读取语言包正文。第一次 lookup 按其 context 加载可见 descriptor；切换到不同 culture 时，`SetCultureAsync` 加载 Host/Presentation 与当前存在活动 lease 的 scoped descriptor。激活 scope 后首次带对应 context 的 lookup 按需加载；最后一个 lease 释放后，后续 lookup 立即退回全局资源。

### 3. 加载策略

1.0 只有两种实际触发路径：

| 触发 | 说明 |
|---|---|
| Lookup | 首次查找时按 `LocalizationLookupContext` 加载当前 culture 的可见 package；缺失后再按需加载 fallback。 |
| CultureSwitch | 切换到不同 culture 时加载全局 descriptor 与已有活动 lease 对应的 scoped descriptor。 |

路由、窗口和插件只通过 `ActivateScope` lease 改变可见集合；1.0 没有独立的 `Eager`、route preload、plugin preload 或 `PreloadHint` API。

### 4. 当前文化懒加载

示例：

```text
CurrentCulture = zh-CN
Lookup context = SettingsModule
Active leases = SettingsModule, SalesPlugin

Load:
Host.zh-CN
SettingsModule.zh-CN

Do not load:
Host.en-US
SettingsModule.ja-JP
SalesPlugin.zh-CN
SalesPlugin.en-US
```

### 5. Fallback 按需加载

Fallback chain 示例：

```text
zh-CN -> zh-Hans -> zh -> invariant
```

规则：

- 只有当前层缺失 key 时，才加载下一层 fallback package。
- fallback package 也按 contribution 加载。
- fallback 加载失败必须记录诊断。
- 同一 culture/package 的并发 load 必须合并为一个 in-flight task。
- package cache key 必须同时包含 culture 和 package id，避免同名 package 的不同 culture 互相污染。
- lookup 阶段 package load 失败时必须记录诊断，跳过该 package，并继续 fallback chain。

### 6. Active Package Set

Active package set 由当前运行时状态决定：

- `ResourceScope.Host` 和 `ResourceScope.Presentation` 始终全局可见。
- `ResourceScope.Module`、`Plugin`、`Route`、`Window` 必须同时满足 descriptor `ScopeId`、活动 `ILocalizationScopeLease` 和 lookup context id 匹配。

Route 离开、Window 关闭或 Plugin 停用时释放对应 lease，后续 lookup 立即不再看到该 scope。插件 contribution 撤销会清除对应 cache；单纯 lease 释放不会立即驱逐已加载 package。

### 7. 缓存

package cache 与 in-flight load 的稳定键均为 `(culture, packageId)`。

规则：

- 成功 load 才能写入 cache；失败、取消、已撤销或 service 已 Dispose 的结果不得发布。
- culture switch 可复用相同键的现有 package，并通过 `CultureState.LoadedPackageIds` 发布当前/回退链已加载集合。
- registry owner/contribution revoke 清理对应 package cache。
- service Dispose 取消 in-flight load 并释放全部 cache。
- 1.0 没有弱引用缓存、内存压力驱逐或按 package version/resource revision 分层的策略。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| package not found | fallback 或 missing marker。 |
| package load failed | culture switch 阶段 rollback；普通 lookup 阶段 fallback。 |
| cache entry 已由 revoke 失效 | 丢弃旧条目；后续查找按当前 Registry snapshot 决定是否重新加载。 |
| plugin package revoked | 清理 cache 并 fallback。 |

并发 lookup 对同一 `(culture, packageId)` 共享由 service lifetime token 控制的 load task；调用方 token 仅取消自己的等待，不取消其他 waiter。load 完成时必须重验 descriptor 是否仍注册；已撤销或 service 已 Dispose 的 package 立即释放且不发布。

File provider 在打开文件前规范化 `Location` 与 `AllowedRootPath`，拒绝 root 外路径；locpack reader 以 16 MiB 为硬上限并在复制期间观察取消，校验 schema version、package version、id、culture、SHA-256 checksum、resources object、重复根属性/资源 key 和非字符串值。

### 9. 测试策略

测试必须覆盖：

- startup 只加载 manifest。
- 当前 culture package 加载。
- 未选择 culture package 不加载。
- fallback 按需加载。
- scope lease 激活本身不加载正文，首次带匹配 context 的 lookup 才加载。
- culture switch 只预加载全局和已有活动 lease 对应的 package。
- plugin contribution 撤销清理 cache，且在途 load 不得复活 package。
