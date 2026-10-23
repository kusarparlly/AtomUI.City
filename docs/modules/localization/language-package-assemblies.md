# AtomUI.City.Localization Language Package Assemblies 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Language Package Assemblies` 相关实现决策，不重新定义模块边界。

## 设计决策

- 包布局必须可由测试断言。
- 路径必须使用跨平台分隔符处理。
- 安装目录不得允许路径穿越。
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

## AtomUI.City.Localization Language Package Assemblies 设计

适用范围：静态链接或已由 owner 加载的 assembly 内嵌 locpack、file locpack、插件 owner 撤销与 NativeAOT 边界。

### 1. 定位

Localization 支持从编译期已知或已经由 owner 加载的 assembly 中读取 embedded locpack，但它不负责加载该 assembly。

语言包 assembly 推荐是 resource-only assembly，不放可执行代码。它只承载指定 culture 的资源数据，不承载业务逻辑。

可更新、可下载或独立部署的语言资源使用纯数据 `.locpack`。默认 Localization 在 CoreCLR、trimming、single-file 和 NativeAOT 下遵循同一 provider 边界，不以动态 DLL 作为语言包扩展机制。

### 2. Provider 模型

统一抽象：

```text
ILanguagePackageProvider
-> LanguagePackageRegistry
-> AssemblyLanguagePackageProvider
-> FileLanguagePackageProvider
```

| Provider | 场景 |
|---|---|
| `AssemblyLanguagePackageProvider` | 静态链接或已由 owner 加载的 assembly embedded locpack。 |
| `FileLanguagePackageProvider` | 可更新、可下载或独立部署的纯数据 `.locpack`。 |

两者都输出同一套 `LanguagePackage` / `ILanguagePackageProvider` contract。

`LanguagePackageRegistry` 负责把 descriptor 绑定到 owner，拒绝重复 `(culture, packageId)`，并在 owner revoke 后移除该 owner 的 descriptors 且拒绝同 owner 后续注册。`RegisterRange` 必须全有或全无，生成 registrar 使用该入口避免部分 manifest 发布。Provider 必须拒绝与自身 `Kind` 不匹配的 descriptor；load 取消必须返回 `LanguagePackageLoadResult.Failed(Cancelled)`，不能抛出未声明异常。

### 3. Assembly 语言包布局

静态引用的资源程序集或模块主程序集可以嵌入 locpack；descriptor 必须直接保存 `SourceAssembly`。示意布局：

```text
locales/
  zh-CN/
    AtomUI.City.App.resources.dll
    SettingsModule.resources.dll
    SalesPlugin.resources.dll
  en-US/
    AtomUI.City.App.resources.dll
    SettingsModule.resources.dll
    SalesPlugin.resources.dll
```

也允许自定义命名：

```text
SalesModule.Localization.zh-CN.dll
SalesModule.Localization.en-US.dll
```

当前 provider 不做 satellite probing，也不按路径加载 DLL。静态引用或已经由 owner 加载的语言包必须在 descriptor 中提供 `SourceAssembly` 和 embedded locpack resource name。生成的 registrar 使用 `SourceAssembly`，因此自包含单文件应用不依赖 `Assembly.Location`。descriptor 必须说明：

- Culture。
- PackageId。
- Source assembly。
- Resource base name。
- ContributionId。
- Version。
- Checksum。
- Owner / contribution：collectible plugin 必须先由 PluginSystem 加载，再贡献其 `SourceAssembly`；Localization 只在 owner revoke 时释放资源引用，不创建、选择或回落 AssemblyLoadContext。

### 4. 加载流程

```text
Language package descriptor
-> require its source assembly
-> open embedded locpack stream
-> validate and create immutable LanguagePackage
-> cache by (culture, packageId)
```

Host app 的静态 embedded 语言包直接引用 source assembly，兼容自包含单文件与 NativeAOT 发布。插件语言包 descriptor 引用已由 PluginSystem 加载的插件 assembly，并通过 owner/contribution 撤销先释放 package；ContributionLease 和 AssemblyLoadContext 均由 PluginSystem 管理。外部语言文件由 `FileLanguagePackageProvider` 加载 `.locpack`。

### 5. 卸载约束

如果语言包需要随插件卸载，必须避免外部强引用。

禁止：

- Host 静态缓存插件语言包 assembly。
- Host 静态缓存插件 `ResourceManager`。
- Host 静态缓存插件 localizer delegate。
- Host 持有插件语言包里的 Type。
- AtomUI/Avalonia ResourceDictionary 未移除就卸载插件。
- generated accessor 类型放进语言包 assembly。

生成的 key constants 和 registrar 位于模块或插件主 assembly。语言包 assembly 只提供资源数据。

### 6. 外部 Locpack 与 NativeAOT

外部、可替换资源统一使用 file-based locpack；NativeAOT 与普通 CoreCLR 使用相同格式。

当前格式是 schema v1 JSON locpack；binary 和 embedded generated table 尚无 Feature ID。

规则：

- locpack 不依赖动态 assembly loading。
- locpack descriptor 与 assembly package descriptor 语义一致。
- 应用或后续 Build 能力提供 file descriptor 和 locpack 产物；当前 Localization Generator 不生成 locpack 文件。
- 运行时通过 `FileLanguagePackageProvider` 加载当前 culture 的 locpack。
- schema v1 locpack 的 UTF-8 JSON 总大小上限为 16 MiB；读取期间观察取消，重复 JSON 根属性必须拒绝。

### 7. 安全和完整性

语言包 assembly / locpack 应支持：

- checksum。
- version。
- culture metadata。
- package compatibility。
- plugin ownership。

语言包不作为安全边界。不可信语言包仍然必须受 Host 安全策略和插件策略约束。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| Assembly descriptor 缺少 `SourceAssembly` | 返回 `InvalidDescriptor`；禁止根据 Location 动态加载。 |
| schema/version/id/culture/checksum 不匹配 | 返回对应稳定 `LocalizationErrorKind`，不发布 package。 |
| locpack 超过 16 MiB | 返回 `PackageTooLarge`，不继续分配或发布 package。 |
| embedded resource suffix 同时匹配多个资源 | 返回 `PackageNotFound`，不得按枚举顺序任取一个资源。 |
| checksum 不匹配 | 拒绝加载。 |
| culture 不匹配 | 拒绝加载。 |
| NativeAOT 发布 | 默认 provider 组合可用；generated embedded descriptor、file locpack 和 in-memory 路径均不得产生 trimming/AOT warning。 |
| 卸载后仍有引用 | 由 PluginSystem 的 unload/lease 诊断承接。 |

### 9. 测试策略

测试必须覆盖：

- 已知 source assembly embedded package 加载。
- source assembly package 在没有文件路径时加载。
- 缺少 SourceAssembly 或仅给出 assembly path 时稳定拒绝且不动态加载。
- descriptor 指定 source assembly/resource 解析。
- file locpack 加载。
- file locpack provider（Native AOT publish smoke 需由 Build/Release Gate 单独立项）。
- checksum mismatch。
- locpack size limit、duplicate root property 和 provider kind mismatch。
- embedded resource exact/unique suffix 解析。
- plugin package owner revoke 后释放对已加载 assembly 的引用。
- Host 不持有插件 package 引用。
- 默认 `AddLocalization()` 的双 TFM NativeAOT publish/run。
