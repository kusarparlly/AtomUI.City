# AtomUI.City.Templates Module Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Module Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-006`。状态：Completed。公开入口为 `GenerationTemplateRenderer` 的 Module kind。

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
| AUC-TEMPLATES-010 | Avalonia Desktop Application Template | Pending |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时 Host 不依赖 Templates` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## 模块模板设计

适用范围：模块类、模块依赖、服务注册、配置、Contribution、source generator 输入和模块测试

### 1. 目标

模块模板用于创建 AtomUI.City 模块骨架。模块是应用组成单元和能力贡献方，不是生命周期 Scope，也不是插件。

### 2. 默认结构

```text
src/<ProjectName>/Modules/<ModuleName>/
  <ModuleName>Module.cs
  <ModuleName>Options.cs
  <ModuleName>Contributions.cs
tests/<ProjectName>.Tests/Modules/<ModuleName>/
  <ModuleName>ModuleTests.cs
```

### 3. Module Id

规则：

- 默认 Module Id 使用模块类全名。
- 只有需要公开稳定 Id、跨版本兼容、插件发布或清单对外暴露时，才显式指定。
- 不强制要求用户写 `[Module("...")]`。

### 4. 模块内容

模板生成：

- Module 类。
- 模块依赖声明入口。
- 服务注册入口。
- 配置入口。
- Contribution 入口。
- source generator 可识别声明。
- 模块单元测试。
- 测试矩阵条目。

### 5. 服务注册

规则：

- 模块服务注册进入对应 ServiceCollection。
- 不在服务注册阶段构建 ServiceProvider。
- 插件模块不能修改 Host Root ServiceProvider。
- 自动服务注册必须 AOT 友好。

### 6. 配置

如果生成 Options：

- Options 有明确 section。
- 支持 validation。
- 支持 PreConfigure。
- 默认生成 Options 单元测试。

### 7. Contribution

如果模块贡献路由、权限、本地化、事件或 Data client：

- Contribution 必须可撤销。
- Contribution 必须能进入 manifest。
- ContributionId 必须稳定。
- 测试必须覆盖 lease 创建和撤销。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| Module Id | Unit | 默认全名、显式 Id。 |
| 依赖声明 | Unit | 拓扑排序、缺失依赖、循环依赖。 |
| 服务注册 | Unit | 服务进入正确容器，不构建 ServiceProvider。 |
| Options | Unit | binding、validation、PreConfigure。 |
| Contribution | Unit | request、lease、revoke。 |
| generator 输入 | Generator | 模块清单可生成。 |
