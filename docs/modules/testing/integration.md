# AtomUI.City.Testing Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Testing | Testing 可以创建可控 Host，但生产 Host 不得依赖 Testing 包。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Testing.Tests |
| Data | AtomUI.City.Testing | Data 提供运行时接口、context 和 result；Testing 提供 Fake/Recording/Scripted 测试实现。生产 Data 不引用 Testing。 | Data contracts -> Testing implementations -> test project | 每个测试创建、执行、断言后丢弃；连接由测试显式 Start/Stop | 并发队列保护脚本与记录；连接生命周期由单个测试所有者驱动 | 空脚本返回稳定失败；取消和委托异常不被吞掉。 | DataTestDoublesTests |
| PluginSystem | AtomUI.City.Testing | 插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Testing.Tests |
| Testing | AtomUI.City.Testing | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Testing.Tests |

## 集成硬约束

- 生产项目不得引用 Testing。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
