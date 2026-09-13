# AtomUI.City.Templates Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

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
| AUC-TEMPLATES-010 | Avalonia Desktop Application Template | ApplicationTemplateBuildSmokeTests, DotnetNewTemplateIntegrationTests, ApplicationTemplateDesktopProcessTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时 Host 不依赖 Templates` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Templates 诊断和测试设计

适用范围：模板诊断、变量校验、模板生成测试、构建测试、smoke test 和功能点测试矩阵

### 1. 目标

Templates 的生成结果必须可构建、可测试、符合 Build 约定，并且不生成违反框架编程范式的结构。

### 2. 诊断

模板诊断至少包含：

- template id。
- variable name。
- output path。
- project name。
- diagnostic code。
- severity。
- remediation message。

### 3. 错误码登记

下表中 `Current` 由当前源码产生；`Planned` 只表示未来可选的诊断细化，不属于可依赖的 1.0 诊断合同，也不影响对应 Feature 的完成状态。

| Code | 含义 | 状态 |
|---|---|---|
| `AUCTPL0001` | 模板变量无效。 | Current |
| `AUCTPL0002` | `RootNamespace` 不能使用框架命名空间。 | Current |
| `AUCTPL2001` | generation 名称或依赖类型无效。 | Current |
| `AUCTPL2002` | generation 工程名无效。 | Current |
| `AUCTPL2003` | generation namespace 无效。 | Current |
| `AUCTPL2004` | page RoutePath 缺失或无效。 | Current |
| `AUCTPL2005` | localization culture 缺失、重复或无效。 | Current |
| `AUCTPL0201` | PluginId 无效。 | Planned；当前由 PluginSystem Build 校验承接 |
| `AUCTPL0301` | AOT 模式和动态插件配置冲突。 | Current |
| `AUCTPL0401` | 生成结果缺少测试矩阵。 | Planned |
| `AUCTPL0501` | 生成结果不符合 Build 输出约定。 | Planned |
| `AUCTPL1001` | 模板路径非法或逃逸 package root。 | Current |
| `AUCTPL1002` | 模板计划包含重复 normalized path。 | Current |
| `AUCTPL1003` | 模板计划包含不支持的 change type。 | Current |
| `AUCTPL1004` | 输出目标已存在。 | Current |
| `AUCTPL1005` | 输出预检或写入失败。 | Current |
| `AUCTPL1006` | 写入失败后的回滚失败。 | Current |

### 4. 测试类型

| 类型 | 用途 |
|---|---|
| Unit test | 变量校验、路径计算、命名规则。 |
| Snapshot test | 输出文件结构和关键文件内容。 |
| Build test | 生成项目 restore/build。 |
| Smoke test | 应用模板、插件模板、测试模板端到端生成。 |
| Plugin package test | 插件模板打包和 layout validation。 |

### 5. Smoke 测试

必须覆盖：

- 应用模板生成。
- 插件模板生成。
- 测试模板生成。
- 生成后 restore。
- 生成后 build。
- manifest 生成。
- 插件包 layout validation。

模块、页面、本地化和配置由 `GenerationTemplateRendererTests` 覆盖同一工作区真实 restore/build/test；Avalonia desktop application 已由 `AUC-TEMPLATES-010` 覆盖生成结构、Headless XAML/Presentation 和 Windows 原生进程三层 smoke。

### 6. 禁止事项测试

必须断言模板不会生成：

- 默认业务概念。
- 用户代码中的 `AtomUI.City.*` 命名空间。
- 未进入测试矩阵的功能点。
- 不符合 Build 文档的输出配置。
- 运行时扫描作为默认发现机制。
- 插件修改 Host Root ServiceProvider 的代码。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| variable validation | Unit | 缺失、非法、冲突变量。 |
| application template | Smoke/Build | 生成、restore、build。 |
| module template | Snapshot/Build | 文件结构、模块 manifest 输入。 |
| page template | Snapshot/Unit | route、ViewModel、View、测试入口。 |
| plugin template | Build/Plugin | package、manifest、unload test 入口。 |
| test template | Snapshot | `FeatureTestMatrix.md` 生成。 |
| no business defaults | Snapshot | 默认无业务页面。 |
