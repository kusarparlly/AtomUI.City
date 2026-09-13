# AtomUI.City.Cli Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-CLI-001 | Command Model | Completed | CliApplication, CliCommandLine, CliExitCodes | CliCommandArchitectureTests |
| AUC-CLI-002 | New App Command | Completed | CliApplication, ApplicationTemplateRenderer, CliEnvelope | CliNewAppTests |
| AUC-CLI-003 | Build and Test Commands | Completed | DotnetInvocation, ProcessRunner, CliEnvelope | CliBuildAndTestCommandTests |
| AUC-CLI-004 | Plugin Inspect and Doctor | Completed | CliApplication, PluginManifestReader, PluginDiagnostic | CliInspectDoctorPluginTests |
| AUC-CLI-005 | AI-Friendly Envelope | Completed | CliEnvelope, CliDiagnostic, CliExecutionEnvironment | CliCommandArchitectureTests |
| AUC-CLI-006 | Non-Interactive and CI Mode | Completed | CliExecutionEnvironment, CliExitCodes | CliCommandArchitectureTests |
| AUC-CLI-007 | Generation Commands | Completed | CliApplication -> GenerationTemplateRenderer | CliGenerationCommandTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 命令入口固定为 `atomui city`。 | 必须有实现、测试或工程门禁证据。 |
| 非交互和 CI 模式不得等待输入。 | 必须有实现、测试或工程门禁证据。 |
| JSON envelope 不能混入普通日志。 | 必须有实现、测试或工程门禁证据。 |
| exit code、diagnostic code、artifact schema 必须稳定。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-CLI-001 Command Model

Feature ID: `AUC-CLI-001`
Status: Completed
Goal: 定义 `atomui city` 命令解析、全局选项、exit code 和 usage 行为。
Public Contract: CliApplication, CliCommandLine, CliExitCodes
Runtime / Build Behavior: 解析 command、option、argument、`--json`、`--ci` 和工作目录；未知命令和缺少子命令输出 usage diagnostic。
Failure Behavior: 未知命令、缺参、未知 option、value option 缺值返回 ArgumentError，不执行 handler。
Threading / Cancellation: 解析为纯 CPU；RunAsync 必须观察 CancellationToken。
Diagnostics: diagnostic 包含 code、message、target、position；envelope 包含 command 和 exit code。
Tests: `CliCommandArchitectureTests`
Required Assertions: 断言入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-002 New App Command

Feature ID: `AUC-CLI-002`
Status: Completed
Goal: 通过 CLI 调用 Templates 生成可构建应用。
Public Contract: CliApplication, ApplicationTemplateRenderer, CliEnvelope
Runtime / Build Behavior: `atomui city new app` 校验 app name、root namespace、AOT/dynamic plugin 冲突和目标文件冲突；先生成 plan/artifacts，再按需 render。`--dry-run` 只输出 plan/artifacts，不写文件。
Failure Behavior: 缺少 app name、非法 app name、保留 root namespace、AOT/dynamic plugin 冲突、目标文件冲突和取消都返回失败 envelope，且不得覆盖或写入目标文件。
Threading / Cancellation: CLI 在渲染前观察 token，renderer 在每个文件写入前观察 token；取消后输出 plan/artifacts 和 `AUCCLI0106`。
Diagnostics: `AUCCLI0101` 到 `AUCCLI0106` 覆盖缺参、保留 namespace、冲突变量、非法 app name、目标冲突和取消。
Tests: `CliNewAppTests`
Required Assertions: 断言生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-003 Build and Test Commands

Feature ID: `AUC-CLI-003`
Status: Completed
Goal: 通过 CLI 统一调用 `dotnet build/test` 和工程门禁。
Public Contract: DotnetInvocation, ProcessRunner, CliEnvelope
Runtime / Build Behavior: 命令构造 dotnet invocation，记录 working directory、CI mode、stdout/stderr summary、exit code 和 duration；dry-run 只输出 invocation。
Failure Behavior: dotnet 非零退出返回原始 exit code 和 `AUCCLI0201`；取消返回 `AUCCLI0202`；工作目录不存在返回 `AUCCLI0203` 且不启动进程。
Threading / Cancellation: 取消会停止等待并尝试终止子进程；stdout/stderr 捕获和 envelope 输出都有上限。
Diagnostics: diagnostic 包含 invocation working directory、exit code 和 stderr 摘要；CI mode 通过 invocation 进入 process environment。
Tests: `CliBuildAndTestCommandTests`
Required Assertions: 断言成功、失败、非零 exit code、取消、CI 模式和输出截断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-004 Plugin Inspect and Doctor

Feature ID: `AUC-CLI-004`
Status: Completed
Goal: 提供插件包检查、manifest 验证和安装前诊断。
Public Contract: CliApplication, PluginManifestReader, PluginDiagnostic
Runtime / Build Behavior: `plugin inspect` 读取 package root 或 manifest path 并验证 manifest metadata；`plugin doctor` 验证 package layout、main assembly 和 required contribution。两者都不加载插件 assembly。
Failure Behavior: 缺少 path 返回 `AUCCLI0302`；manifest 缺失、版本非法和 layout 错误映射 PluginSystem `AUCPLG...` diagnostics，并保留 `data.pluginDiagnostics`。
Threading / Cancellation: 文件读取前观察 token；命令只读，不执行插件业务代码。
Diagnostics: diagnostic 包含 plugin id、package path、manifest field 或 layout path；PluginSystem diagnostic code 保持原样。
Tests: `CliInspectDoctorPluginTests`
Required Assertions: 断言合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-005 AI-Friendly Envelope

Feature ID: `AUC-CLI-005`
Status: Completed
Goal: 让 AI agent 能稳定读取命令结果和下一步建议。
Public Contract: CliEnvelope, CliDiagnostic, CliExecutionEnvironment
Runtime / Build Behavior: envelope 包含 schemaVersion、command、status、success、exitCode、diagnostics、data、artifacts、suggestedCommands、changedFiles、retryable、suggestedActions 和 documentationLinks。
Failure Behavior: `--json` 只输出 JSON；参数错误 retryable=false，运行时失败 retryable=true；diagnostic 自动生成 explain suggested command。
Threading / Cancellation: 写入 JSON 前观察 token；artifact 和 changed file 从 data 中稳定提升。
Diagnostics: diagnostic 包含 code、message、target、position；suggested command 使用 `atomui city explain <code> --json`。
Tests: `CliCommandArchitectureTests`
Required Assertions: 断言 schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-006 Non-Interactive and CI Mode

Feature ID: `AUC-CLI-006`
Status: Completed
Goal: 保证 CI 和 agent 环境不会被交互提示阻塞。
Public Contract: CliExecutionEnvironment, CliExitCodes
Runtime / Build Behavior: 检测 CI、stdin availability、`ATOMUI_CITY_NON_INTERACTIVE` 和 `--non-interactive`；`--ci` 或 CI 环境会启用非交互模式并传递给 dotnet invocation。
Failure Behavior: 非交互模式下需要确认的命令缺少 `--yes` 直接返回 `AUCCLI0401`，不能等待 stdin。
Threading / Cancellation: RunAsync 必须观察 token；子进程继承 CI 环境。
Diagnostics: diagnostic 包含 missing confirmation、required option 和 environment mode。
Tests: `CliCommandArchitectureTests`
Required Assertions: 断言 CI、non-interactive、stdin unavailable、需要确认时失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CLI-007 Generation Commands

Feature ID: `AUC-CLI-007`
Status: Completed
Goal: 调用 Templates 生成 module、page、test、configuration 和 localization 产物；plugin generation 不属于本阶段。
Public Contract: CliApplication -> GenerationTemplateRenderer、TemplatePlan、CliEnvelope。
Runtime / Build Behavior: CLI 解析目标工程和 namespace，Templates 负责内容、plan、事务写入与回滚；dry-run 不写文件。
Failure Behavior: 缺少 kind/name、plugin/未知 kind、工程无法唯一解析、模板变量非法、目标冲突、取消和写入失败均返回稳定失败 envelope，不保留半成品。
Threading / Cancellation: cancellation 贯穿 CLI 和 Templates；同一 output root 的 apply 串行。
Diagnostics: `AUCCLI0501` 到 `AUCCLI0504`，Templates 诊断保持 `AUCTPL...` code。
Tests: `CliGenerationCommandTests` 和 `GenerationTemplateRendererTests`。
Required Assertions: 真实变更、dry-run、冲突、取消、回滚和生成产物可构建。
Acceptance Criteria: 五类非插件命令具备真实 plan、render、diagnostic、rollback 和 build/test smoke 闭环。
