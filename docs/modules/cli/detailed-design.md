# AtomUI.City.Cli Detailed Design 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Detailed Design` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Cli 详细设计

适用范围：CLI 定位、命令模型、AI 友好能力、模板调用、构建调用、插件管理、诊断、配置、非交互模式和测试

### 1. 定位

`AtomUI.City.Cli` 是开发者、CI 和 AI Agent 使用 AtomUI.City 工程化能力的统一入口。

用户命令入口统一为：

```bash
atomui city <command>
```

CLI 负责命令交互、参数解析、模板调用、Build 调用、诊断输出和工作区检查。

CLI 不定义项目结构，不实现 MSBuild task，不维护模板内容，不直接加载插件程序集。项目结构由 Templates 定义，构建、manifest、打包、发布由 Build 定义，运行时插件由 PluginSystem 定义。

### 2. 设计原则

| 原则 | 说明 |
|---|---|
| Thin orchestration | CLI 只编排 Templates、Build、Testing、PluginSystem metadata，不复制其规则。 |
| AI-friendly first | 关键命令支持机器可读输出、dry-run、plan/apply、稳定 exit code。 |
| Non-interactive automation | CI/Agent 模式下不允许隐藏 prompt。 |
| Documentation-aware | 能检查文档先行、模块文档、测试矩阵。 |
| Test-gate aware | 能检查功能点是否有单元测试或替代测试说明。 |
| Diagnostic-code-first | 错误必须有稳定 code、原因、修复建议和文档链接。 |
| Workspace-aware | 能结构化输出解决方案、项目、模块、路由、插件、manifest 和测试状态。 |
| Build-compatible | build、pack、publish 命令调用 Build，不绕过 Build 输出规则。 |
| Template-compatible | new、generate 命令调用 Templates，不在 CLI 内硬编码模板结构。 |

### 3. 命令树

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

详细命令规则见：[命令模型设计](commands.md)。

### 4. AI 友好能力

所有关键命令支持：

```text
--json
--pretty
--no-color
--verbosity quiet|normal|detailed|diagnostic
--non-interactive
--dry-run
```

复杂操作支持：

```bash
atomui city plan generate page Orders/List --route /orders --json
atomui city apply .city/plans/2026-06-11-generate-page.json
```

AI 友好能力详见：[AI 集成设计](ai-integration.md)。

### 5. 执行管线

CLI 命令执行管线：

```text
Parse command
-> Load CLI configuration
-> Detect workspace
-> Validate arguments
-> Resolve command service
-> Build execution plan if needed
-> Execute Templates / Build / metadata operation
-> Collect diagnostics
-> Write human output or JSON output
-> Return stable exit code
```

规则：

- 参数校验必须发生在写文件前。
- `--dry-run` 不能写业务文件。
- `--json` 输出不能混入彩色文本。
- 非交互模式缺参数必须失败并输出诊断。
- destructive 操作必须支持 `--dry-run` 或 plan/apply。

### 6. Workspace Awareness

CLI 必须能输出当前工作区结构化事实：

```bash
atomui city inspect workspace --json
```

输出包括：

- solution。
- projects。
- packages。
- modules。
- routes。
- plugins。
- generated manifests。
- docs status。
- test matrix status。
- build output status。

inspect 命令只读，不修改文件。

### 7. Docs 和 Tests Gate

CLI 提供治理检查：

```bash
atomui city docs check --json
atomui city tests check --json
```

规则：

- `docs check` 检查文档先行、模块文档、链接、测试矩阵。
- `tests check` 检查功能点单元测试、释放断言和替代测试说明。
- 缺少文档或测试不自动修复，只输出诊断和建议动作。

详细规则见：[文档和测试门禁命令设计](docs-and-tests-gates.md)。

### 8. Templates 和 Build 集成

生成命令调用 Templates：

```bash
atomui city generate page Orders/List --route /orders
```

构建命令调用 Build：

```bash
atomui city build --json
```

规则：

- CLI 不重新定义模板输出结构。
- CLI 不重新定义 Build target。
- CLI 只传递参数、收集结果、输出诊断。

### 9. Plugin 命令

插件命令处理本地插件管理，但不能绕过 PluginSystem 规则。

示例：

```bash
atomui city plugin install SalesPlugin.1.0.0.nupkg --dry-run
atomui city plugin inspect com.company.sales --json
```

规则：

- install/update 支持 plan/apply。
- 不能覆盖运行中插件目录。
- `UnloadPending` 时输出 pending 操作和原因。
- CLI 不直接执行插件业务代码。

详细规则见：[插件命令设计](plugin-commands.md)。

### 10. 诊断

CLI 错误输出必须稳定。

建议前缀：

| 前缀 | 来源 |
|---|---|
| `AUCCLI` | CLI 自身错误。 |
| `AUCBLD` | Build 透传错误。 |
| `AUCTPL` | Templates 透传错误。 |
| `AUCPLG` | PluginSystem 透传错误。 |
| `AUCGEN` | Source Generator 透传错误。 |
| `AUCANL` | Analyzer 透传错误。 |

详细规则见：[诊断设计](diagnostics.md)。

### 11. 测试矩阵

| 功能点 | 测试类型 | 测试工具 | 必测场景 |
|---|---|---|---|
| command parsing | Unit | CliCommandTestHost | 参数、默认值、非法参数。 |
| JSON output | Unit | CliOutputAssertions | schema、diagnostics、exit code。 |
| plan/apply | Unit/Integration | CliPlanTestHost | dry-run、plan 文件、apply、rollback。 |
| generate commands | CLI/Template smoke | TemplateSmokeTestHost | module、page、plugin、test。 |
| build commands | CLI/Build integration | BuildTestHost | build、pack、publish 透传 diagnostics。 |
| plugin commands | CLI/Plugin integration | PluginTestHost | install、update、disable、UnloadPending。 |
| docs check | Unit | DocsGateAssertions | 缺文档、缺测试矩阵。 |
| tests check | Unit | TestsGateAssertions | 缺单测、缺释放断言。 |
| non-interactive | Unit | CliCommandTestHost | 不 prompt、exit code 稳定。 |

完整测试规则见：[诊断和测试设计](diagnostics-and-testing.md)。

### 12. 完成标准

CLI 任一功能点完成必须满足：

- 命令文档已确认。
- 参数和 JSON schema 已定义。
- exit code 已定义。
- 测试矩阵已有条目。
- 单元测试或 CLI command test 已存在。
- 非交互行为已测试。
- JSON 输出已测试。
