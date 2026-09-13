# AtomUI.City.Templates Plugin Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-004`。状态：Completed。模板包必须通过真实 `dotnet new install` 和实例化测试，项目名替换不得修改 `AtomUICityPlugin*` MSBuild 属性名。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

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

## 插件模板设计

适用范围：插件项目、PluginId、主程序集、模块、manifest、资源、打包配置和插件测试

### 1. 目标

插件模板用于创建符合 PluginSystem 和 Build 约定的插件项目。

第一版规则：

- 一个插件一个主业务程序集。
- 一个插件发布成一个独立 NuGet 包。
- 插件通过普通模块贡献能力。
- 插件不修改 Host Root ServiceProvider。

### 2. 默认结构

```text
<PluginName>.slnx
Directory.Build.props
src/<PluginName>/
  <PluginName>.csproj
  <PluginName>Module.cs
  atomui-city/plugin.json
tests/<PluginName>.Tests/
  FeatureTestMatrix.md
  PluginPackageTests.cs
```

### 3. 项目属性

插件项目默认包含：

```xml
<AtomUICityPlugin>true</AtomUICityPlugin>
<AtomUICityPluginId>...</AtomUICityPluginId>
<AtomUICityPluginDisplayNameKey>...</AtomUICityPluginDisplayNameKey>
<AtomUICityPluginDescriptionKey>...</AtomUICityPluginDescriptionKey>
<AtomUICityPluginApiVersion>1.0</AtomUICityPluginApiVersion>
<AtomUICityPackageAsPlugin>true</AtomUICityPackageAsPlugin>
```

规则：

- `PluginId` 默认由模板变量生成。
- `DisplayName` 和 `Description` 使用本地化 key。
- 默认启用 manifest 生成和 package layout validation。

### 4. 插件模块

插件模块使用普通 Module 抽象。

规则：

- 不生成单独的公共 `PluginModule` 基类。
- 默认 module 继承 Core `ModuleBase`；依赖和 contribution 由开发者按 PluginSystem 合同显式添加。

### 5. 资源和本地化

当前模板不生成资源和本地化目录。插件资源模板属于 `AUC-TEMPLATES-008` 落地后的扩展，不得视为 AUC-TEMPLATES-004 的现有输出。

### 6. 打包

插件模板必须生成符合 Build 插件打包规则的项目：

- plugin csproj 必须声明 `PackageReadmeFile`，并将根目录 `README.md` 打入包根。
- plugin csproj 必须显式将 `atomui-city/plugin.json` 打入包内同名目录，不能依赖空的 Build target 隐式完成。
- `atomui-city/plugin.json` 在模板中提供可读初始 manifest，并由 Build 生成流程继续验证或覆盖。
- contribution manifests 由 source generator 和 Build task 生成。
- package 输出到 `output/packages/plugins`。
- 包布局必须通过 validation。

### 7. 测试

生成工作区默认提供 module contract test。模板仓库自身通过 `TemplatePackageLayoutTests` 检查 manifest/MSBuild/package layout，并通过 `DotnetNewTemplateIntegrationTests` 检查真实安装、实例化和 token 隔离。插件 load/unload、lease、operation cancellation 属于 PluginSystem 的测试责任，不由空插件骨架伪造通过。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| PluginId | Unit/Build | 生成、格式、manifest 字段。 |
| 包布局 | Build | 单主程序集、plugin.json、资源。 |
| 模块合同 | Unit | 生成 module 继承 `ModuleBase`。 |
| 模板引擎 | TemplateSmoke | 项目名、PluginId、TFM 分别替换，框架 MSBuild 属性名不变。 |
