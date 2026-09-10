# AtomUI.City.Localization MVVM Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `MVVM Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization MVVM Integration 设计

适用范围：ViewModel 文本查找、生成 key constants、`ILocalizedText` 与 ActivationScope 绑定，以及 Command、Interaction、Validation 的集成边界。

### 1. 定位

MVVM 集成让 ViewModel、Command、Interaction 和 Validation 使用统一本地化能力。

Mvvm 不实现资源查找。Localization 提供文本和 culture notification。应用组合层或独立可选 UI 适配包负责 UI 展示刷新。

### 2. ViewModel Lookup

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

### 3. ActivationScope

Localization subscription 必须绑定 `ActivationScope`。

规则：

- ViewModel 激活时调用 `CreateTextAsync` 创建 `ILocalizedText`，并通过 `ActivationScope.Add` 持有。
- ViewModel 停用时释放订阅。
- ViewModel 构造函数不启动长期订阅。
- 插件 ViewModel 的 localizer 不泄漏到 Host 静态缓存。

### 4. Command

Command 文本可在业务 metadata 中约定下列 key；Localization 1.0 不声明专用 Command metadata 类型：

```text
TextKey
ToolTipKey
DescriptionKey
IconKey
```

业务层可以把 `ILocalizedText.Changed` 连接到 Command 属性通知；专用自动 adapter 需由 MVVM 模块另立 Feature：

```text
CultureChanged
-> command text provider refresh
-> application UI adapter updates menu / toolbar / shortcut UI
```

Command 可执行性不由 Localization 决定。

### 5. Interaction

Interaction request 不应传固定显示文本。

推荐传：

- TitleKey。
- MessageKey。
- ButtonKey。
- MessageArgs。

应用拥有的 Interaction handler 在显示时通过 `ILocalizationService.GetMessageAsync` 查找当前 culture 文本。

### 6. Validation

Validation message 应使用 MessageKey + MessageArgs。

文化切换后，仍显示的 validation message 可由 `CreateMessageTextAsync` 刷新；专用 Validation adapter 不属于 Localization 当前 public API。

### 7. 测试策略

测试必须覆盖：

- ViewModel service lookup。
- generated key constant lookup。
- ActivationScope 停用释放 subscription。
- `ILocalizedText` / message text culture refresh。

Command、Interaction、Validation 的专用跨模块测试随各自 Feature ID 建设，不能在本页宣称已由 Localization 自动完成。
