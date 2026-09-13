# AtomUI.City.Cli Project Creation 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Project Creation` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Cli` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-CLI-001 | Command Model | CliCommandArchitectureTests |
| AUC-CLI-002 | New App | CliNewAppTests |
| AUC-CLI-003 | Build and Test | CliBuildAndTestCommandTests |
| AUC-CLI-004 | Plugin Inspect Doctor | CliInspectDoctorPluginTests |
| AUC-CLI-005 | AI Envelope | CliCommandArchitectureTests |
| AUC-CLI-006 | Non-Interactive and CI Mode | CliCommandArchitectureTests |
| AUC-CLI-007 | Generation Commands | CliGenerationCommandTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation runtime UI` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## CLI 项目创建设计

适用范围：`atomui city new app`、应用模板调用、参数校验、生成计划、测试项目和 Build 接入

### 1. 目标

项目创建命令调用 Templates 应用模板，生成符合 AtomUI.City 编程范式的最小可运行应用。

命令：

```bash
atomui city new app <AppName>
```

### 2. 参数

建议参数：

| 参数 | 说明 |
|---|---|
| `AppName` | 应用名。 |
| `--namespace` | RootNamespace。 |
| `--target-framework` | 目标框架。 |
| `--sample` | 在 `Samples/` 下生成不含业务领域概念的最小 ViewModel 示例。 |
| `--output` | 输出目录。 |
| `--include-tests` | 是否生成测试项目，默认 true。 |
| `--use-aot` | 是否启用 AOT 友好默认设置。 |
| `--use-dynamic-plugins` | 是否启用动态插件模式。 |
| `--sample` | 是否生成 sample，默认 false。 |

### 3. 执行流程

```text
Parse arguments
-> Validate template variables
-> Detect target directory
-> Build creation plan
-> Invoke application template
-> Write files
-> Run optional restore/build if requested
-> Emit diagnostics
```

### 4. 规则

- 默认生成测试项目。
- 默认不生成业务页面。
- 默认不启用动态插件。
- `AppName` 必须是合法 C# identifier；非法值返回 `AUCCLI0104`，不写文件。
- `--use-aot` 和 `--use-dynamic-plugins` 冲突时返回 `AUCCLI0103`。
- 用户命名空间不能以 `AtomUI.City` 开头，违规返回 `AUCCLI0102`。
- 目标文件已存在时返回 `AUCCLI0105`，不得覆盖已有文件。
- 取消时返回 `AUCCLI0106`，JSON 输出包含已计算的 plan/artifacts，且不得写入新文件。
- 生成项目必须引用 `AtomUI.City.Build`。

### 5. Dry-run

```bash
atomui city new app SalesClient --dry-run --json
```

输出 `data.plan` 和 `data.artifacts`，列出将创建的目录、文件、项目引用、测试项目和风险；dry-run 不写文件。

### 6. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 参数校验 | Unit | 缺 AppName、非法 namespace。 |
| dry-run | CLI | 不写文件，输出 plan。 |
| 应用生成 | Template smoke | 文件结构完整。 |
| 测试项目 | Template smoke | FeatureTestMatrix 存在。 |
| Build 接入 | Build smoke | 生成项目可 build。 |
