# AtomUI.City.Templates Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Templates | Templates 生成 Host 应用骨架，但不参与 Host 运行时。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.TemplateSmokeTests |
| PluginSystem | AtomUI.City.Templates | 本模块通过 manifest、包布局、模板、CLI 或 generator 支持插件开发和检查；不直接持有运行时插件对象。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.TemplateSmokeTests |
| CLI / dotnet new | AtomUI.City.Templates | CLI 调用 public renderer；NuGet Template Engine 使用包内静态模板。二者共同变量的默认语义必须一致，但输出不要求逐字节相同。 | Tooling -> Templates | 单次 plan/render/instantiate | 同目标 renderer 调用串行；dotnet new 由工具进程管理 | 任一入口失败不得报告伪成功或覆盖已有文件。 | CliNewAppTests; DotnetNewTemplateIntegrationTests |
| Presentation / Avalonia | AUC-TEMPLATES-010 | 生成真实 `Application`、classic desktop lifetime、DI 主窗口和 Presentation bootstrap；Templates 不进入运行时依赖图。 | Templates generated output -> Presentation | Host start -> runtime attach/window register -> UI loop -> Host stop/dispose | UI 对象只在 Avalonia UI 线程创建和注册；Host 清理不依赖已停止的 UI context。 | 非 desktop lifetime、缺失 Host、重复 attach 或窗口注册失败时进程非零退出且不得保留半初始化 UI。 | Completed |
| Testing | AtomUI.City.Templates | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.TemplateSmokeTests |

## 集成硬约束

- 生成项目必须 restore、build 和 test。
- 模板变量必须校验。
- 输出不得包含机器绝对路径。
- dry-run 不写文件。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
