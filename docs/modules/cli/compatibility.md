# AtomUI.City.Cli Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 命令入口固定为 atomui city。
- 非交互和 CI 模式不得等待输入。
- JSON envelope 不能混入普通日志。
- exit code 稳定。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AUCCLI0001` 表示缺少 `city` 根命令，`AUCCLI0002` 表示未知命令，`AUCCLI0003` 表示缺少子命令，`AUCCLI0004` 表示未知 option，`AUCCLI0005` 表示 value option 缺值；diagnostic `target` 和 `position` 字段进入 1.0 兼容承诺。
- `AUCCLI0101` 表示缺少 app name，`AUCCLI0102` 表示 root namespace 使用框架保留前缀，`AUCCLI0103` 表示 AOT 与 dynamic plugin 默认冲突，`AUCCLI0104` 表示 app name 不是合法 identifier，`AUCCLI0105` 表示目标文件已存在，`AUCCLI0106` 表示 new app generation 取消。
- `atomui city new app --json` 成功、dry-run、目标冲突和取消 envelope 的 `data.plan`、`data.artifacts` 字段进入 1.0 兼容承诺；目标冲突还包含 `data.conflict`。
- `AUCCLI0201` 表示 dotnet 子进程非零退出，`AUCCLI0202` 表示 dotnet 子进程取消，`AUCCLI0203` 表示工作目录不存在。
- `atomui city build/test/pack/publish --json` 的 `data.invocation`、`data.exitCode`、`data.stdout`、`data.stderr`、`data.durationMs`、`workingDirectory` 和 `ciMode` 字段进入进程边界兼容承诺；internal `DotnetInvocation`、`ProcessRunner` 本身不形成可引用 CLR API。
- `AUCCLI0301` 表示 plugin install 缺少 package path，`AUCCLI0302` 表示 plugin inspect/doctor 缺少 package path。
- `atomui city plugin inspect/doctor --json` 的 `data.packageRoot`、`data.manifestPath`、`data.manifest`、`data.succeeded` 和 `data.pluginDiagnostics` 字段进入 1.0 兼容承诺；PluginSystem `AUCPLG...` diagnostic code 必须原样进入 CLI diagnostics。
- CLI JSON envelope 顶层 `status`、`artifacts`、`suggestedCommands`、`changedFiles` 和 `retryable` 字段进入 1.0 兼容承诺；`suggestedActions` 保留为兼容 alias。
- `retryable=false` 用于成功和参数错误，`retryable=true` 用于运行时失败；每个 diagnostic code 必须生成 `atomui city explain <code> --json` suggested command。
- `AUCCLI0401` 表示非交互或 CI 模式下命令需要显式 `--yes` 确认。
- `AUCCLI0501` 表示 generate 缺少 kind/name，`AUCCLI0502` 表示未知或延期的 generation kind，`AUCCLI0503` 表示目标工程无法唯一解析，`AUCCLI0504` 表示生成事务取消。
- `atomui city generate <kind> <name> --json` 的 `data.plan`、`data.artifacts`、顶层 `changedFiles` 进入 1.0 兼容承诺；`generate plugin` 在当前范围稳定返回 `AUCCLI0502`。
- `AUC-CLI-008` 已完成；rule set `atomui-city-framework/1.0`、`AUCCLI0601~0616` 和 governance gate `data` 字段属于已交付 process contract。不得复用 code 或改变已确认语义；让相同输入由通过变成失败、改变强制扫描范围或解析语法必须提升 rule-set major version。
- `AUC-CLI-009` 已完成；`plan generate`/`apply`、Plan 1.0 schema、`AUCCLI0701~0715`、`.city/plans`/`.city/transactions` 布局和对应 envelope 字段属于已交付 process contract。
- Plan 1.0 reader 必须严格拒绝未知字段和未知 operation/change kind。增加 optional/required 字段、允许新的写操作或改变 hash/path/rollback 语义都必须提升 plan schema version；第一版 producer 不得以 minor 代码更新悄悄生成 update/delete/move。
- `operationId`、plan digest 和 completed journal 共同决定幂等身份；改变 canonicalization 会改变身份，属于 plan schema breaking change。Plan 1.0 明确不可跨 workspace/移动后重放，不承诺第三方 producer 兼容。
- `AUC-CLI-010` 已完成；`explain` data 字段和 `AUCCLI0801~0803` 属于已交付合同。diagnostic code 与 `ownerModule` 是稳定 identity；summary/cause 文案可澄清，但不得改变 severity、故障含义或把未知 code 从失败改为成功。
- explain catalog 的新增条目是 additive；删除 CLI 仍可能产生/透传的条目、复用 code 或改变 related command 的安全语义属于 breaking change。目录覆盖范围的扩展不意味着 CLI 可以执行对应模块代码。
- `AUC-CLI-011` 已完成；package id `AtomUI.City.Cli`、tool command `atomui`、根参数 `city`、第一版 tool TFM `net10.0` 和独立安装模型属于已交付分发合同，并由真实 package-consumer gate 验证。
- tool command、package id 或根参数变更属于 CLI breaking change。提高最低 tool runtime、改变 local/global 安装语义或把 CLI 引入应用 runtime graph 必须提供迁移说明；普通应用 `PackageReference` 永远不获得隐式全局安装副作用。
- CLI 不改写 Templates 诊断码；`AUCTPL2001~2005` 与 `AUCTPL1004~1006` 原样输出。
- `CliExecutionEnvironment` 的 `isCi`、`isNonInteractive` 和 `isStdinAvailable` 语义进入 1.0 兼容承诺；`--ci` 必须启用非交互并传递给 dotnet invocation。
- `--json` 模式失败时只输出 JSON envelope；非 JSON 解析失败输出 `Usage:` 文本块。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
