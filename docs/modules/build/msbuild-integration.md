# AtomUI.City.Build MSBuild Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `MSBuild Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Build` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-BUILD-001 | Output Layout | OutputLayoutTests |
| AUC-BUILD-002 | Package Metadata | PackageMetadataTests |
| AUC-BUILD-003 | Project Inventory | ProjectInventoryTests |
| AUC-BUILD-004 | Dependency Boundary | ProjectDependencyBoundaryTests |
| AUC-BUILD-005 | Source Generator Packaging | SourceGeneratorProjectStructureTests |
| AUC-BUILD-006 | Release Gates | PackagingReleaseGateTests; EngineeringGateTests |
| AUC-BUILD-008 | MSBuild Transitive Assets | BuildAssemblyTests; SourceGeneratorProjectStructureTests; ProjectInventoryTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时包不依赖 Build 生产程序集` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Build MSBuild 集成设计

适用范围：Build props、targets、tasks、MSBuild 属性、Item、Target 和 buildTransitive 分发

### 1. 目标

Build 通过 MSBuild 集成把框架约定接入标准 `dotnet build`、`dotnet pack`、`dotnet publish` 和 CI。

设计目标：

- 应用项目少配置即可获得框架构建能力。
- 插件项目声明属性即可生成标准插件包。
- generator/analyzer 通过 build assets 自动接入。
- 所有 target 可被 CLI 和 CI 调用。
- 构建失败有稳定 diagnostic code。

### 2. Build Assets

建议包内布局：

```text
buildTransitive/
  AtomUI.City.Build.contract.json
  AtomUI.City.Build.props
  AtomUI.City.Build.targets
  AtomUI.City.Application.targets
  AtomUI.City.Plugin.targets
  AtomUI.City.Core.Diagnostics.targets
analyzers/
  dotnet/cs/AtomUI.City.Generators.dll
tools/
  net10.0/AtomUI.City.Build.Tasks.dll
```

规则：

- `buildTransitive` 用于应用和插件项目自动获得构建规则。
- `AtomUI.City.Build.contract.json` 是 Property、Item、Target、默认值、可见性和 package asset 的机器可读 Preview baseline；测试必须与真实 MSBuild XML 精确比较。
- Roslyn generator/analyzer 作为 analyzer asset 引入。
- MSBuild task 不进入运行时包主链路。
- Build 包不得包含 `lib/` 运行时程序集；它只分发 buildTransitive、analyzer 和 tools 资产。
- `AtomUI.City.Build.Tasks` 是 tools-only internal contract，由 targets 自动加载；应用开发者不得直接引用。
- 标准应用和插件只引用 `AtomUI.City.Build`，模板用 `PrivateAssets=all` 与 `IncludeAssets=build;buildTransitive;analyzers` 防止 Build 程序集进入应用运行时。

### 3. 推荐属性

| 属性 | 默认值 | 说明 |
|---|---|---|
| `AtomUICityOutputRoot` | `output` | 框架输出根目录。 |
| `AtomUICityGenerateManifests` | `true` | 是否生成 manifest。 |
| `AtomUICityValidateManifests` | `true` | 是否校验 manifest。 |
| `AtomUICityEnableAnalyzers` | `true` | 是否启用 analyzer。 |
| `AtomUICitySourceGenerationMode` | `Strict` | `Strict`、`Compatible`、`Off`。 |
| `AtomUICityStrictAot` | `false` | 是否启用严格 AOT 检查。 |
| `AtomUICityPackagePlugin` | `false` | 是否打包插件。 |
| `AtomUICityPackageApplication` | `false` | 是否打包应用。 |
| `AtomUICityPluginProfile` | 空 | 插件兼容 profile。 |
| `AtomUICityBuildDiagnosticsLevel` | `Normal` | 诊断详细程度。 |
| `AtomUICityApplicationId` | `AssemblyName` | application manifest 稳定身份。 |

`application.manifest.json` v1 固定包含 `schemaVersion`、`appId`、`frameworkVersion`、`pluginApiVersion`、`pluginProfile`、`targetFramework`、`runtimeIdentifier`、`aotMode`、`staticPlugins`、`resourcePacks`；文件项包含规范相对路径与 SHA-256。

### 4. 插件属性

插件属性沿用 PluginSystem 设计：

- `AtomUICityPlugin`
- `AtomUICityPluginId`
- `AtomUICityPluginVersion`
- `AtomUICityPluginPublisher`
- `AtomUICityPluginDisplayNameKey`
- `AtomUICityPluginDescriptionKey`
- `AtomUICityMinHostVersion`
- `AtomUICityMaxHostVersion`
- `AtomUICityPluginApiVersion`
- `AtomUICityPluginUnloadable`
- `AtomUICityPluginNativeAotCompatible`
- `AtomUICityPluginResourceMode`
- `AtomUICityPackageAsPlugin`

属性名使用 `AtomUICity` 前缀是为了避免和普通 MSBuild 属性冲突。

### 5. 推荐 Item

| Item | 说明 |
|---|---|
| `AtomUICityPluginCapability` | 插件请求能力。 |
| `AtomUICityPluginDependency` | 插件依赖。 |
| `AtomUICityPluginContract` | 共享 contract 版本。 |
| `AtomUICityLanguagePackage` | 语言包。 |
| `AtomUICityPluginAsset` | 插件资产。 |
| `AtomUICityPluginNativeAsset` | native/RID 资产。 |
| `AtomUICityContributionManifest` | 额外贡献清单。 |
| `AtomUICityStaticPlugin` | 应用静态插件引用。 |
| `AtomUICityResourcePack` | 资源包。 |

### 6. 推荐 Target

| Target | 说明 |
|---|---|
| `GenerateAtomUICityManifests` | 生成模块和应用级 manifest。 |
| `ValidateAtomUICityManifests` | 校验 manifest。 |
| `GenerateAtomUICityPluginManifest` | 生成插件 `plugin.json`。 |
| `ValidateAtomUICityPluginPackage` | 校验插件包布局。 |
| `PackAtomUICityPlugin` | 生成插件 NuGet 包。 |
| `PublishAtomUICityApplication` | 发布应用。 |
| `ValidateAtomUICityAotCompatibility` | AOT/trimming 兼容检查。 |
| `WriteAtomUICityBuildDiagnostics` | 写入构建诊断。 |
| `CleanAtomUICityOutput` | 清理框架输出。 |

### 7. Target 顺序

推荐顺序：

```text
CoreCompile
-> GenerateAtomUICityManifests
-> ValidateAtomUICityManifests
-> ValidateAtomUICityAotCompatibility
-> PackAtomUICityPlugin
-> PublishAtomUICityApplication
-> WriteAtomUICityBuildDiagnostics
```

具体接入点以 .NET SDK target graph 为准，但 Build 文档必须保证顺序语义稳定。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| props 导入 | Build test | 应用项目自动获得默认属性。 |
| targets 导入 | Build test | target 可被调用。 |
| 属性覆盖 | Build test | output、strict AOT、plugin profile 生效。 |
| Item 收集 | Build test | language package、asset、capability 被收集。 |
| target 顺序 | Build test | manifest 先于 package validation。 |
| diagnostics | Build test | 失败 target 输出稳定 code。 |
