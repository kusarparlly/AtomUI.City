# AtomUI.City.Mvvm Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Mvvm | AtomUI.City.Mvvm 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Mvvm.Tests |
| PluginSystem | AtomUI.City.Mvvm | 插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Mvvm.Tests |
| Testing | AtomUI.City.Mvvm | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Mvvm.Tests |
| State | AtomUI.City.Mvvm | ViewModel 经 ActivationScope 绑定 State Reaction 与订阅；ambient StateScope 经 IStateScopeAccessor 串联。 | State -> Mvvm | ActivationScope 释放时级联释放 | Immediate 默认；Queued/Dispatcher 可选 | State 错误走 State 错误策略，不杀死 ViewModel。 | tests/AtomUI.City.Mvvm.Tests |
| EventBus | AtomUI.City.Mvvm | ViewModel 订阅必须绑定 ActivationScope。 | EventBus -> Mvvm | ActivationScope 释放时退订 | 订阅自带调度策略 | 订阅异常走 EventBus 错误策略。 | tests/AtomUI.City.Mvvm.Tests |
| Routing | AtomUI.City.Mvvm | Route 离开前调 DeactivationGuard（ICanDeactivate→IConfirmDeactivate）；激活失败中止导航并释放候选 scope。 | Routing -> Mvvm | 候选 scope commit 后 ViewModel 转 Active | 导航事务串行 | 拒绝中止导航；取消返回 Cancel。 | tests/AtomUI.City.Mvvm.Tests |
| Presentation | AtomUI.City.Mvvm | 创建/解析 ViewModel、注册 Interaction handler（RegisterHandler）、绑定验证状态，并通过 `IUiCommandFactory` 镜像 domain-first Command 创建入口。 | Presentation -> Mvvm | ActivationScope 生命周期；CommandExecutionDomain 由应用/ViewModel owner 持有 | UI marshal 由 Presentation 承担；domain 不改变业务执行线程 | handler 缺失返回 NotHandled；绑定失败不进入 Active；domain closing 准入返回 Rejected。 | tests/AtomUI.City.Mvvm.Tests; tests/AtomUI.City.Presentation.Tests |

## 集成硬约束

- MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。
- Interaction 只表达请求，UI 展示由 Presentation handler 完成。
- Command 状态支持完成、取消、异常和并发拒绝结果。
- ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
