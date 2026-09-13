# AtomUI.City.Templates Page Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Page Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-007`。状态：Completed。公开入口为 `GenerationTemplateRenderer` 的 Page kind，Presentation 生成合同已稳定。

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

## 页面模板设计

适用范围：页面路由、ViewModel Target、ViewModel、View、Outlet、Activation 和页面测试

### 1. 目标

页面模板用于创建一条完整页面进入链路，但不把业务概念写进模板。

页面链路：

```text
Route
-> ViewModel Target
-> ViewModel Activation
-> View
-> Outlet
-> VisualTree
```

### 2. 默认结构

```text
src/<ProjectName>/Routes/<PageName>/
  <PageName>Route.cs
  <PageName>ViewModel.cs
  <PageName>View.axaml
  <PageName>View.axaml.cs
tests/<ProjectName>.Tests/Routes/<PageName>/
  <PageName>RouteTests.cs
  <PageName>ViewModelTests.cs
```

### 3. 路由职责

模板生成的 route 声明只表达：

- RouteId。
- RoutePath。
- ViewModel Target。
- 参数。
- Outlet。
- 权限元数据，如果用户选择生成。
- 本地化标题 key，如果用户选择生成。

Routing 不生成 View 映射。View 映射由 Presentation 模板声明。

### 4. ViewModel

ViewModel 默认包含：

- Activation 入口。
- CancellationToken 使用示例。
- Command 示例，只有用户选择时生成。
- Validation 示例，只有用户选择时生成。
- Interaction 示例，只有用户选择时生成。

默认不生成业务字段。

### 5. View

View 默认包含最小可渲染结构。

规则：

- 不生成业务布局。
- 不生成过度装饰。
- View 和 ViewModel 绑定必须可被 Presentation source generator 识别。
- View code-behind 不写业务逻辑。

### 6. 测试

页面模板默认生成：

- route match 测试。
- ViewModel target 测试。
- activation 测试。
- cancellation 测试。
- Presentation fake commit 测试，如果启用 View。

真实 visual tree 测试放平台集成测试。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| RoutePath | Unit | match、参数、约束失败。 |
| ViewModel Target | Unit | target 解析。 |
| Activation | Unit | activate、cancel、dispose。 |
| View mapping | Generator/Unit | ViewModel -> View 映射生成。 |
| Outlet commit | Framework integration | fake outlet commit。 |
