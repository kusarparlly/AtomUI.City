# AtomUI.City.Localization Source Generation 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Source Generation` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。
- 优先 source generator，避免运行时反射扫描。
- generated output 必须稳定排序。
- diagnostics 必须可由 generator tests 断言。

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

## AtomUI.City.Localization Source Generation 设计

适用范围：Resource manifest、language package descriptor、强类型 key 常量、AOT 边界和构建期诊断。

### 1. 定位

Localization 是 source-generator-first 模块。

运行时默认不扫描程序集找资源 key。Source Generator 生成 manifest、descriptor 注册入口和强类型 key 常量；`AssemblyLanguagePackageProvider.Discover` 仅作为显式调用的反射式兼容入口。

### 2. 生成内容

Generator 负责：

- Resource manifest。
- Language package descriptor。
- Supported culture manifest。
- Fallback culture manifest。
- Key constants。
- Module resource descriptor。
- Plugin resource descriptor。

当前生成入口为 `AtomUI.City.Generated.GeneratedLocalizationManifest`：`RegisterPackages` 通过一次 `LanguagePackageRegistry.RegisterRange` 原子注册全部 descriptor，任一冲突都不得留下部分注册；`SupportedCultures` 和 `ResourceKeys` 提供稳定 manifest，`Keys` 提供强类型常量。生成 descriptor 保存声明 assembly 的 `AssemblyLoadContext`。生成链由 `AtomUICityIncrementalGenerator.Initialize` 直接初始化，不允许仅存在独立 metadata/builder 而未接入 Roslyn pipeline。

`LocalizationMetadataReader` 将 attribute 读取阶段错误写入 `LocalizationMetadata.Diagnostics`；主 pipeline 必须先报告这些错误并停止生成，不能把无效声明过滤后继续产生部分 manifest。

1.0 package identity 固定为 `(culture, packageId)`；当相同 package id 存在多个 culture 时，`LocalizedResourceAttribute.Culture` 必填以消除歧义。resource scope/scope id 必须与目标 package 一致。

`String`、`FormattedString`、`ValidationMessage`、`ErrorMessage`、`CommandText`、`RouteTitle` 生成字符串 key；`Pluralization`、`ResourceObject`、`FlowDirection`、`CultureMetadata` 尚无 1.0 runtime contract，Generator 必须报错而不是生成无效元数据。`Critical=true` 会写入 descriptor 的 `CriticalResourceKeys`，culture commit 前必须验证。

### 3. 强类型 Key

`Keys` 常量生成在声明 attribute 的模块或插件主 assembly 中，不生成在独立语言包 assembly 中。

原因：

- 语言包 assembly 应保持 resource-only。
- 插件语言包卸载不能影响主插件的 key contract。
- Host 不应持有语言包 assembly 类型。

### 4. Analyzer 诊断

必须诊断：

- 重复 package identity 和 key。
- 缺失或歧义 package 引用。
- fallback 不完整。
- fallback cycle。
- attribute 必填字符串为空。
- invalid culture、未知 enum、scope、scope id 和 resource base name。
- resource/package scope 不匹配。
- 尚无运行时合同的 resource kind。

源码中 key 引用存在性、格式参数数量、插件覆盖策略和资源类型泄漏 Analyzer 尚无 Feature ID，不属于 1.0 已交付范围。

### 5. AOT

Native AOT 模式：

- 禁止依赖动态 assembly loading。
- 使用 file-based locpack。
- manifest 和 key constants 仍由 generator 生成。
- file locpack descriptor 由应用显式注册；当前 attribute registrar 生成的是 assembly provider descriptor。

### 6. Build 集成

Build 模块后续负责：

- 生成 language package assembly。
- 生成 locpack。
- 输出 manifest。
- 校验资源完整性。
- 复制 culture package 到 output。

Localization 文档只定义 contract。

### 7. 测试策略

测试必须覆盖：

- manifest 生成。
- key constants 和 registrar 生成。
- registrar 原子批量注册。
- duplicate key 诊断。
- missing/ambiguous package 诊断。
- fallback incomplete 诊断。
- fallback cycle 诊断。
- culture identity 规范化和碰撞诊断。
- 空 attribute 参数、未知 enum、scope/resource kind/resource base name 诊断。
