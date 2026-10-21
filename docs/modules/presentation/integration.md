# AtomUI.City.Presentation Integration

## 集成矩阵

| 上游/下游 | 输入或输出 | 所有权 | 线程 | 失败边界 |
| --- | --- | --- | --- | --- |
| Core | Module registration、Host lifecycle、LifecycleScope、diagnostics | Host/Runtime | lifecycle work 不在 lock 内 | stop 聚合失败 |
| Routing | 已提交 snapshot、commit receipt、target、operation/route/reuse identity | Router 是真相 owner；应用 plan factory owns candidate before Outlet admission | coordinator/Outlet；Avalonia 部分进 UI | Outlet 不回滚 Router；协调器只做 receipt-guarded conditional restore |
| MVVM | activation、deactivation、Interaction、Validation snapshot、CommandFactory | Entry/ActivationScope；UI command factory 只拥有创建策略 | guard/activation/command business 非 UI；命令可观察通知和 visual handler UI | 普通失败局限 Operation/Outlet；通知失败只诊断 |
| State | ViewModel 暴露的属性 | State/ViewModel | 由业务选择 dispatcher | 无自动 State-to-Control 通道 |
| Localization | 业务代码产生的最终文案/资源 | Localization/application | 由业务和 Avalonia binding 决定 | Presentation 无 culture 状态 |
| Security | 业务 ViewModel 的授权结果 | Security/application | 非 UI 计算 | Presentation 不做授权裁决 |
| PluginSystem | owner-bound UI contribution/revoke request | plugin/contribution lease | UI resource revoke 进 dispatcher | active view 可阻断 unload |
| Avalonia | Window、Control、VisualTree、dispatcher 和真实事件 | WindowSession/Entry | 只在 UI dispatcher | platform failure 分层升级 |

## Routing-Presentation 协调

`IRoutePresentationCoordinator` 属于 Presentation 技术协调层，不是隐藏业务 Router。调用方传入具体 Router、Outlet、navigation delegate 和 `IRoutePresentationPlanFactory`；协调器按 Outlet 实例串行执行 Router commit、plan 创建和 Outlet commit。Router 未提交时不创建 plan；Presentation 失败时使用本次 commit receipt 请求条件恢复；若更新导航已推进 generation，恢复稳定拒绝并返回 `RestoreRejectedByNewerNavigation`，不得覆盖新状态。

应用组合层继续负责按 Router target 调用 `IViewModelFactory`、`IViewLocator`、`ViewFactory`、`ViewBinder` 构造 plan，并决定业务错误等级、重试与用户反馈。plan 成功返回给协调器后仍由应用 factory 临时拥有；传给 `CommitAsync` 后所有权立即转移到 Outlet，factory/协调器不得再访问或释放候选。factory 在返回前失败时必须自行清理部分资源。

## 禁止耦合

- Core、Routing、State、Localization 不得引用 Avalonia。
- Presentation 不得引用 AtomUI 或 Localization，不得解释 Security/State 业务语义。
- Presentation 不得用 EventBus 模拟内部强一致事务提交。
- 应用不得直接改动 managed Outlet 的 physical content 绕过 RouteOutlet。
- 协调器不得回放用户输入、重建 VisualTree 或恢复 ActivationScope；这些都不是 Router receipt 的职责。

## MVVM Command 组合

CLI、Headless 和低级测试直接使用 `CommandFactory`。Avalonia ViewModel 默认注入 `IUiCommandFactory`；该 Presentation 入口固定使用当前 DI 中的 `IUiDispatcher` 和 `IHostDiagnostics`，不要求业务代码逐命令传递基础设施依赖。工厂同时镜像 `CommandExecutionDomain` 入口，但只转发创建参数，不拥有命令执行、domain、取消、activation 或异常处理状态机。
