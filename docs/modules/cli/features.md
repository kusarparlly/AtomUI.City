# AtomUI.City.Cli Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public / Internal Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-CLI-001 | Command Model | Completed | Public: CliApplication, CliExitCodes；Internal: CliCommandLine | CliCommandArchitectureTests |
| AUC-CLI-002 | New App Command | Completed | Public: CliApplication, ApplicationTemplateRenderer；Internal: CliEnvelope | CliNewAppTests |
| AUC-CLI-003 | Build and Test Commands | Completed | Public: CliApplication；Internal: DotnetInvocation, ProcessRunner, CliEnvelope | CliBuildAndTestCommandTests |
| AUC-CLI-004 | Plugin Inspect and Doctor | Completed | CliApplication, PluginManifestReader, PluginDiagnostic | CliInspectDoctorPluginTests |
| AUC-CLI-005 | AI-Friendly Envelope | Completed | Public: CliExecutionEnvironment；Internal CLR: CliEnvelope, CliDiagnostic；Public process contract: JSON envelope | CliCommandArchitectureTests |
| AUC-CLI-006 | Non-Interactive and CI Mode | Completed | CliExecutionEnvironment, CliExitCodes | CliCommandArchitectureTests |
| AUC-CLI-007 | Generation Commands | Completed | CliApplication -> GenerationTemplateRenderer | CliGenerationCommandTests |
| AUC-CLI-008 | Repository Governance Gates | Completed | CLI commands: `docs check`, `tests check` | CliGovernanceGateTests |
| AUC-CLI-009 | Plan and Apply Transaction | Completed | CLI commands: `plan generate`, `apply`；versioned plan schema | CliPlanApplyTests |
| AUC-CLI-010 | Diagnostic Explain Catalog | Completed | CLI command: `explain`；versioned diagnostic catalog | CliExplainTests |
| AUC-CLI-011 | .NET Tool Packaging and Distribution | Completed | NuGet tool package `AtomUI.City.Cli`；command `atomui` | CliToolPackageTests / package-consumer gate |

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
Public Contract: CliApplication, CliExitCodes
Internal Contract: CliCommandLine
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
Public Contract: CliApplication, ApplicationTemplateRenderer
Internal Contract: CliEnvelope；其 JSON 输出是 public process contract，但 CLR 类型不公开。
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
Public Contract: CliApplication 的 build/test 命令和进程 JSON/exit-code contract
Internal Contract: DotnetInvocation, ProcessRunner, CliEnvelope
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
Public Contract: CliExecutionEnvironment，以及 `atomui city --json` 的进程输出 schema
Internal Contract: CliEnvelope, CliDiagnostic
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
Public Contract: CliApplication -> GenerationTemplateRenderer、TemplatePlan。
Internal Contract: CliEnvelope；其 artifacts/changedFiles JSON 字段仍属于 public process contract。
Runtime / Build Behavior: CLI 解析目标工程和 namespace，Templates 负责内容、plan、事务写入与回滚；dry-run 不写文件。
Failure Behavior: 缺少 kind/name、plugin/未知 kind、工程无法唯一解析、模板变量非法、目标冲突、取消和写入失败均返回稳定失败 envelope，不保留半成品。
Threading / Cancellation: cancellation 贯穿 CLI 和 Templates；同一 output root 的 apply 串行。
Diagnostics: `AUCCLI0501` 到 `AUCCLI0504`，Templates 诊断保持 `AUCTPL...` code。
Tests: `CliGenerationCommandTests` 和 `GenerationTemplateRendererTests`。
Required Assertions: 真实变更、dry-run、冲突、取消、回滚和生成产物可构建。
Acceptance Criteria: 五类非插件命令具备真实 plan、render、diagnostic、rollback 和 build/test smoke 闭环。

## AUC-CLI-008 Repository Governance Gates

Feature ID: `AUC-CLI-008`
Status: Completed；versioned evaluator、稳定诊断、真实 framework workspace 正/负向检查与只读/取消测试已落地。
Goal: 对 AtomUI.City 框架仓库执行确定性、只读的文档结构与 Feature 测试证据检查；发现错误时返回稳定诊断和非零 exit code。
Scope: 第一版只支持 AtomUI.City framework profile。普通 City 应用、第三方 library 和其他仓库不因引用 City 而自动受此门禁约束；未来 profile 必须另行设计和分配 Feature。
Command Contract: `atomui city docs check`、`atomui city tests check`、`--json`、`--ci`、`--non-interactive`。
Internal Contract: versioned framework rule catalog、workspace discovery、documentation gate evaluator、test evidence gate evaluator；模板生成不属于本 Feature。
Runtime / Build Behavior: `docs check` 验证 framework profile 要求的文档、相对链接、Feature ID、测试矩阵映射和禁用占位符；`tests check` 验证测试目录/项目、Feature 引用的测试类型、合法 Test Type 和 Generator/Analyzer/Build 专项测试证据。两条命令只读取工作区，不执行测试、不修改文件。
Failure Behavior: 工作区无法识别、必要文件缺失、断链、Feature ID 非法或重复、Feature 未进入测试矩阵、测试项目/类型不存在、Test Type 非法或专项证据缺失均输出独立诊断；存在任一 Error 时返回 `CliExitCodes.Failure`，参数错误返回 `CliExitCodes.ArgumentError`，不得返回 success envelope。
Threading / Cancellation: 文件枚举与读取前后观察 cancellation；取消停止新增扫描且不产生文件变更。第一版不启动子进程，不加载目标程序集，不执行测试代码。
Diagnostics: `AUCCLI0601` 至 `AUCCLI0616`；不得复用已经表示 dotnet 子进程失败的 `AUCCLI0201`。
Tests: `CliGovernanceGateTests`
Required Assertions: 断言合法仓库、无法识别的工作区、缺失文档、断链、重复 Feature ID、缺测试矩阵、缺测试项目/类型、非法 Test Type、专项证据缺失、取消、JSON diagnostics、非零 exit code 和全程只读。
Non-goals: 不生成文档；不自动修复；不执行 `dotnet test`；不判断架构内容质量；不通过字符串搜索声称已经证明 Dispose/Cancellation/异常断言；不决定集成测试能否替代单元测试；不提供 application/library/minimal profile；不验证 PluginSystem 运行时行为。
Acceptance Criteria: 第一版所有规则都能由确定输入得到稳定结果，生成与检查范围不存在隐式扩张，失败不会返回成功 envelope，并由单元测试证明没有写入工作区。

## AUC-CLI-009 Plan and Apply Transaction

Feature ID: `AUC-CLI-009`
Status: Completed；五类 Templates preview、Plan 1.0、create-only transaction、lock/staging/journal、重放与 rollback 已落地。
Goal: 把已支持的非插件代码生成拆成可审查的 plan 与受控 apply 两步，保证实际写入与经确认的计划一致，并在失败或取消后恢复稳定工作区。
Scope: 第一版只支持 `generate module|page|test|config|localization`，且 plan 中只允许 UTF-8 文本文件的 `create` 变更。`new app`、插件命令、build/test/pack/publish、update/delete/move、二进制文件、任意 shell 命令和第三方 plan producer 均不在范围内。
Command Contract: `atomui city plan generate <kind> <name> [generate options] [--json]`；`atomui city apply <plan-file> [--yes] [--json]`。plan 文件必须位于当前 workspace 的 `.city/plans`，apply 不接受 stdin、URL 或 workspace 外路径。
Internal Contract: versioned plan reader/writer、isolated template preview owner、workspace/path validator、SHA-256 integrity validator、exclusive apply lock、transaction staging/journal 和 generation operation adapter；这些实现类型不得扩大 Templates 或 CLI 的 CLR public surface。
Runtime / Build Behavior: plan 在进程专属临时目录调用与 `AUC-CLI-007` 相同的 Templates Renderer，读取成功产物和 `TemplatePlan` 后计算完整内容、规范化相对路径、内容哈希和前置条件；临时目录完全清理成功后才原子写入 `.city/plans/<operationId>.json`，全程不写业务文件。apply 把 plan 当作不可信输入，先完成全量 schema、workspace、路径、重复目标、哈希和目标不存在检查，再在 `.city/transactions/<operationId>` staging 后提交。完成 journal 与 plan digest 匹配时重复 apply 返回成功 no-op；其他既存目标一律冲突失败。
Failure Behavior: 缺少或不支持的 operation、plan 写入失败、plan 缺失/过大/格式非法、schema 不支持、workspace 不匹配、路径逃逸或符号链接逃逸、内容哈希不一致、目标冲突、锁冲突、提交失败、回滚失败和取消均返回独立 `AUCCLI07xx` 诊断。任何 preflight 失败都不得写业务文件；提交失败必须尝试回滚，回滚失败必须同时保留原始异常与 rollback diagnostics，禁止返回 success。
Threading / Cancellation: 同一 workspace 同时只允许一个 apply 事务，锁冲突快速失败。plan 在每次模板计算和 IO 前后观察 token。apply 在 preflight/staging 期间响应取消；进入 commit 后延迟取消，必须先完成 commit 或 rollback，恢复稳定状态后再返回取消结果。
Diagnostics: `AUCCLI0701` 至 `AUCCLI0715`；diagnostic 必须包含适用的 `operationId`、workspace-relative `path`、`planFile`、`expectedHash`、`actualHash`、`temporaryPath` 或失败阶段。
Tests: `CliPlanApplyTests`
Required Assertions: 五种合法 generation plan、隔离 preview 与成功/失败/取消清理、非范围 operation、plan 不写业务文件、严格 schema、大小上限、路径/链接逃逸、重复和大小写冲突、workspace mismatch、内容篡改、目标竞争、跨进程锁、成功 apply、重复 apply、取消、提交失败、完整回滚、回滚失败聚合、JSON envelope 和全程无任意命令执行。
Non-goals: 第一版不提供通用自动化语言，不执行 plan 中携带的命令，不修改或删除现有业务文件，不接受外部 producer，不替代 `generate --dry-run`，也不为 PluginSystem 预先实现安装事务。
Acceptance Criteria: plan 足以确定性重放五类生成结果；apply 只能执行合同内 create 操作；成功、失败、取消和重试都具有明确工作区终态，并由真实临时工作区集成测试证明没有静默部分提交。

## AUC-CLI-010 Diagnostic Explain Catalog

Feature ID: `AUC-CLI-010`
Status: Completed；immutable catalog、canonical lookup、未知 code failure 和并发离线查询已落地。
Goal: 将 CLI 实际产生或原样透传的稳定 diagnostic code 转换为可操作、可机器解析的说明，而不是依赖 message 文本猜测故障含义。
Scope: 第一版目录只收录当前 CLI 命令能够产生的 `AUCCLI...`，以及 CLI 明确原样透传的 `AUCTPL...`、`AUCPLG...` code。City 其他未经过 CLI 暴露的模块诊断、运行时日志搜索、异常堆栈分析和在线文档抓取不在范围内。
Command Contract: `atomui city explain <diagnostic-code> [--json]`；code 去除首尾空白后按 ordinal ignore-case 查询，输出始终返回目录中的 canonical uppercase code。
Internal Contract: immutable、versioned diagnostic explanation catalog；每个条目唯一包含 code、severity、summary、cause、suggestedActions、documentationLinks、relatedCommands 和 ownerModule。目录编译进 CLI，不在运行时解析 Markdown、扫描程序集或访问网络。
Runtime / Build Behavior: 已知 code 返回结构化说明；缺少 code 返回 ArgumentError；未知 code 返回失败而不是伪造通用解释。`explain` 是纯只读查询，不启动 Host、子进程、模板或插件代码。
Failure Behavior: 缺少 code、未知 code 和目录自身重复/非法条目分别使用 `AUCCLI0801`、`AUCCLI0802`、`AUCCLI0803`。目录完整性错误是构建/测试阻断项；若仍在运行时发现，命令返回 Failure，禁止选择任意重复条目。
Threading / Cancellation: 目录初始化后不可变并可并发读取；输出前观察 cancellation。没有后台任务、锁、文件写入或 Dispose 所有权。
Diagnostics: `AUCCLI0801` 至 `AUCCLI0803`。解释 `AUCCLI0802` 本身必须由静态 fallback 条目提供，避免未知 code 路径递归查询失败。
Tests: `CliExplainTests`
Required Assertions: 每个 CLI 可产生/透传 code 恰有一个目录条目；大小写规范化；字段完整且链接为仓库相对或受信 HTTPS；缺参、未知 code、重复 code、非法 severity、JSON/Text 输出、取消、并发读取以及不会读取网络/Markdown/程序集。
Non-goals: 不根据自然语言推测诊断，不执行修复建议，不保证解释未被 CLI 暴露的全框架诊断，也不把可变 message 文本提升为兼容键。
Acceptance Criteria: 对目录内任一 code 返回稳定且可操作的说明；目录覆盖率和唯一性由 contract test 自动验证；未知输入不能返回 success envelope。

## AUC-CLI-011 .NET Tool Packaging and Distribution

Feature ID: `AUC-CLI-011`
Status: Completed；项目已声明 `PackAsTool`/`ToolCommandName=atomui`，并通过隔离本地 feed、cache 与 tool-path 的真实安装和进程验收。
Goal: 把文档中的 `atomui city ...` 变成可从 NuGet 安装、可固定版本、可在开发机和 CI 中真实调用的 .NET Tool，而不是仅能通过源码或 `AtomUI.City.Cli.exe` 启动。
Distribution Contract: package id 固定为 `AtomUI.City.Cli`，tool command 固定为 `atomui`，入口仍要求第一个业务参数为 `city`。项目必须设置 `PackAsTool=true`、`ToolCommandName=atomui` 并使用统一 `AtomUICityVersion`；第一版 tool target framework 为 `net10.0`。
Installation Contract: 全局安装使用 `dotnet tool install --global AtomUI.City.Cli --version <version>` 后直接执行 `atomui city ...`；仓库和 CI 推荐使用 local tool manifest 固定精确版本，并通过 `dotnet tool run atomui city ...` 调用。普通应用的 `PackageReference` 不安装 shell command，也不得隐式修改用户 PATH 或全局 tool 状态。
Package Boundary: CLI 是独立工程工具，不进入应用 runtime dependency graph。tool package 只携带运行所需资产和依赖，不包含测试/fixture/source；未被 CLI 使用的 ProjectReference 必须删除，Build/buildTransitive 包不得作为 CLI runtime 依赖。CLI tool 的运行时版本要求与用户应用目标框架相互独立，net8.0 应用可以由安装了 .NET 10 runtime 的开发机使用该 tool。
Failure Behavior: 安装、更新、卸载、feed、签名或 runtime 缺失发生在 CLI 进程启动前，由 `dotnet tool`/NuGet 以非零 exit code 报告，不伪造 `AUCCLI` 诊断。进程成功启动后继续遵守现有 CLI envelope 和 exit-code 合同。
Threading / Cancellation: 包安装生命周期归 `dotnet tool` 所有；CLI 不创建自更新后台任务，不在运行时改写自己的安装目录，也不自动更新全局工具。
Diagnostics: 不新增运行时 diagnostic code；打包、安装和命令 shim 缺陷由 Pack/PackageConsumer 门禁阻止发布。
Tests: `CliToolPackageTests` 与 `engineering/check-cli-tool-package-consumer.ps1`。
Required Assertions: Release pack 产生 DotnetTool package；metadata、版本、`DotnetToolSettings.xml` 和 command name 正确；从隔离本地 feed、独立 package cache 和临时 `--tool-path` 安装；不使用 ProjectReference；执行真实 `atomui city doctor --json`；参数和 exit code 贯穿 shim；卸载/删除 tool-path 后无全局残留；Windows/Linux 为 PR 门禁，macOS 为 release 门禁。
Non-goals: 不把 CLI 合并到任一 runtime package，不因应用引用 City 自动安装，不实现自更新器，不发布 RID-specific/self-contained CLI，也不承诺没有 .NET 10 runtime 的机器可以运行第一版工具。
Acceptance Criteria: 候选 `.nupkg` 能在干净临时环境中安装为命令 `atomui`，通过真实进程完成至少一个成功 JSON 命令和一个稳定失败命令；包内容、依赖来源和清理均可审计。
