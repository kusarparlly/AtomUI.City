# AtomUI.City.Cli API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块当前全部 public 类型、成员、命令名、参数、exit code 和 JSON envelope 均为 `Preview`。`PublicAPI.Unshipped.txt` 只冻结待审签名，不表示 `Stable`；只有逐项完成语义 review 并在本文改标的 API 才能进入稳定承诺。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Command Entry | public CliApplication/CliExitCodes；internal Program/CliCommandLine | `atomui city` 命令入口和解析。 | 未知命令、非法参数稳定失败。 |
| Envelope | internal CliEnvelope/CliDiagnostic；public process JSON schema | 机器可读输出和诊断。 | `--json` 只能输出 JSON envelope；CLR 实现类型不对开发者公开。 |
| Process Invocation | internal DotnetInvocation/ProcessRunner | 调用 dotnet 子进程。 | 保留 exit code 和 stdout/stderr 摘要；不形成可引用 CLR API。 |
| Environment | CliExecutionEnvironment | CI、非交互、stdin availability 和工作目录。 | 非交互不等待输入；CI 自动启用非交互。 |
| Generation | CliApplication, GenerationTemplateRenderer, TemplatePlan | 解析目标工程并调用 Templates 生成非插件产物。 | dry-run 不写文件；冲突、取消和失败不保留半成品。 |
| Repository Governance Gates | CLI command surface；内部 rule catalog/evaluator | 对 AtomUI.City framework profile 执行只读文档和测试证据检查。 | 有 Error 返回 Failure；不生成、修复或执行测试。 |
| Plan / Apply Transaction | CLI command surface；内部 plan codec/validator/transaction | 将五类非插件 generation 变更持久化为可审查计划，并在受控事务中创建文件。 | plan 不写业务文件；apply 不信任 plan；任何失败不得静默留下部分提交。 |
| Diagnostic Explain Catalog | CLI command surface；内部 immutable explanation catalog | 按稳定 diagnostic code 返回原因和修复信息。 | 未知 code 失败；不从 message、Markdown、程序集扫描或网络推断。 |
| .NET Tool Distribution | NuGet package `AtomUI.City.Cli`；tool command `atomui` | 把 CLI 进程入口作为独立 .NET Tool 分发。 | `PackAsTool=true`、`ToolCommandName=atomui`、版本统一；应用 PackageReference 不安装该工具。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| CliApplication.RunAsync | 执行命令。 | argv、environment、stdout/stderr。 | exit code。 | 解析失败、handler 失败映射 CliExitCodes；缺少 `city`、缺少子命令、未知 command、未知 option 和 option 缺值都返回 ArgumentError。 | 必须观察 token。 | 单进程单次调用；handler 内部并发隔离。 |
| internal CliCommandLine.Parse | 解析命令行。 | argv 不得为 null。 | internal CliCommandLine 和 parse diagnostics。 | 未知 option、缺参返回 parse diagnostic，不执行 handler。 | 纯 CPU，无 token。 | 无共享状态。 |
| `atomui city new app` handler | 生成应用模板。 | AppName、namespace、target framework、output、dry-run、AOT/plugin flags。 | exit code 和 CliEnvelope。 | `AUCCLI0101` 到 `AUCCLI0106` 覆盖缺参、保留 namespace、冲突变量、非法 app name、目标冲突和取消；目标冲突不得覆盖已有文件。 | 渲染前和每个文件写入前观察 token；取消输出失败 envelope。 | 先 plan 后 render；dry-run 可重复调用且不写文件。 |
| internal CliEnvelope JSON/Text 输出 | 输出机器可读 envelope 或文本摘要。 | envelope、TextWriter。 | 写入完成。 | JSON 模式只写 JSON envelope；文本失败输出稳定 usage；AI 字段缺失属于 process contract break。 | 写入前观察 token。 | JSON 输出不得混入普通日志；artifacts 和 changedFiles 从 data 稳定提升。 |
| internal ProcessRunner.RunAsync | 运行 dotnet 或其他子进程。 | internal DotnetInvocation。 | process result。 | 非零 exit code 保留为 command failure；工作目录不存在由 handler 在启动前拒绝。 | 取消必须终止子进程或停止等待。 | stdout/stderr 捕获和 envelope 摘要必须有上限。 |
| internal DotnetInvocation | 表达 `dotnet build/test/pack/publish` 调用。 | command、project、configuration、framework、working directory、CI mode。 | executable、arguments、workingDirectory、ciMode。 | 参数缺失由命令解析层拒绝；未知 option 不生成 invocation。 | 纯数据，无 token。 | arguments 不可被外部 mutation 改写。 |
| `atomui city plugin inspect/doctor` handler | 读取插件 manifest 和校验 package layout。 | package root 或 `atomui-city/plugin.json` path。 | CliEnvelope，manifest，pluginDiagnostics。 | 缺 path 返回 `AUCCLI0302`；PluginSystem `AUCPLG...` diagnostics 原样映射到 CLI diagnostics。 | 文件读取前观察 token。 | 只读；不得加载插件 assembly。 |
| Non-interactive confirmation | 防止 CI/agent 被 prompt 阻塞。 | CliExecutionEnvironment、`--ci`、`--non-interactive`、`--yes`。 | CliEnvelope。 | 需要确认且缺少 `--yes` 返回 `AUCCLI0401`，不读取 stdin。 | 纯 CPU，无 token。 | 同一 argv 结果稳定。 |
| `atomui city generate` handler | 生成 module/page/test/config/localization。 | kind、name、project、namespace、output、route/culture/dependency/reload options。 | CliEnvelope 中包含 plan 和 artifacts。 | `AUCCLI0501~0504` 或原样 `AUCTPL...`；`generate plugin` 明确失败。 | render 前后及每个文件写入前后观察 token。 | dry-run 幂等；同 output root 的 apply 串行。 |
| `atomui city docs check` handler | 验证 AtomUI.City framework profile 的文档结构与确定性文档规则。 | 工作目录、输出模式、cancellation；第一版不接受 application/library profile。 | CliEnvelope；`data` 包含 gate、ruleSetId、ruleSetVersion、root、workingDirectory、checkedFiles、errorCount、warningCount。 | 工作区无法识别、必要文档缺失、断链、Feature ID/测试矩阵错误或禁用占位符产生 `AUCCLI06xx`；任一 Error 返回 Failure。 | 每次目录枚举和文件读取前后观察 token。 | 只读、无共享状态；相同工作区快照产生相同结果。 |
| `atomui city tests check` handler | 验证 Feature 到测试项目、测试类型和 Test Type 的静态证据。 | 工作目录、输出模式、cancellation。 | CliEnvelope；输出字段与 docs check 一致。 | 测试根/项目、引用测试类型、合法 Test Type 或专项测试证据缺失产生 `AUCCLI06xx`；任一 Error 返回 Failure。 | 每次目录枚举和文件读取前后观察 token。 | 只读；不启动 `dotnet test`、不加载测试程序集、不执行测试代码。 |
| `atomui city plan generate` handler | 为既有五类非插件 generation 生成可持久化计划。 | kind、name、与 `generate` 相同的业务 options、working directory、输出模式、cancellation。 | CliEnvelope；`data.planFile`、`data.plan`、顶层 `artifacts`；隔离 Templates preview 清理成功后原子写入 `.city/plans/<operationId>.json`。 | 缺参/未知 operation 为 ArgumentError；模板规划、preview 清理、路径验证、哈希或 plan IO 失败返回 `AUCCLI07xx`/原始 `AUCTPL...`，不得写业务文件或持久化不完整 plan。 | template preview、读取/清理临时目录、目录创建、序列化和原子替换前后观察 token。 | 每次调用拥有独立 preview root；相同 workspace 快照和规范化输入产生相同 plan digest；允许复用字节完全相同的既有 plan，禁止覆盖内容不同的同名文件。 |
| `atomui city apply` handler | 校验并执行第一版 create-only plan。 | `.city/plans` 内 plan file、`--yes`、工作目录、输出模式、cancellation。 | CliEnvelope；`data.operationId`、`data.planFile`、`data.planDigest`、`data.transactionState`，顶层 `changedFiles`。 | 缺失/非法/过大 plan、未知 schema、workspace/path/hash 冲突、锁冲突、提交/回滚失败或取消返回 `AUCCLI07xx`；失败不得返回 success。 | preflight/staging 可取消；commit/rollback 临界区延迟取消直至工作区稳定。 | workspace 级独占事务；相同 completed journal + plan digest 重放为成功 no-op，其他既存目标视为冲突。 |
| `atomui city explain` handler | 从版本化目录读取某个 diagnostic code 的操作说明。 | 单个 diagnostic code、输出模式、cancellation。 | CliEnvelope；`data` 含 canonical code、severity、summary、cause、suggestedActions、documentationLinks、relatedCommands、ownerModule。 | 缺参返回 `AUCCLI0801`/ArgumentError；未知 code 返回 `AUCCLI0802`/ArgumentError；目录非法返回 `AUCCLI0803`/Failure。 | 查询为同步只读；写输出前观察 token。 | immutable catalog 可并发读；ordinal-ignore-case 输入映射到唯一 canonical code，结果无调用顺序依赖。 |
| `AtomUI.City.Cli` tool package | 提供命令 shim `atomui` 并把参数原样交给 Program。 | NuGet source、精确 package version、global/local/tool-path install scope。 | 安装后 `atomui city ...` 或 `dotnet tool run atomui city ...`；应用包引用不产生 command。 | NuGet/feed/runtime/install 失败由 `dotnet tool` 返回非零 exit code；进程未启动时没有 CLI envelope。 | 安装取消由 `dotnet tool` 管理；CLI 启动后使用命令 token。 | 同一 scope/version 重复安装遵守 dotnet tool 行为；CLI 不自更新、不修改其他 scope。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `CliApplication` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExecutionEnvironment` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExitCodes` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

`CliCommandLine`、`CliEnvelope`、`CliDiagnostic`、`DotnetInvocation` 和 `ProcessRunner` 是 CLI 进程内部的 CLR 实现类型，不属于可引用的 .NET API。它们产生或执行的命令语法、JSON schema、diagnostic 字段和 invocation 字段仍是进程边界上的兼容契约，必须继续遵守本文档与 `compatibility.md` 的变更规则；不得为了让文档看似一致而扩大这些类型的 CLR visibility。

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
