# AtomUI.City.Templates Detailed Design 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Detailed Design` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Templates` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-TEMPLATES-001 | Application Template | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-002 | Package Layout | TemplatePackageLayoutTests |
| AUC-TEMPLATES-003 | Template Variables | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-004 | Plugin Template | TemplatePackageLayoutTests |
| AUC-TEMPLATES-005 | Test Template | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-006 | Module Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-007 | Page Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-008 | Localization Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-009 | Configuration Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-010 | Avalonia Desktop Application Template | ApplicationTemplateBuildSmokeTests, DotnetNewTemplateIntegrationTests, ApplicationTemplateDesktopProcessTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时 Host 不依赖 Templates` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Templates 详细设计

适用范围：应用模板、模块模板、页面模板、插件模板、测试模板、本地化模板、配置模板、模板变量、生成输出和模板验证

### 1. 定位

`AtomUI.City.Templates` 负责把 AtomUI.City 的默认编程范式固化成可生成的项目结构。

Templates 不是简单创建空项目。它生成的工程必须符合 Core、Lifecycle、Routing、MVVM、Presentation、PluginSystem、Build 和 Testing 的约束。

CLI 负责调用模板和传递参数。Build 负责构建、manifest、打包和输出目录。Templates 负责生成默认结构、默认代码形态和默认测试入口。

### 2. 设计原则

| 原则 | 说明 |
|---|---|
| Programming-model-first | 模板必须体现 AtomUI.City 推荐写法。 |
| No business concepts | 默认不生成 Workbench、Dashboard、Documents 等业务概念。 |
| Build-compatible | 生成结果必须符合 Build 的 `output/`、manifest、MSBuild 和打包规则。 |
| Test-first | 每个模板默认生成测试项目或测试入口。 |
| AOT-friendly | 默认使用 source generator 和显式声明，不使用运行时扫描。 |
| Plugin-ready | 应用模板具备插件系统接入点，但不默认强制启用动态插件。 |
| Minimal but runnable | 生成的应用必须可运行，但只包含最小 App root。 |

### 3. 职责

Templates 负责：

- 应用模板。
- 模块模板。
- 页面模板。
- 插件模板。
- 测试模板。
- 本地化模板。
- 配置模板。
- 模板变量。
- 命名规则。
- 输出目录结构。
- 模板 smoke test 规范。

Templates 不负责：

- CLI 参数解析。
- MSBuild task 实现。
- 插件运行时安装。
- 真实部署。
- 业务样例功能。
- UI 自动化测试框架。

### 4. 模板类型

| 模板 | 用途 |
|---|---|
| Application template | 已实现：创建可运行的 AtomUI.City Avalonia 桌面应用工作区。 |
| Module template | 已实现：创建模块、依赖声明、服务注册和测试骨架。 |
| Page template | 已实现：创建 Route -> ViewModel Target -> View 的页面结构。 |
| Plugin template | 创建一个插件项目，一个插件一个主程序集，一个 NuGet 包。 |
| Test template | 创建符合功能点测试门禁的测试项目。 |
| Localization template | 已实现：创建语言资源和语言包结构。 |
| Configuration template | 已实现：创建 Options、配置 section、验证和测试结构。 |
| Avalonia desktop template | 已实现：创建 Application、desktop lifetime、DI 主窗口和 Presentation bootstrap。 |

### 5. 生成结果边界

生成的用户项目命名空间使用用户自己的 `RootNamespace`。

规则：

- 用户项目不放到 `AtomUI.City.*` 命名空间下。
- `AtomUI.City.*` 只属于框架。
- 模板生成的代码必须能被 source generator 识别。
- 模板生成的项目必须引用 `AtomUI.City.Build`。
- 模板生成的测试项目默认只引用被测项目和必要测试包；需要 `AtomUI.City.Testing` 能力时显式引入。
- 默认不生成业务页面和业务服务。

### 6. 应用结构

应用模板默认结构：

```text
src/<AppName>/
  <AppName>.csproj
  Program.cs
  App.axaml
  App.axaml.cs
  DesktopBootstrap.cs
  MainWindow.axaml
  MainWindow.axaml.cs
  Properties/AssemblyInfo.cs
  Modules/
  Routes/
  Resources/
  Configuration/
  Localization/
tests/<AppName>.Tests/
  FeatureTestMatrix.md
  ApplicationSmokeTests.cs
```

工作区根还必须生成 `<AppName>.slnx`、`Directory.Build.props`、`Directory.Packages.props` 和 docs entry；`Directory.Packages.props` 显式关闭父级 Central Package Management 继承。

详细规则见：[应用模板设计](application-template.md)。

### 7. 页面链路

页面模板必须体现：

```text
Route
-> ViewModel Target
-> ViewModel Activation
-> View
-> Outlet
-> VisualTree
```

规则：

- Routing 只负责 Route -> ViewModel Target。
- MVVM 负责 Activation、Command、Interaction、Validation。
- Presentation 负责 ViewModel -> View、Outlet commit 和 visual lifecycle feedback。
- AtomUI/Avalonia 负责控件、样式、主题和底层 UI 行为。

详细规则见：[页面模板设计](page-template.md)。

### 8. 插件结构

插件模板默认结构：

```text
src/<PluginName>/
  <PluginName>.csproj
  <PluginName>Module.cs
  Resources/
  Localization/
  atomui-city/
tests/<PluginName>.Tests/
  FeatureTestMatrix.md
  PluginPackageTests.cs
  PluginLifecycleTests.cs
```

规则：

- 一个插件一个主业务程序集。
- 一个插件发布成一个 NuGet 包。
- 默认生成 `AtomUICityPluginId`。
- 默认启用 manifest 生成。
- 默认生成插件测试。

详细规则见：[插件模板设计](plugin-template.md)。

### 9. 测试门禁

Templates 必须落实全局测试门禁：

- 每个功能点必须有单元测试。
- 集成测试不能替代单元测试。
- 生成 `FeatureTestMatrix.md`。
- 默认引用被测项目和必要测试包，不强制引入 `AtomUI.City.Testing`。
- 页面模板默认生成 routing 和 activation 测试入口。
- 插件模板默认生成 package、lease、cancellation、unload 测试入口。

详细规则见：[测试模板设计](test-template.md)。

### 10. 模板变量

模板变量包括：

- `AppName`
- `RootNamespace`
- `ModuleName`
- `PageName`
- `RoutePath`
- `PluginId`
- `PackageId`
- `TargetFramework`
- `UseAot`
- `UseDynamicPlugins`
- `IncludeTests`
- `IncludeSample`

详细规则见：[模板变量设计](template-variables.md)。

### 11. 测试矩阵

| 功能点 | 测试类型 | 测试工具 | 必测场景 |
|---|---|---|---|
| application template | Smoke/Build | TemplateSmokeTestHost | restore、build、test、Host start/stop、manifest 生成。 |
| module template | Unit/Build | GenerationTemplateRendererTests | 模块图、服务注册、source generator 输入。 |
| page template | Unit/Build | GenerationTemplateRendererTests | route match、ViewModel target、Presentation binding。 |
| plugin template | Build/Plugin | DotnetNewTemplateIntegrationTests | pack/install/instantiate、plugin.json、单主程序集、package layout、token 隔离。 |
| test template | Unit/Build | ApplicationTemplateBuildSmokeTests | 测试矩阵、项目引用边界、基础 smoke test。 |
| localization template | Unit/Build | GenerationTemplateRendererTests | culture 目录、resource manifest、fallback。 |
| configuration template | Unit/Build | GenerationTemplateRendererTests | Options binding、validation、reload policy。 |
| Avalonia desktop template | Headless/Platform | ApplicationTemplateBuildSmokeTests, ApplicationTemplateDesktopProcessTests | Application、desktop lifetime、主窗口、Presentation bootstrap。 |

完整测试规则见：[诊断和测试设计](diagnostics-and-testing.md)。

### 12. 完成标准

Templates 任一功能点完成必须满足：

- 对应模板文档已确认。
- 输出结构有测试矩阵条目。
- 生成结果符合 Build 文档。
- 生成结果包含测试入口。
- Smoke test 能验证生成项目可构建。
- 不生成默认业务概念。
