# AtomUI.City.Templates Test Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Test Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-005`。状态：Completed。当前随应用和插件工作区生成测试项目，不提供独立 `dotnet new` short name。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## 测试模板设计

适用范围：测试项目、功能点测试矩阵、TestHost、单元测试、集成测试和模板默认测试入口

### 1. 目标

测试模板用于落实 AtomUI.City 的功能点测试门禁。

规则：

- 每个功能点必须有单元测试。
- 集成测试不能替代单元测试。
- 无法单元测试的功能点必须说明原因并提供替代测试。

### 2. 默认结构

```text
tests/<ProjectName>.Tests/
  FeatureTestMatrix.md
  <FeatureName>Tests.cs
```

可选结构：

```text
tests/<ProjectName>.FrameworkIntegrationTests/
tests/<ProjectName>.PlatformIntegrationTests/
```

### 3. FeatureTestMatrix

默认生成：

```text
| 功能点 | 测试类型 | 测试工具 | 必测场景 | 完成门禁 |
|---|---|---|---|---|
```

规则：

- 模板生成的功能点必须自动写入矩阵。
- 新增页面、模块、插件时必须补矩阵。
- 集成测试条目不能替代单元测试条目。

### 4. TestHost

测试项目默认引用：

- `AtomUI.City.Testing`
- 被测项目。

默认生成 TestHost 使用入口：

- application smoke。
- module host。
- routing host。
- plugin host，如果是插件模板。

### 5. 单元测试默认项

模板应生成最小单元测试：

- 构造测试。
- manifest/generator 输入测试。
- lifecycle 或 activation 测试。
- diagnostics 测试，如果模板生成诊断行为。

### 6. 集成测试默认项

可选生成：

- Framework integration test。
- Platform integration test。
- Template smoke test。

默认不生成真实 UI 平台集成测试，除非用户显式选择。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 测试项目生成 | Smoke | restore、build。 |
| FeatureTestMatrix | Unit | 文件存在，包含模板功能点。 |
| TestHost 引用 | Unit/Build | 测试项目能使用 TestHost。 |
| 单元测试入口 | Unit | 默认测试可运行。 |
| 集成测试开关 | Build | 显式启用时生成对应项目。 |
