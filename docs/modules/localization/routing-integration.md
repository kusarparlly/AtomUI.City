# AtomUI.City.Localization Routing Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Routing Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Routing Integration 设计

适用范围：Route title、breadcrumb、错误路由、route scope package 按需加载，以及 Resolver/Guard 文案的集成边界。

### 1. 定位

Routing 集成让路由 metadata 使用本地化 key，而不是固定显示文本。

Routing 不查找资源，不操作 UI。Localization 解析 key，应用 ViewModel/View 展示文本。

### 2. Route Metadata

Route 可以声明：

```text
TitleKey
DescriptionKey
BreadcrumbKey
GroupKey
ErrorTitleKey
```

Source Generator 将这些写入 Route descriptor。

### 3. 页面进入预加载

应用的 Routing-Presentation 编排器消费已匹配 Route 时激活 `LocalizationLookupContext.RouteId` lease；ViewModel 或应用 UI 首次 lookup 时按需加载当前 culture 的 route package。

```text
Route matched
-> application orchestrator activates route localization scope
-> CreateTextAsync loads selected culture package on demand
-> bind localized metadata setters
```

查找失败按普通 Localization fallback/missing marker 处理；Localization 不把资源缺失转换为导航失败。

### 4. Guard / Resolver 文案

Routing 当前 `RouteGuardResult` / `RouteResolveResult` 提供 `Code` 和 `Message`，没有 Localization 专用 `MessageKey/MessageArgs` contract。业务可以约定把 `Code` 映射为 localization key；框架级强类型错误文案集成需由 Routing 另立 Feature ID。

### 5. Culture 切换

文化切换后必须刷新：

- 当前 route title。
- breadcrumb。
- route error view。

Routing 的 NavigationSnapshot 不因 culture change 重新创建。

### 6. 插件路由

插件路由标题和 breadcrumb 使用插件本地化资源。

插件停用时：

- route contribution 撤销。
- language package 撤销。
- navigation UI fallback 或移除。

### 7. 测试策略

测试必须覆盖：

- Route title key。
- breadcrumb refresh。
- route scope activation 和按需 package load。
- Route metadata setter refresh。
- 插件 route resource revoke。
