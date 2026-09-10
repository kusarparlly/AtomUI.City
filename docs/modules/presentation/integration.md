# AtomUI.City.Presentation Integration

## 集成矩阵

| 上游/下游 | 输入或输出 | 所有权 | 线程 | 失败边界 |
| --- | --- | --- | --- | --- |
| Core | Module registration、Host lifecycle、LifecycleScope、diagnostics | Host/Runtime | lifecycle work 不在 lock 内 | stop 聚合失败 |
| Routing | 已提交 snapshot、target、operation/route/reuse identity | Router 是真相 owner；adapter owns candidate before admission | adapter/Outlet；Avalonia 部分进 UI | Presentation 不回滚 Router |
| MVVM | activation、deactivation、Interaction、Validation snapshot | Entry/ActivationScope | guard/activation 非 UI；visual handler UI | 普通失败局限 Operation/Outlet |
| State | ViewModel 暴露的属性 | State/ViewModel | 由业务选择 dispatcher | 无自动 State-to-Control 通道 |
| Localization | 业务代码产生的最终文案/资源 | Localization/application | 由业务和 Avalonia binding 决定 | Presentation 无 culture 状态 |
| Security | 业务 ViewModel 的授权结果 | Security/application | 非 UI 计算 | Presentation 不做授权裁决 |
| PluginSystem | owner-bound UI contribution/revoke request | plugin/contribution lease | UI resource revoke 进 dispatcher | active view 可阻断 unload |
| Avalonia | Window、Control、VisualTree、dispatcher 和真实事件 | WindowSession/Entry | 只在 UI dispatcher | platform failure 分层升级 |

## Routing-Presentation adapter

adapter 属于应用组合层，不属于 Router 或 Presentation 的隐藏中央对象。它负责按 Router target 调用 `IViewModelFactory`、`IViewLocator`、`ViewFactory`、`ViewBinder` 并提交 plan。plan 传给 `CommitAsync` 后所有权立即转移，adapter 不再访问或释放候选。

## 禁止耦合

- Core、Routing、State、Localization 不得引用 Avalonia。
- Presentation 不得引用 AtomUI 或 Localization，不得解释 Security/State 业务语义。
- Presentation 不得用 EventBus 模拟内部强一致事务提交。
- 应用不得直接改动 managed Outlet 的 physical content 绕过 RouteOutlet。
