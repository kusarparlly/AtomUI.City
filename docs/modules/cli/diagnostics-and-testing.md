# AtomUI.City.Cli Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## CLI 诊断和测试设计

适用范围：CLI command tests、golden output、JSON schema、template/build/plugin integration smoke、docs/tests gate 和 AI 输出验证

### 1. 目标

CLI 每个功能点必须有测试。测试必须覆盖人类输出、JSON 输出、exit code、非交互行为和跨模块调用边界。

### 2. 测试工具

Testing 包应支持：

- `CliCommandTestHost`。
- `CliOutputAssertions`。
- `CliPlanTestHost`。
- `CliWorkspaceFixture`。
- `GoldenOutputAssertions`。
- `JsonSchemaAssertions`。
- `ExitCodeAssertions`。

### 3. 测试类型

| 类型 | 用途 |
|---|---|
| Unit test | 参数解析、配置、exit code、JSON schema。 |
| Golden output test | human help、no-color、verbosity。 |
| CLI integration test | command -> Templates/Build/Plugin metadata。 |
| Template smoke test | new/generate 输出可构建。 |
| Build integration test | build/pack/publish 调用 Build。 |
| Plugin integration test | 插件 plan/install/update/pending。 |

### 4. JSON Schema 测试

必须覆盖：

- success envelope。
- failure envelope。
- diagnostics array。
- suggestedActions。
- documentationLinks。
- schemaVersion。

### 5. Plan / Apply 测试

必须覆盖：

- plan 生成。
- dry-run 不写文件。
- apply 写文件。
- apply schema 校验。
- apply 文件冲突。
- rollback 信息存在。

### 6. Non-interactive 测试

必须覆盖：

- 缺参数不 prompt。
- 需要确认但缺 `--yes` 时失败。
- `--json` 下不输出普通文本。
- exit code 稳定。

### 7. 命令测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| command parsing | Unit | 每个命令路径。 |
| generate | CLI integration | module、page、plugin、test。 |
| build | CLI/Build | Build diagnostics 透传。 |
| plugin | CLI/Plugin | install dry-run、UnloadPending。 |
| inspect | Unit/CLI | 只读 JSON。 |
| docs check | Unit | 缺文档和缺矩阵。 |
| tests check | Unit | 缺单测和释放断言。 |
| explain | Unit | 已知和未知 code。 |

### 8. 测试隔离

CLI 测试必须使用临时工作区。

规则：

- 不使用真实用户插件目录。
- 不依赖真实 NuGet feed。
- 不修改开发者全局配置。
- 不依赖真实终端颜色能力。
