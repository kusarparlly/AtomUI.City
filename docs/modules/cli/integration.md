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
| Templates | AtomUI.City.Cli | CLI 只解析命令和目标工程；文件内容、plan、冲突预检、事务写入与回滚由 Templates 提供。 | Templates -> CLI | 单次 generation operation | 同 root 串行，不同 root 可并发 | Templates diagnostics 原样进入 CLI envelope。 | CliGenerationCommandTests, GenerationTemplateRendererTests |
| PluginSystem | AtomUI.City.Cli | 本模块通过 manifest、包布局、模板、CLI 或 generator 支持插件开发和检查；不直接持有运行时插件对象。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Cli.Tests |
| Testing | AtomUI.City.Cli | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Cli.Tests |

## 集成硬约束

- 命令入口固定为 atomui city。
- 非交互和 CI 模式不得等待输入。
- JSON envelope 不能混入普通日志。
- exit code 稳定。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
