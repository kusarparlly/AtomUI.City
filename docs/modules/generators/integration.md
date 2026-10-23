# AtomUI.City.Generators Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Generators | 本模块不进入 Host 运行时容器。Host 只消费 generator 生成的 manifest、registrar 或编译期诊断结果。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Generators.Tests |
| PluginSystem | AtomUI.City.Generators | 本模块通过 manifest、包布局、模板、CLI 或 generator 支持插件开发和检查；不直接持有运行时插件对象。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Generators.Tests |
| EventBus | AtomUI.City.Generators | EventContract/EventChannel/EventHandler attributes -> generated service registrar 与 event manifest。 | EventBus declarations -> compile-time generator -> runtime DI/registry | compilation 生命周期；运行时由 owner Module/Host 管理 | 编译期无后台任务；运行时遵循 EventBus 合同 | 非法声明输出 `AUCGEN005` 并阻断不完整输出；不回退到程序集扫描。 | EventBusGeneratorTests |
| Data | AtomUI.City.Generators | DataClient/DataOperation attributes -> generated data client registrar 与 descriptor。 | Data declarations -> compile-time generator -> runtime catalog | compilation 生命周期；运行时由 Data catalog/Host 管理 | 编译期无后台任务；运行时遵循 Data 合同 | 非法声明输出 `AUCGEN005` 并阻断 Data registrar；不回退到程序集扫描。 | AtomUICityIncrementalGeneratorDataTests |
| Mvvm | AtomUI.City.Generators | `ValidatingViewModelBase` + DataAnnotations -> generated property validation。 | Mvvm declarations -> compile-time generator -> runtime validation hook | compilation 生命周期；运行时由 ViewModel/ValidationScope 管理 | 同步规则在 property mutation 线程执行；异步规则遵循 Mvvm token 合同 | 无法静态解析输出 `AUCGEN009` 并阻断该类型输出；不回退运行时反射。 | GeneratedValidationTests; MvvmNativeAotProcessTests |
| Testing | AtomUI.City.Generators | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Generators.Tests |

## 集成硬约束

- Generator 不引用 AtomUI.City 运行时包。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
