# AtomUI.City.Templates Configuration Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Configuration Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-009`。状态：Completed。公开入口为 `GenerationTemplateRenderer` 的 Configuration kind。

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

## 配置模板设计

适用范围：Options、配置 section、PreConfigure、配置验证、reloadable 配置和配置测试

### 1. 目标

配置模板用于生成符合 Core Configuration 约定的 Options 和配置结构。

### 2. 默认结构

```text
Configuration/
  <FeatureName>Options.cs
  <FeatureName>OptionsValidator.cs
tests/<ProjectName>.Tests/Configuration/
  <FeatureName>OptionsTests.cs
```

### 3. Options

规则：

- Options 必须有明确 section。
- Options 名称使用用户命名空间。
- 默认生成 validation。
- reloadable 必须显式声明。
- 插件 Options 必须按 PluginId 分区。

### 4. PreConfigure

模板可生成 PreConfigure 入口。

规则：

- PreConfigure 用于模块默认值和提前配置。
- PreConfigure 不执行 IO。
- PreConfigure 不构建 ServiceProvider。
- 插件拥有自己的 PreConfigure store。

### 5. 配置文件

模板可以生成：

```text
appsettings.json
appsettings.Development.json
```

规则：

- 默认配置最小化。
- 不生成业务配置项。
- 插件默认配置进入插件资源或 manifest。

### 6. 测试

默认生成：

- binding test。
- validation test。
- PreConfigure test。
- reloadable test，如果启用。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| Options section | Unit | section name 稳定。 |
| binding | Unit | 配置绑定成功。 |
| validation | Unit | 成功和失败。 |
| PreConfigure | Unit | 默认值和覆盖顺序。 |
| plugin config | Unit | 插件配置隔离。 |
