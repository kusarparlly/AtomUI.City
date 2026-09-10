# AtomUI.City.Localization Validation And Errors 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Validation And Errors` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Validation and Errors 设计

适用范围：Validation message、Data/Security error、MessageKey、MessageArgs、错误文本刷新和诊断。

### 1. 定位

Validation 和错误模块不应该返回固定显示文本。

它们应返回稳定 code、message key 和参数，由 Localization 在当前 culture 下渲染。

### 2. Message 模型

建议消息结构：

```text
ErrorCode
MessageKey
MessageArgs
Severity
Diagnostics
```

`MessageKey` 用于用户可见文本。`Diagnostics` 用于开发和日志，不直接展示给用户。

### 3. Validation

Localization 提供 `GetMessageAsync` 与 `CreateMessageTextAsync` 作为 Validation message 的查找/刷新原语；Validator 的 `MessageKey/MessageArgs` 结构由 MVVM owning module 定义。

规则：

- Validator 返回 MessageKey 和 MessageArgs。
- 应用 UI 或独立可选适配包负责展示本地化结果。
- Culture 切换后可见 validation message 刷新。
- 缺失 validation key 使用 missing marker。

### 4. Data / Security Error

建议 DataError 和 Security authorization result 不直接包含最终显示文本。Localization 不定义这两个模块的错误结果类型。

示例：

```text
DataError.AuthorizationForbidden
MessageKey = "Errors.AuthorizationForbidden"
MessageArgs = [...]
```

文化切换后，错误提示可以重新渲染。

### 5. Interaction 错误

Dialog、Toast、Notification 应传 MessageKey。

已经显示的临时 UI 是否刷新，由应用 UI 策略决定：

- 长时间存在的 Dialog 应刷新。
- 短生命周期 Toast 可以不刷新，但后续新 Toast 使用新 culture。

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| MessageKey 缺失 | missing marker + diagnostics。 |
| MessageArgs 或自定义 formatter 执行异常 | raw template + diagnostics。 |
| culture switch 时错误 UI 已关闭 | 忽略刷新。 |
| plugin error key revoked | fallback 或 clear UI。 |

### 7. 测试策略

Localization 模块测试覆盖：

- message key + arguments 格式化以及任意 formatter 异常隔离。
- message text culture switch refresh。
- missing key marker。
- plugin key revoked。

Data/Security/Validation 专用结果到 MessageKey 的集成测试由相应 owning module 负责，不能由本文冒充已实现合同。
