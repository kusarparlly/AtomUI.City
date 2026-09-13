# AtomUI.City.Cli API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Command Entry | Program, CliApplication, CliCommandLine | `atomui city` 命令入口和解析。 | 未知命令、非法参数稳定失败。 |
| Envelope | CliEnvelope, CliDiagnostic, CliExitCodes | 机器可读输出和诊断。 | `--json` 只能输出 JSON envelope。 |
| Process Invocation | DotnetInvocation, ProcessRunner | 调用 dotnet 子进程。 | 保留 exit code 和 stdout/stderr 摘要。 |
| Environment | CliExecutionEnvironment | CI、非交互、stdin availability 和工作目录。 | 非交互不等待输入；CI 自动启用非交互。 |
| Generation | CliApplication, GenerationTemplateRenderer, TemplatePlan | 解析目标工程并调用 Templates 生成非插件产物。 | dry-run 不写文件；冲突、取消和失败不保留半成品。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| CliApplication.RunAsync | 执行命令。 | argv、environment、stdout/stderr。 | exit code。 | 解析失败、handler 失败映射 CliExitCodes；缺少 `city`、缺少子命令、未知 command、未知 option 和 option 缺值都返回 ArgumentError。 | 必须观察 token。 | 单进程单次调用；handler 内部并发隔离。 |
| CliCommandLine.Parse | 解析命令行。 | argv 不得为 null。 | CliCommandLine 和 parse diagnostics。 | 未知 option、缺参返回 parse diagnostic，不执行 handler。 | 纯 CPU，无 token。 | 无共享状态。 |
| `atomui city new app` handler | 生成应用模板。 | AppName、namespace、target framework、output、dry-run、AOT/plugin flags。 | exit code 和 CliEnvelope。 | `AUCCLI0101` 到 `AUCCLI0106` 覆盖缺参、保留 namespace、冲突变量、非法 app name、目标冲突和取消；目标冲突不得覆盖已有文件。 | 渲染前和每个文件写入前观察 token；取消输出失败 envelope。 | 先 plan 后 render；dry-run 可重复调用且不写文件。 |
| CliEnvelope JSON/Text 输出 | 输出机器可读 envelope 或文本摘要。 | envelope、TextWriter。 | 写入完成。 | JSON 模式只写 JSON envelope；文本失败输出稳定 usage；AI 字段缺失属于 contract break。 | 写入前观察 token。 | JSON 输出不得混入普通日志；artifacts 和 changedFiles 从 data 稳定提升。 |
| ProcessRunner.RunAsync | 运行 dotnet 或其他子进程。 | DotnetInvocation。 | process result。 | 非零 exit code 保留为 command failure；工作目录不存在由 handler 在启动前拒绝。 | 取消必须终止子进程或停止等待。 | stdout/stderr 捕获和 envelope 摘要必须有上限。 |
| DotnetInvocation | 表达 `dotnet build/test/pack/publish` 调用。 | command、project、configuration、framework、working directory、CI mode。 | executable、arguments、workingDirectory、ciMode。 | 参数缺失由命令解析层拒绝；未知 option 不生成 invocation。 | 纯数据，无 token。 | arguments 不可被外部 mutation 改写。 |
| `atomui city plugin inspect/doctor` handler | 读取插件 manifest 和校验 package layout。 | package root 或 `atomui-city/plugin.json` path。 | CliEnvelope，manifest，pluginDiagnostics。 | 缺 path 返回 `AUCCLI0302`；PluginSystem `AUCPLG...` diagnostics 原样映射到 CLI diagnostics。 | 文件读取前观察 token。 | 只读；不得加载插件 assembly。 |
| Non-interactive confirmation | 防止 CI/agent 被 prompt 阻塞。 | CliExecutionEnvironment、`--ci`、`--non-interactive`、`--yes`。 | CliEnvelope。 | 需要确认且缺少 `--yes` 返回 `AUCCLI0401`，不读取 stdin。 | 纯 CPU，无 token。 | 同一 argv 结果稳定。 |
| `atomui city generate` handler | 生成 module/page/test/config/localization。 | kind、name、project、namespace、output、route/culture/dependency/reload options。 | CliEnvelope 中包含 plan 和 artifacts。 | `AUCCLI0501~0504` 或原样 `AUCTPL...`；`generate plugin` 明确失败。 | render 前后及每个文件写入前后观察 token。 | dry-run 幂等；同 output root 的 apply 串行。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `CliApplication` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliDiagnostic` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliEnvelope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExecutionEnvironment` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExitCodes` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DotnetInvocation` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
