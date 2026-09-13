# AtomUI.City.Templates Application Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Application Template` 相关实现决策，不重新定义模块边界。

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

## 应用模板设计

适用范围：AtomUI.City 应用模板、Host 启动、模块入口、配置、本地化、测试项目和 Build 接入

### 1. 目标

当前应用模板用于创建一个可运行的 AtomUI.City Host 应用工作区。它体现框架默认启动、工程和测试范式，但不伪造尚未接线的 UI；真正的 Avalonia desktop lifetime 由 `AUC-TEMPLATES-010` 承接。

### 2. 默认结构

```text
<AppName>.slnx
Directory.Build.props
Directory.Packages.props
docs/<AppName>.md
src/<AppName>/
  <AppName>.csproj
  Program.cs
  Modules/
  Routes/
  Resources/
  Configuration/
  Localization/
tests/<AppName>.Tests/
  FeatureTestMatrix.md
  ApplicationSmokeTests.cs
```

### 3. 默认能力

应用模板默认包含：

- GenericHost 启动入口。
- AtomUI.City Core 配置。
- Lifecycle 接入。
- ModuleSystem 接入。
- Routing 接入。
- Localization 接入。
- `AtomUI.City.Build` 引用。
- 测试项目。
- 可运行的 Host 入口。
- solution、Directory.Build、Directory.Packages 隔离项和 docs entry。
- 生成内容不包含机器绝对路径。

可选启用：

- PluginSystem。
- Dynamic plugins。
- AOT-friendly metadata。

Security、Data、EventBus 等业务模块当前由开发者在生成后显式添加，模板没有对应开关。

### 4. Program 入口

模板生成的入口必须使用 Host builder 风格。

设计要求：

- 使用 .NET 扩展方法组织配置。
- 不在入口中写业务代码。
- 不直接构建 ServiceProvider 做服务解析。
- 不在入口中执行插件加载细节。
- 生命周期通过 Host 接入；Presentation runtime 在 `AUC-TEMPLATES-010` 完成后接入。

### 5. 项目文件

应用项目必须引用：

- `AtomUI.City.Core`
- `AtomUI.City.Mvvm`
- `AtomUI.City.Routing`
- `AtomUI.City.Localization`
- `AtomUI.City.Build`

可选引用：

- `AtomUI.City.State`
- `AtomUI.City.EventBus`
- `AtomUI.City.Data`
- `AtomUI.City.Security`
- `AtomUI.City.PluginSystem`

### 6. Build 接入

项目默认启用：

- manifest generation。
- 通过 `AtomUI.City.Build` 接入 analyzer 和 source generator 资产。

`UseAot` 当前设置 `AtomUICityAotFriendly` 元数据，不等同于直接设置 `PublishAot=true`。

### 7. 测试项目

默认生成测试项目：

```text
tests/<AppName>.Tests/
```

测试项目必须包含：

- `FeatureTestMatrix.md`
- 真实 Host start/stop smoke test。

### 8. Sample 策略

默认不生成业务页面。

如果提供 `IncludeSample=true`，示例必须明确标记为 sample，不能污染默认编程范式，也不能引入业务域概念作为默认结构。

### 9. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 应用生成 | Smoke | 文件结构完整。 |
| Solution 和工程对齐 | Smoke | `.slnx`、`Directory.Build.props`、`Directory.Packages.props`、docs entry 均存在并只使用相对路径；父级 CPM 不得改变生成项目语义。 |
| Host 启动 | Framework integration | 真实 Host 能启动并停止。 |
| Avalonia desktop 启动 | Platform integration | 归属 `AUC-TEMPLATES-010`，当前为 Planned。 |
| Build 接入 | Build | manifest 生成、Build 包和 analyzer 资产接入。 |
| 测试项目 | Unit | `FeatureTestMatrix.md` 和 smoke test 存在。 |
| 可选能力 | Unit/Build | tests、sample、AOT metadata 和 dynamic plugin 开关影响 plan、文件或项目引用。 |
