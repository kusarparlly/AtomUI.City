# AtomUI.City.Cli Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Cli | CLI 默认不启动桌面 Host；只创建工程、调用构建、检查插件和输出诊断。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Cli.Tests |
| Templates | AtomUI.City.Cli | Templates 计算五类 generation 内容和普通 generate 的写入事务；CLI 的 `AUC-CLI-009` adapter 在进程专属临时 root 调用现有 Renderer、读取成功产物并清理 preview，再编码受限 Plan 1.0，独立负责持久化 plan、workspace 锁、apply journal 和 create-only 重放。CLI 不复制模板内容规则，也不为此扩大 Templates public API。 | Templates -> CLI | 单次 generation/plan/apply operation | 同 workspace apply 串行，不同 workspace 可并发 | Templates diagnostics 原样进入 CLI envelope；preview 清理、CLI plan/schema/事务失败使用 `AUCCLI07xx`。 | CliGenerationCommandTests, GenerationTemplateRendererTests, CliPlanApplyTests |
| Diagnostic catalog | AtomUI.City.Cli | CLI 为自身产生及明确透传的 `AUCCLI`/`AUCTPL`/`AUCPLG` code 编译静态 explanation entry；不通过运行时扫描反向依赖其他模块。 | Module-owned diagnostic metadata -> CLI catalog | 进程级 immutable snapshot | 无锁并发读 | 缺失/重复/非法 entry 阻断 contract test；运行时返回 `AUCCLI0803`。 | CliExplainTests |
| PluginSystem | AtomUI.City.Cli | 本模块通过 manifest、包布局、模板、CLI 或 generator 支持插件开发和检查；不直接持有运行时插件对象。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Cli.Tests |
| Testing | AtomUI.City.Cli | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Cli.Tests |

## 集成硬约束

- 命令入口固定为 atomui city。
- 非交互和 CI 模式不得等待输入。
- JSON envelope 不能混入普通日志。
- exit code 稳定。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
