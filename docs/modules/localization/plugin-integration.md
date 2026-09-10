# AtomUI.City.Localization Plugin Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
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

## AtomUI.City.Localization Plugin Integration 设计

适用范围：插件本地化贡献、语言包懒加载、assembly package、资源撤销、卸载和跨插件 contract。

### 1. 定位

插件可以贡献本地化资源，但必须受 Host 生命周期、Registry owner/contribution id、文化切换和卸载顺序约束管理。

插件启用不等于加载所有语言包。插件只注册 localization manifest，当前 culture 和活动 UI 决定实际加载哪个语言包。

### 2. 插件贡献

插件可以贡献：

- Localization manifest。
- Language package descriptor。
- Assembly language package。
- File locpack。
- Route title key。
- Command text key。
- Validation / error message key。

插件集成层调用生成的 `RegisterPackages(registry, ownerId)` 或 `LanguagePackageRegistry.Register` 进入 Registry。Localization 不声明 PluginSystem 的 Contribution Request 类型。

### 3. 插件加载流程

```text
Plugin enable
-> register localization manifest
-> register package descriptors
-> do not load all language packages

Plugin route opened under zh-CN
-> load plugin zh-CN package
-> resolve scoped text through active LocalizationScopeLease
-> application/optional UI adapter refresh plugin UI
```

### 4. 插件停用流程

```text
Plugin stopping
-> block new localization lookup
-> detach plugin UI / route
-> remove AtomUI/Avalonia resource dictionaries
-> revoke package descriptors
-> clear plugin resource cache
-> dispose language packages
-> PluginSystem release its contribution/view leases
```

### 5. Assembly 卸载

插件语言包 assembly 必须随插件卸载。

禁止：

- Host 静态缓存插件 ResourceManager。
- Host 静态缓存插件 language assembly。
- Host 长期持有插件 localizer delegate。
- AtomUI/Avalonia 仍持有插件 ResourceDictionary。
- 插件 generated accessor 泄漏到 Host 静态缓存。

### 6. Contract 边界

跨插件边界使用的 MessageKey、ResourceKey、ErrorCode 可以是字符串或 Host 共享 contract 中的强类型 key。

插件私有资源类型不能被 Host 长期持有。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| 插件 package 缺失 | fallback 或 missing marker。 |
| 插件 package 加载失败 | 记录诊断，不影响 Host。 |
| 插件资源撤销失败 | 进入插件卸载错误聚合。 |
| 卸载后仍有引用 | 由 PluginSystem 的 unload/lease 诊断承接；Localization 不声明 `UnloadPending` 状态。 |

### 8. 测试策略

测试必须覆盖：

- 插件 manifest 注册。
- 插件当前 culture package 懒加载。
- 插件未选 culture package 不加载。
- 插件 UI binding 先释放、资源 descriptor 后撤销。
- 插件 package assembly 卸载。
- Host 不持有插件私有引用。
