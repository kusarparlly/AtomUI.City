# AtomUI.City.Localization Lookup And Fallback 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Lookup And Fallback` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Lookup and Fallback 设计

适用范围：资源查找、资源优先级、fallback culture、missing marker、格式化错误和撤销后 fallback。

### 1. 定位

Lookup and fallback 负责把 resource key 解析为当前 culture 下的显示资源。

查找必须可诊断、可预测、可测试。

### 2. 查找输入

查找输入包含：

- Resource key。
- 当前 `CultureState` snapshot。
- 可选 `LocalizationLookupContext`（ModuleId、PluginId、RouteId、WindowId）。
- message format args。

### 3. 查找顺序

```text
route resources
-> window resources
-> plugin resources
-> module resources
-> host resources
-> presentation framework resources
-> fallback culture
-> invariant fallback
-> missing marker
```

同一 scope 内保持 Host 注册顺序，作为 contribution order 和 Host policy 的稳定表达。

### 4. Fallback Culture

Fallback chain 示例：

```text
zh-CN -> zh-Hans -> zh -> invariant
```

规则：

- 每次 lookup 只使用全局 descriptor 与本次 `LocalizationLookupContext` 可见、且 lease 已激活的 scoped descriptor 计算 fallback chain；其他活动 scope 不得污染本次查找。
- fallback package 按需加载。
- fallback 全部未命中时记录 `AUCLOC002`；成功命中由 `LocalizedString.IsFallback/Culture` 表达，不额外产生 warning。
- target package 的 critical resource 在 commit 前校验；缺失时保持旧 culture state 并释放本次未缓存 package。
- 非 critical resource fallback 失败返回 missing marker。
- 当前 culture 的所有 scope 均未命中后，才进入 fallback culture chain。

### 5. Missing Marker

当前 1.0 固定 missing marker：

```text
!Settings.Title!
```

在返回 marker 前仍会尝试 invariant fallback，并写入 diagnostics。开发/发布模式可配置 marker 尚无 Feature ID。

### 6. 格式化

格式化资源必须使用实际命中资源的 culture。

规则：

- 参数数量不匹配或任意 `IFormattable`/格式提供器执行异常时返回 raw template。
- 所有格式化执行异常记录 diagnostics，不得让用户格式化代码逃逸为 lookup 异常。
- `GetMessageAsync` 使用命中资源的 culture 进行格式化。

### 7. 资源撤销

插件资源撤销后：

```text
Revoke contribution
-> remove package store
-> invalidate lookup cache
-> fallback to owning module / host
-> notify optional application refresh hook or clear application UI
```

撤销后的资源不能继续被 Host cache 命中。

### 8. 测试策略

测试必须覆盖：

- 当前 scope 命中。
- module fallback。
- host fallback。
- culture fallback。
- 动态 scoped descriptor 声明的 fallback 以及跨 scope fallback 隔离。
- invariant fallback。
- missing marker。
- 格式参数和自定义 formatter 异常。
- 插件撤销后 fallback。
