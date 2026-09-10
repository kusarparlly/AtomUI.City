# AtomUI.City.Localization Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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
| AUC-LOCALIZATION-008 | Generated Localization Manifest | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationMetadataReaderTests; LocalizationManifestBuilderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation concrete UI types` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Localization Diagnostics and Testing 设计

适用范围：缺失资源诊断、加载失败、fallback、culture switch、AtomUI bridge、插件撤销和测试工具。

### 1. 定位

Localization 必须可诊断、可测试。

缺失文本不能只表现为 UI 空白。必须能说明缺的是哪个 key、哪个 culture、哪个 package、哪个 contribution 和哪个 fallback 阶段。

### 2. 诊断字段

诊断记录必须填写与当前事件适用的字段；不存在的上下文保持空值，不能伪造。可用字段包括：

- operation id。
- culture / fallback culture。
- resource key。
- package id。
- scope / scope id。
- provider kind / location。
- contribution id / revoked package count。
- error kind。
- load attempt / elapsed milliseconds。
- culture revision。

敏感信息通常不应放入本地化资源 key。错误参数写入诊断时需要脱敏。

### 3. 诊断分类

| 分类 | 说明 |
|---|---|
| ResourceMissing | 当前 culture 缺 key。 |
| FallbackMissing | fallback 也缺 key。 |
| PackageLoadFailed | 语言包加载、完整性校验、超出大小上限或 critical key 校验失败。 |
| FormatFailed | 格式化失败。 |
| ResourceRevoked | 资源 contribution 已撤销。 |
| AtomUiApplyFailed | AtomUI/Avalonia 资源应用失败。 |
| CultureSwitchRejected | package load、critical validation、非法 culture 或 fallback cycle 导致提交前拒绝。 |

`PackageVersionMismatch`、`FormatFailed`、`ResourceRevoked` 是 `LocalizationErrorKind`；稳定诊断 code 以 [diagnostics.md](diagnostics.md) 的 AUCLOC001-010 表为准。插件资源泄漏由 PluginSystem 诊断，不由 Localization 伪造独立 code。

### 4. Testing 包

当前 `tests/AtomUI.City.Localization.Tests` 使用模块私有 test double：

- recording/blocking/throwing language package provider。
- recording/throwing presentation bridge。
- `InMemoryLocalizationDiagnostics`。
- deterministic culture switch、scope、撤销、并发 load 和 dispose 驱动。

Localization 专用 `AtomUI.City.Testing` public helper 尚无 Feature ID，不属于当前合同。

### 5. 测试场景

必须覆盖：

- manifest-only startup。
- selected culture package lazy load。
- fallback package lazy load。
- culture switch success。
- culture switch 提交前拒绝，以及 bridge 提交后失败不回滚。
- missing marker。
- format error。
- AtomUI bridge apply。
- UI binding refresh。
- plugin package revoke。
- plugin assembly unload。
- file locpack provider；Native AOT publish smoke 由 Build/Release Gate 后续立项。
- locpack 16 MiB 上限、重复根属性拒绝和 provider 取消。
- culture/revoke 提交后调用方取消仍完成 bridge 与文本刷新。
- generated registrar 原子注册，空 attribute 参数与未知 enum 阻断生成。

### 6. 无 UI 测试

Localization Core 测试不依赖真实 AtomUI/Avalonia。

Localization 使用 fake application bridge 验证通知合同。真实 UI resource dictionary 刷新由应用或独立可选适配包测试，不属于 Presentation 主模块测试责任。
