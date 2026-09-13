# AtomUI.City.Cli Non Interactive And Ci 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Non Interactive And Ci` 相关实现决策，不重新定义模块边界。

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

## CLI 非交互和 CI 设计

适用范围：非交互模式、CI 模式、exit code、JSON 输出、确认策略和自动化安全

### 1. 目标

CLI 必须适合 CI 和 AI Agent 自动化执行。自动化模式下不能出现隐藏 prompt、不可预测输出或不稳定 exit code。

### 2. 非交互模式

启用方式：

```bash
atomui city build --non-interactive
```

或环境变量：

```text
ATOMUI_CITY_NON_INTERACTIVE=true
```

规则：

- 不允许 prompt。
- 缺少必填参数直接失败。
- 需要确认的操作必须要求 `--yes` 或 apply plan。
- 错误必须输出诊断。
- `--ci`、`--non-interactive`、`ATOMUI_CITY_NON_INTERACTIVE=true` 或 stdin unavailable 都进入非交互模式。
- 非交互模式下缺少 `--yes` 返回 `AUCCLI0401`，不会读取 stdin。

### 3. CI 模式

CI 模式建议组合：

```bash
atomui city docs check --json --non-interactive --no-color
atomui city tests check --json --non-interactive --no-color
atomui city build --json --non-interactive --no-color
```

规则：

- 输出可被日志系统保存。
- JSON 输出不混入普通文本。
- exit code 稳定。
- 不使用交互选择。
- CI mode 会传递到 `DotnetInvocation.CiMode`，子进程环境设置 `CI=true` 和 `DOTNET_CLI_TELEMETRY_OPTOUT=1`。

### 4. Exit Code

建议：

| Exit Code | 含义 |
|---|---|
| `0` | 成功。 |
| `1` | 一般错误。 |
| `2` | 参数错误。 |
| `3` | 文档门禁失败。 |
| `4` | 测试门禁失败。 |
| `5` | Build 失败。 |
| `6` | Template 失败。 |
| `7` | Plugin 操作失败。 |
| `8` | Plan/apply 失败。 |

### 5. 确认策略

写操作分级：

| 操作 | 自动化要求 |
|---|---|
| 创建文件 | 支持 dry-run，非交互可执行。 |
| 修改文件 | 支持 plan/apply 或 `--yes`。 |
| 删除文件 | 必须 dry-run 或 apply plan。 |
| 插件 install/update/remove | 必须支持 plan/apply。 |
| 清理 output | 必须显式命令和确认。 |

### 6. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| non-interactive | Unit | 不 prompt。 |
| missing args | Unit | 失败并输出诊断。 |
| exit code | Unit | docs/tests/build/plugin failure。 |
| JSON only | Unit | 无彩色文本混入。 |
| confirmation | Unit | destructive 操作需要确认。 |
