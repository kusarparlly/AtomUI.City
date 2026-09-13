# AtomUI.City.Cli Build Commands 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Build Commands` 相关实现决策，不重新定义模块边界。

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

## CLI Build 命令设计

适用范围：`atomui city build`、`pack`、`publish` 命令、Build 调用、诊断透传和输出规则

### 1. 目标

Build 命令负责调用 `AtomUI.City.Build` 定义的 MSBuild targets，不在 CLI 内重新实现构建逻辑。

### 2. 命令

```bash
atomui city build
atomui city pack
atomui city publish
```

### 3. 参数

| 参数 | 说明 |
|---|---|
| `--configuration` | Debug、Release。 |
| `--framework` | Target Framework。 |
| `--project` | 指定项目。 |
| `--output-root` | 覆盖 Build 输出根目录。 |
| `--strict-aot` | 启用严格 AOT 检查。 |
| `--json` | 输出结构化诊断。 |

### 4. 执行流程

```text
Inspect workspace
-> Resolve project
-> Build MSBuild invocation
-> Run Build target
-> Collect Build diagnostics
-> Map exit code
-> Emit CLI output
```

### 5. 规则

- 不绕过 MSBuild target。
- 默认遵守 `output/` 布局。
- Build diagnostic code 原样透传；CLI 命令级失败使用 `AUCCLI0201` 到 `AUCCLI0203`。
- CLI 可以补充命令级诊断，但不能改写 Build 错误语义。
- `--json` 输出包含 invocation、exitCode、stdout/stderr 摘要、durationMs 和 Build diagnostics。
- `--ci` 会在 invocation 中记录 CI mode，并向子进程设置 CI 环境。
- stdout/stderr 摘要必须截断，防止 agent 或 CI 读取无限日志。

### 6. Pack

`pack` 根据项目类型调用：

- 普通 NuGet pack。
- 插件 package target。
- 模板 package target。

插件 pack 必须执行 package layout validation。

### 7. Publish

`publish` 调用应用发布 target。

规则：

- Native AOT 兼容性由 Build 校验。
- 动态插件和 AOT 冲突由 Build diagnostic 表达。
- 发布输出进入 Build 定义的 publish layout。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| build | CLI/Build | target 调用、diagnostics 透传。 |
| pack | CLI/Build | plugin package validation。 |
| publish | CLI/Build | publish layout、AOT diagnostic。 |
| 参数映射 | Unit | configuration、framework、project。 |
| JSON 输出 | Unit | Build diagnostics envelope。 |
