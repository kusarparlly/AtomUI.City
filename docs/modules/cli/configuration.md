# AtomUI.City.Cli Configuration 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Configuration` 相关实现决策，不重新定义模块边界。

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

## CLI 配置设计

适用范围：CLI 配置来源、工作区配置、用户配置、模板源、插件源、profile 和优先级

### 1. 目标

CLI 配置用于控制命令默认值、模板源、插件源、输出模式和自动化行为。CLI 配置不能替代应用运行时配置。

### 2. 配置来源

优先级从高到低：

```text
Command line arguments
-> Environment variables
-> Workspace CLI config
-> User CLI config
-> Defaults
```

### 3. 工作区配置

建议路径：

```text
.atomui/city/cli.json
```

用途：

- 默认 `AtomUICityOutputRoot`。
- 默认 template source。
- 默认 plugin source。
- 默认 PluginProfile。
- docs/tests gate 策略。
- CI 输出策略。

### 4. 用户配置

用户级配置用于非项目特定设置：

- 默认 template source。
- trusted plugin source。
- 输出偏好。
- telemetry 开关，如果该能力存在。

用户配置路径遵循平台约定，具体实现由 CLI 模块定义。

### 5. 环境变量

建议：

| 变量 | 用途 |
|---|---|
| `ATOMUI_CITY_OUTPUT_ROOT` | 输出根目录。 |
| `ATOMUI_CITY_NON_INTERACTIVE` | 强制非交互。 |
| `ATOMUI_CITY_NO_COLOR` | 禁用颜色。 |
| `ATOMUI_CITY_TEMPLATE_SOURCE` | 模板源。 |
| `ATOMUI_CITY_PLUGIN_SOURCE` | 插件源。 |

### 6. 配置规则

- 命令行参数优先。
- 非交互模式不能从 prompt 补配置。
- 配置读取错误必须输出诊断。
- `--json` 模式下配置诊断也使用 JSON。
- CLI 配置不能写入应用运行时配置。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 优先级 | Unit | 参数覆盖环境变量和配置文件。 |
| 工作区配置 | Unit | `.atomui/city/cli.json` 读取。 |
| 环境变量 | Unit | non-interactive、no-color。 |
| 配置错误 | Unit | JSON 无效、字段无效。 |
| 只读命令 | Unit | inspect 不修改配置。 |
