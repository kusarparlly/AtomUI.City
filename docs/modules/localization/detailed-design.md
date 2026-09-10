# AtomUI.City.Localization Detailed Design 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Detailed Design` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Detailed Design

适用范围：多语言资源、文化切换、语言包懒加载、独立语言包 assembly、AtomUI/Avalonia 集成、UI 热刷新、插件资源、AOT/source generator 和测试策略。

### 1. 定位

`AtomUI.City.Localization` 是应用级多语言资源运行时。

Localization 负责文化状态、资源包管理、按当前语言懒加载语言包、资源查找、UI 热刷新、插件资源撤销、缺失诊断和 source generator 资源索引。

Localization 不决定业务文案，不直接渲染 UI，也不替代应用的 Avalonia binding。它必须让 View、ViewModel、Route、Command、Validation、Data/Security 错误都能用统一方式表达本地化文本。

核心链路：

```text
Localization manifest
-> selected culture
-> first lookup or culture switch lazily loads visible language packages
-> immutable LanguagePackage cache
-> optional application-owned refresh bridge
-> AtomUI/Avalonia resources and bindings
```

### 2. 设计原则

- Culture-first lazy loading：懒加载以当前 culture 的语言包为单位，不按单个 key 零散加载。
- Manifest-only startup：启动只加载 manifest，不加载所有语言包。
- Assembly package capable：普通 .NET 运行时支持语言包独立 assembly 动态加载。
- AOT compatible：Native AOT 模式使用 file-based locpack provider，不依赖动态 assembly loading。
- UI integration：文化变化可通过应用或独立 UI 适配包实现的可选 bridge 同步到 AtomUI/Avalonia。
- Transactional culture switch：文化切换必须先准备并校验 package，再提交状态；提交前失败或调用方取消保留旧状态。可选 application-owned bridge 位于提交后，改由 service lifetime token 完成本次刷新；失败返回 Result、记录诊断并继续本地文本刷新，不回滚已发布 CultureState。
- Plugin-aware：插件语言包必须可撤销、可释放、可卸载。
- Strong diagnostics：缺失 key、重复 key、fallback 失败和格式化错误必须可诊断。
- Source-generator-first：资源 manifest、强类型 key 和 descriptor 由 source generator 生成。
- Testable：支持无真实 UI 的文化切换、查找、fallback、插件撤销和 UI refresh 测试。

### 3. 非目标

Localization 不负责：

- 业务翻译内容。
- 在线翻译服务。
- 翻译工作流系统。
- AtomUI/Avalonia 控件实现。
- UI 布局自适应策略。
- 业务错误模型。
- 具体资源编辑器。

### 4. 核心抽象

| 类型 | 职责 |
|---|---|
| `ILocalizationService` | 当前文化、语言包加载、文化切换和资源查找入口。 |
| `ILocalizationService.CultureState` | 通过 City.State `IReadOnlyState<CultureState>` 提供当前文化状态和 revision。 |
| `ILanguagePackageProvider` | 加载指定 culture 的语言包。 |
| `LanguagePackage` | 已加载、只读且可释放的语言包。 |
| `ILocalizedText` | 可随文化变化刷新显示值的本地化文本句柄。 |
| `LanguagePackageRegistry` | 按 owner 管理 Host、Module、Plugin 的 descriptor 注册与撤销。 |
| `LocalizationLookupContext` / `ILocalizationScopeLease` | 约束 Module、Plugin、Route、Window 资源可见性。 |
| `IPresentationLocalizationBridge` | 应用或独立 UI 适配包可选实现的 culture commit 回调；默认 no-op。 |
| `ILocalizationDiagnostics` | 缺失资源、加载失败、fallback 和刷新诊断。 |

命名不加 `City` 前缀。

### 5. 资源分层

资源来源：

```text
Host resources
Module resources
Plugin resources
Presentation resources
Route / Window resources
```

查找优先级：

```text
Current feature / plugin
-> owning module
-> application host
-> shared framework
-> fallback culture
-> invariant fallback
-> missing resource marker
```

详细规则见：[resource-model.md](resource-model.md) 和 [lookup-and-fallback.md](lookup-and-fallback.md)。

### 6. 语言包懒加载

Localization 懒加载以 language package 为单位。

```text
Startup
-> load localization manifests only
-> know available cultures and package descriptors
-> do not load language packages

Current culture = zh-CN
-> first lookup loads packages visible to its context
-> culture switch loads global and active-lease zh-CN packages
-> load fallback packages only when needed
-> commit culture
-> refresh UI
```

关键约束：

- 每个语言包只服务一个 culture。
- 当前选择语言决定实际加载哪些 language package。
- 插件启用不等于加载所有语言资源。
- 模块注册不等于加载所有语言资源。
- Fallback culture 按需加载，不能一次性加载全部 fallback。

详细规则见：[lazy-loading.md](lazy-loading.md)。

### 7. 语言包 Assembly

普通 .NET 运行时支持语言包放在独立 assembly 中运行时动态加载。

推荐模式：

```text
AssemblyLanguagePackageProvider
FileLanguagePackageProvider
```

- `AssemblyLanguagePackageProvider`：普通 .NET / CoreCLR / 插件动态加载场景。
- `FileLanguagePackageProvider`：Native AOT 或严格 AOT 模式，使用 `.locpack`、json 或 binary resource pack。

语言包 assembly 应尽量是 resource-only，不放可执行代码。生成的 key constants 和 registrar 位于模块或插件主 assembly，语言包 assembly 只提供资源数据。

详细规则见：[language-package-assemblies.md](language-package-assemblies.md)。

### 8. 文化切换

文化切换必须事务式。

```text
SetCultureAsync("ja-JP")
-> calculate active package set
-> load ja-JP packages
-> validate critical resources
-> prepare AtomUI resource dictionaries
-> commit CurrentCultureState
-> swap resource dictionaries on UI Thread
-> notify localized bindings
```

加载失败时：

```text
Load failed
-> keep old culture
-> release partially loaded packages
-> emit diagnostics
```

详细规则见：[culture-management.md](culture-management.md)。

### 9. AtomUI/Avalonia 集成

Localization 不直接操作控件。文化变化通过应用或独立可选 UI adapter 接入 AtomUI/Avalonia；Presentation 主模块不实现该适配。

```text
LocalizationService.SetCultureAsync
-> load selected language packages
-> commit culture state
-> optional application-owned IPresentationLocalizationBridge.ApplyCultureAsync
-> refresh live ILocalizedText handles
-> application ViewModel/Avalonia Binding updates UI
```

AtomUI/Avalonia 资源更新必须在 UI Thread。

该 UI 线程保证由应用或独立 UI adapter 提供；Localization Core 的普通 `ILocalizedText` handler 不保证执行线程。

详细规则见：

- [atomui-integration.md](atomui-integration.md)
- [ui-refresh.md](ui-refresh.md)

### 10. 开发者体验

字符串 key 模式：

```csharp
public sealed partial class SettingsViewModel
{
    private readonly ILocalizationService _localization;

    public ValueTask<LocalizedString> GetTitleAsync(CancellationToken cancellationToken) =>
        _localization.GetStringAsync("Settings.Title", cancellationToken);
}
```

生成 key 常量模式：

```csharp
var title = await localization.GetStringAsync(GeneratedLocalizationManifest.Keys.Settings_Title);
```

声明式 metadata：

```csharp
[Route("settings", TitleKey = "Settings.Title")]
public sealed partial class SettingsViewModel
{
}
```

1.0 提供字符串 key、生成 key 常量、`ILocalizedText` 和可选应用刷新 bridge。Presentation 路由/窗口 setter binding 已在 1.0 冻结前移除；XAML markup extension 和生成的强类型方法 accessor 尚无 Feature ID，不属于当前合同。

详细规则见：[mvvm-integration.md](mvvm-integration.md)。

### 11. 插件资源

插件本地化资源必须绑定 Contribution。

```text
Plugin enable
-> register localization manifest
-> do not load all language packs
-> active plugin route requests selected culture package
-> load package
-> plugin stopping
-> block new lookup
-> detach plugin UI
-> revoke resource descriptors
-> clear resource cache
-> unload package
```

插件卸载后，Host 不能持有插件 ResourceManager、assembly、localizer delegate、generated accessor 实例或 ResourceDictionary。

详细规则见：[plugin-integration.md](plugin-integration.md)。

### 12. AOT 和 Source Generator

Localization generator 负责：

- 生成 resource manifest。
- 生成 language package descriptor。
- 生成 key constants。
- 生成 module/plugin resource descriptor。
- 诊断 fallback 不完整。
- 诊断重复 key。
- 诊断空 attribute 参数、无效 culture、未知 enum、scope、package identity 和无运行时合同的 resource kind。
- 生成 registrar 通过单次 `RegisterRange` 原子发布 descriptor。

运行时默认不扫描程序集找资源。

详细规则见：[source-generation.md](source-generation.md)。

### 13. 错误策略

| 场景 | 默认处理 |
|---|---|
| 当前文化缺 key | 查找 fallback culture。 |
| fallback 也缺 | 查找 invariant。 |
| invariant 也缺 | missing marker + diagnostics。 |
| 格式执行异常 | fallback raw template + diagnostics。 |
| 语言包加载失败 | rollback 到旧 culture。 |
| 插件资源已撤销 | fallback 或清理对应 UI。 |
| UI resource apply 失败 | 保留已提交 CultureState，返回失败 Result、记录错误并继续 LocalizedText 刷新；应用或可选适配包负责其局部资源一致性。 |

### 14. 测试策略

当前单元测试工程提供私有 test double，未向 `AtomUI.City.Testing` 增加 Localization 专用 public API：

- recording/blocking language package provider。
- recording/throwing optional application localization bridge。
- in-memory diagnostics。
- deterministic culture switch、并发 load、撤销和 dispose 驱动。

必须覆盖：

- manifest-only startup。
- selected culture language package lazy load。
- fallback package lazy load。
- culture switch 提交前 package load/critical validation 失败保持旧状态；bridge 提交后失败不回滚状态。
- AtomUI bridge apply。
- binding refresh。
- plugin package revoke。
- file locpack provider。
- locpack 16 MiB 上限和重复根属性拒绝。
- missing key diagnostics。

详细规则见：[diagnostics-and-testing.md](diagnostics-and-testing.md)。
