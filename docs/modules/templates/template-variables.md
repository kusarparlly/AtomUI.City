# AtomUI.City.Templates Template Variables 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Template Variables` 相关实现决策，不重新定义模块边界。

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

## 模板变量设计

适用范围：模板变量、命名规则、命名空间、路径规则、默认值和参数校验

### 1. 目标

模板变量必须稳定、可验证，并且不把框架命名空间误用到用户项目中。

### 2. 核心变量

当前 public renderer/CLI 已实现 `AppName`、`RootNamespace`、`TargetFramework`、`UseAot`、`UseDynamicPlugins`、`IncludeTests` 和 `IncludeSample`。NuGet application template 暴露 name、`TargetFramework`、`IncludeSample`；NuGet plugin template 暴露 name、`PluginId`、`TargetFramework`。`ModuleName`、`PageName`、`RoutePath` 和独立 `PackageId` 属于 Planned Feature，不能视为现有 API。

| 变量 | 说明 |
|---|---|
| `AppName` | 应用名。 |
| `RootNamespace` | 用户项目根命名空间。 |
| `ModuleName` | 模块名。 |
| `PageName` | 页面名。 |
| `RoutePath` | 路由路径。 |
| `PluginId` | 插件运行时 Id。 |
| `PackageId` | NuGet 包 Id。 |
| `TargetFramework` | 目标框架。 |
| `UseAot` | 是否启用 AOT 友好默认设置。 |
| `UseDynamicPlugins` | 是否启用动态插件模式。 |
| `IncludeTests` | 是否生成测试项目，默认 true。 |
| `IncludeSample` | 是否生成示例内容，默认 false。 |

### 3. 命名空间规则

规则：

- 用户代码使用 `RootNamespace`。
- 用户项目不得使用精确的 `AtomUI.City` 或 `AtomUI.City.*` 命名空间；`AtomUI.Cityscape` 等不同标识符不属于保留空间。
- 生成测试项目使用 `<RootNamespace>.Tests`。
- 插件项目使用用户指定或派生的命名空间。
- 框架扩展方法来自 `AtomUI.City.*` 包。

### 4. 路径规则

规则：

- 应用代码进入 `src/`。
- 测试代码进入 `tests/`。
- 模板不直接写 `output/`。
- Build 负责生成 `output/`。
- 插件模板可以包含 `atomui-city/` 资源目录，但最终 manifest 由 Build 生成。

### 5. 默认值

建议默认：

| 变量 | 默认值 |
|---|---|
| `TargetFramework` | 当前框架支持的默认 TFM。 |
| `IncludeTests` | `true`。 |
| `IncludeSample` | `false`。 |
| `UseAot` | `false`。 |
| `UseDynamicPlugins` | `false`。 |
| `RootNamespace` | 为空白时使用 `AppName`。 |

### 6. 校验规则

必须校验：

- 名称是合法 C# identifier 或可转换。
- `RoutePath` 符合路由语法。
- `PluginId` 符合反向域名建议格式。
- `PackageId` 符合 NuGet 包名要求。
- `RootNamespace` 不得等于 `AtomUI.City` 或以 `AtomUI.City.` 开头。
- `TargetFramework` 必须采用 `net<major>.<minor>`，可带一个合法平台后缀，不接受任意文本或路径。
- `UseAot=true` 时不默认启用动态插件。
- 变量诊断必须包含 `variable`、`rawValue` 和 `rule`。
- 校验失败时 `ApplicationTemplateRenderer.Render` 不写任何文件。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 名称校验 | Unit | 合法和非法 identifier。 |
| 命名空间 | Unit | 不生成 `AtomUI.City.*` 用户命名空间。 |
| RoutePath | Unit | 路由语法校验。 |
| PluginId | Unit | 格式校验。 |
| AOT 变量 | Unit | AOT 与动态插件冲突诊断。 |
| Sample 开关 | Unit/TemplateSmoke | false 不生成 sample，true 生成明确位于 `Samples/` 的非业务示例。 |
| 双入口 | TemplateSmoke | renderer/CLI 与 NuGet template 的共同变量保持相同默认语义；真实实例化后不得残留 token。 |
