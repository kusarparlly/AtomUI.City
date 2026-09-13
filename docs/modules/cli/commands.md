# AtomUI.City.Cli Commands 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Commands` 相关实现决策，不重新定义模块边界。

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

## CLI 命令模型设计

适用范围：`atomui city` 命令树、命名规则、参数约定、通用选项和命令边界

### 1. 目标

命令模型必须稳定、清晰、可被人和 AI Agent 使用。

用户入口：

```bash
atomui city <command>
```

### 2. 命名规则

规则：

- 顶层命令组固定为 `atomui city`。
- 动作用动词：`new`、`generate`、`build`、`pack`、`publish`、`inspect`。
- 资源用名词：`module`、`page`、`plugin`、`test`、`manifest`。
- 插件管理命令位于 `plugin` 子命令下。
- 检查类命令使用 `check`。
- 解释诊断使用 `explain`。

### 3. 命令树

下列非插件 `generate` 子树属于 `AUC-CLI-007`。`generate plugin` 不属于当前 1.0 范围。

```text
atomui city
  new app
  generate module
  generate page
  generate test
  generate config
  generate localization
  build
  pack
  publish
  plugin list
  plugin inspect
  plugin install
  plugin update
  plugin remove
  plugin enable
  plugin disable
  plugin doctor
  inspect workspace
  inspect project
  inspect module
  inspect route
  inspect manifest
  docs check
  tests check
  doctor
  explain <diagnostic-code>
  plan <operation>
  apply <plan-file>
```

### 4. 通用选项

所有关键命令支持：

| 选项 | 说明 |
|---|---|
| `--json` | 输出机器可读 JSON。 |
| `--pretty` | JSON 格式化输出。 |
| `--no-color` | 禁止颜色。 |
| `--verbosity` | `quiet`、`normal`、`detailed`、`diagnostic`。 |
| `--non-interactive` | 禁止 prompt。 |
| `--dry-run` | 只输出计划，不写文件。 |
| `--yes` | 对需要确认的操作显式确认。 |
| `--working-directory` | 指定工作目录。 |

### 5. 参数规则

规则：

- 必填参数缺失时返回参数错误。
- `atomui city` 缺少子命令时返回 `AUCCLI0003` 并输出 usage。
- 未知 option 返回 `AUCCLI0004`，value option 缺值返回 `AUCCLI0005`，不得执行 handler。
- 非交互模式不提示补参。
- 参数名稳定，不随输出文本变化。
- 文件路径可相对工作目录解析。
- `--json` 模式错误也输出 JSON。

### 6. 命令边界

CLI 不做：

- 不实现 MSBuild task。
- 不维护模板文件内容。
- 不直接运行插件业务代码。
- 不扫描运行时程序集推断框架结构。
- 不绕过文档和测试门禁。

CLI 做：

- 调用 Templates。
- 调用 Build。
- 读取 manifest。
- 读取 PluginSystem metadata。
- 输出诊断。
- 生成 plan。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 命令解析 | Unit | 每个命令路径解析。 |
| 通用选项 | Unit | json、dry-run、non-interactive。 |
| 非法参数 | Unit | 缺失、未知、冲突参数。 |
| 命令边界 | Unit | generate 不直接调用 Build task 实现。 |
| 帮助输出 | Golden output | 命令帮助稳定。 |
