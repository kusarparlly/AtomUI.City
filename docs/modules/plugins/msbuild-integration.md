# AtomUI.City.PluginSystem MSBuild Integration 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `MSBuild Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

## Public Contract

- 只允许通过 `AtomUI.City.PluginSystem` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-PLUGIN-001 | Plugin Metadata | PluginDeclarationAttributeTests; PluginManifestTests |
| AUC-PLUGIN-002 | Dependency Validation | PluginDependencyTests |
| AUC-PLUGIN-003 | Package Installation | PluginPackageTests |
| AUC-PLUGIN-004 | Discovery | PluginLoadingTests |
| AUC-PLUGIN-005 | Loading | PluginLoadingTests |
| AUC-PLUGIN-006 | MSBuild Contract | PluginMsBuildContractTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## PluginSystem MSBuild 集成设计

适用范围：插件项目属性、Item、Target、清单生成、包验证和本地开发安装

### 1. 目标

插件开发不能要求开发者手写大量清单和包结构。框架应通过 MSBuild targets、tasks 和 source generator 生成稳定产物。

设计目标：

- 插件项目声明少量属性即可生成标准包。
- 清单、贡献索引、资源索引由构建期生成。
- 构建时发现包布局、能力、AOT、依赖问题。
- 打包结果与运行时安装规则一致。
- 支持本地开发安装到插件目录。

### 2. 插件项目属性

推荐插件项目：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AtomUICityPlugin>true</AtomUICityPlugin>
    <AtomUICityPluginId>com.company.sales</AtomUICityPluginId>
    <AtomUICityPluginDisplayNameKey>SalesPlugin.DisplayName</AtomUICityPluginDisplayNameKey>
    <AtomUICityPluginDescriptionKey>SalesPlugin.Description</AtomUICityPluginDescriptionKey>
    <AtomUICityMinHostVersion>1.0.0</AtomUICityMinHostVersion>
    <AtomUICityPluginApiVersion>1.0</AtomUICityPluginApiVersion>
    <AtomUICityPackageAsPlugin>true</AtomUICityPackageAsPlugin>
  </PropertyGroup>
</Project>
```

属性名使用 `AtomUICity` 前缀是为了避免和普通 NuGet/MSBuild 属性冲突，不代表公共 API 类型必须带 `City` 后缀或前缀。

### 3. 推荐属性

| 属性 | 说明 |
|---|---|
| `AtomUICityPlugin` | 标记项目为插件项目。 |
| `AtomUICityPluginId` | 插件运行时身份。 |
| `AtomUICityPluginVersion` | 插件版本，默认可来自 `Version`。 |
| `AtomUICityPluginPublisher` | 发布者。 |
| `AtomUICityPluginDisplayNameKey` | 显示名称本地化 key。 |
| `AtomUICityPluginDescriptionKey` | 描述本地化 key。 |
| `AtomUICityMinHostVersion` | 最小 Host 版本。 |
| `AtomUICityMaxHostVersion` | 最大 Host 版本。 |
| `AtomUICityPluginApiVersion` | 插件 API 版本。 |
| `AtomUICityPluginUnloadable` | 是否设计为可卸载。 |
| `AtomUICityPluginNativeAotCompatible` | 是否声明 AOT 兼容。 |
| `AtomUICityPluginResourceMode` | `Assembly`、`LocPack` 或 `Both`；默认 `Both`，`Assembly` 禁止声明外置 `AtomUICityLanguagePackage`。 |
| `AtomUICityPluginGenerateManifest` | 是否生成清单。 |
| `AtomUICityPluginValidateManifest` | 是否验证清单。 |
| `AtomUICityPackageAsPlugin` | 是否按插件包布局打包。 |
| `AtomUICityPluginDevelopmentMode` | 是否启用开发期本地安装辅助；默认关闭。显式调用安装 target 时写入 `AtomUICityPluginDevelopmentCachePath`（默认位于项目 `output/development/plugins`），不触碰真实用户插件目录。 |

### 4. 推荐 Item

| Item | 说明 |
|---|---|
| `AtomUICityPluginCapability` | 声明请求能力。 |
| `AtomUICityPluginDependency` | 声明插件依赖。 |
| `AtomUICityPluginContract` | 声明共享 contract 版本。 |
| `AtomUICityLanguagePackage` | 声明语言包。 |
| `AtomUICityPluginAsset` | 声明插件资产。 |
| `AtomUICityPluginNativeAsset` | 声明 native/RID 资产。 |
| `AtomUICityContributionManifest` | 声明额外贡献清单。 |

Item metadata 合同：

| Item | Metadata |
|---|---|
| `AtomUICityPluginCapability` | `Scope`，分号分隔，允许为空。 |
| `AtomUICityPluginDependency` | 可选 `VersionRange`。 |
| `AtomUICityPluginContract` | 可选 `VersionRange`，生成确定性 contracts contribution。 |
| `AtomUICityLanguagePackage` | 必需 `Culture`。 |
| `AtomUICityPluginAsset` | 可选 `TargetPath`，默认保持相对目录。 |
| `AtomUICityPluginNativeAsset` | 必需 `RuntimeIdentifier`。 |
| `AtomUICityContributionManifest` | 必需 `Type`；`Required` 默认 `true`；可选 `TargetPath`。 |

Capability、Dependency 和 Contribution 写入现有 `plugin.json` schema；Contract 生成独立 contribution；语言、普通和 native 资产只进入规范包布局，不扩展运行时 manifest 模型。

示例：

```xml
<ItemGroup>
  <AtomUICityPluginCapability Include="routes">
    <Scope>/sales/**</Scope>
  </AtomUICityPluginCapability>
  <AtomUICityPluginDependency Include="com.company.identity">
    <VersionRange>[1.0.0,2.0.0)</VersionRange>
  </AtomUICityPluginDependency>
  <AtomUICityLanguagePackage Include="Resources\zh-CN\*.resx">
    <Culture>zh-CN</Culture>
  </AtomUICityLanguagePackage>
</ItemGroup>
```

### 5. 推荐 Target

| Target | 说明 |
|---|---|
| `GenerateAtomUICityPluginManifest` | 生成 `plugin.json`。 |
| `GenerateAtomUICityContributionManifests` | 生成模块贡献清单。 |
| `ValidateAtomUICityPluginManifest` | 校验清单 schema 和字段。 |
| `ValidateAtomUICityPluginPackage` | 校验包布局和单主程序集规则。 |
| `PackAtomUICityPlugin` | 生成插件 NuGet 包。 |
| `InstallAtomUICityPluginToLocalCache` | 开发期安装到本机插件目录。 |
| `CleanAtomUICityPluginArtifacts` | 清理插件生成产物。 |

Target 应可被普通 `dotnet build`、`dotnet pack` 和 CI 调用。

### 6. Source Generator 分工

Source generator 负责生成编译期可知的索引：

- 模块清单。
- 路由清单。
- 权限清单。
- 本地化 key 索引。
- ViewModel/View 映射索引。
- Data client 代理索引。

MSBuild task 负责整合最终包级清单：

```text
Source generators
-> intermediate manifests
-> MSBuild task merges manifests
-> atomui-city/plugin.json
-> atomui-city/manifests/*.json
```

### 7. 输出路径和包内容

构建期 manifest 输出路径由 `PluginMsBuildContract.GetManifestOutputPath` 计算，必须位于规范化后的 intermediate output path 下：

```text
<IntermediateOutputPath>/AtomUI.City/plugin/atomui-city/plugin.json
```

插件包内容根由 `PluginMsBuildContract.PackageContentRoots` 声明：

| Root | 说明 |
|---|---|
| `lib/` | 主程序集和私有依赖。 |
| `atomui-city/plugin.json` | 插件清单。 |
| `atomui-city/manifests/` | 构建期贡献清单。 |
| `atomui-city/locales/` | 本地化程序集和 locpack。 |
| `atomui-city/assets/` | 图标、样式和其他插件资产。 |
| `runtimes/` | RID native assets。 |

这些字符串是 package layout 和 MSBuild target 的稳定边界，修改必须进入 compatibility。

### 8. 诊断代码

建议诊断：

| Code | 含义 |
|---|---|
| `AUCPLG1001` | 插件项目缺少 `AtomUICityPluginId`。 |
| `AUCPLG1002` | 插件包包含多个主程序集。 |
| `AUCPLG1003` | `DisplayNameKey` 缺失。 |
| `AUCPLG1101` | 能力声明格式无效。 |
| `AUCPLG1201` | 插件依赖版本范围无效。 |
| `AUCPLG1301` | AOT 兼容声明与使用能力冲突。 |
| `AUCPLG1401` | required contribution manifest 未生成。 |

### 9. 开发体验

开发期本地安装流程：

```text
dotnet pack
-> Validate plugin package
-> Install to development PluginProfile
-> Update development lock file
```

规则：

- 开发期安装必须进入 `dev` profile。
- 不应覆盖 stable profile。
- 开发期插件可以来自项目输出目录，但必须显式开启。
- 开发期路径必须进入诊断。

### 10. 测试要求

必须覆盖：

- 最小插件项目生成标准清单。
- 多模块插件生成模块索引。
- 缺少必填属性时报诊断。
- 包布局不合法时报诊断。
- 本地开发安装不会污染 stable profile。
- 生成清单字段顺序稳定。
