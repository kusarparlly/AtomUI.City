# AtomUI.City.Presentation Lifecycle

## 状态机

Presentation 使用五套状态机：Runtime、WindowSession、RouteOutlet、candidate ownership 和 bounded serial lane。完整转换见 [industrial-design.md](industrial-design.md#5-五套状态机)。旧的 Presentation Localization 状态机已在 1.0 冻结前移除，因为 culture 与文案不属于本模块。

非法转换不能被静默接受：普通操作失败进入 `OutOfSync`，恢复或不变量失败进入 `Faulted`，终态拒绝 mutation。

## 启动

```text
Host Build/Start
-> Avalonia framework initialization completed
-> IPresentationRuntime.Attach
-> RegisterWindow on UI thread before Show
-> Attached Property or explicit RegisterOutlet
-> Ready
```

`PresentationModule` 和 `AddPresentation` 注册同一服务。Attach 不增加服务、不运行 IO、不扫描程序集。

## Outlet Entry

```text
adapter owns plan
-> Outlet admission owns plan
-> prepare/guard
-> temporary physical attach
-> activate
-> final commit transfers ownership to Entry
-> old Entry cleanup
```

最终提交前错误走 rollback；rollback 必须恢复旧 content 并释放候选。最终提交后只完成旧资源清理，不回滚。

## Window 关闭

用户或普通应用关闭：若更早注册的 Avalonia `Closing` 处理器已取消本次原生关闭，Session 保持 `Ready` 且不启动关闭事务；否则进入 `Ready -> Closing -> Ready(rejected)` 或 `Ready -> Closing -> Closed`。Host/Dispose/OS 关闭不可拒绝。并发调用共享一个 transaction task；Window 消失后仍可继续清理，task 在清理完成后结束。

## Host Stop

Runtime 先进入 Stopping 并拒绝新 Window；已经接受的 Outlet/Interaction 及释放路径仍可使用 dispatcher。所有 WindowSession 都要尝试关闭，单个失败不阻断其他窗口；最后停止 PresentationScope。失败聚合后 Runtime 进入 Faulted，否则进入 Stopped。

## 逻辑 Root/Child（Implemented / Release Validation Pending）

`AUC-PRESENTATION-017` 已提供显式 `ViewModelCompositionOwner` 和异步 composition lease。Window/Runtime owner 在关闭时先拒绝新的 Root/Child attach，再按 leaf-to-root 结束 composition tree。每个 child 先关闭 Scope 准入、取消并 drain 任务、停用、释放自己的 lease；siblings 的失败互相隔离，最终聚合到 owner。RouteOutlet Entry 继续走独立关闭事务，不被隐式挂入 composition tree。该能力尚未完成全部并发、失败注入和发布门禁，完整合同及剩余验证项见 [viewmodel-composition.md](viewmodel-composition.md)。

## 插件撤销

先阻止新 contribution，再关闭 active view，随后撤销 interaction、view descriptor、resource dictionary 和 resource lease。单项资源失败被收集，不跳过其余资源；仍有 active view 时拒绝插件卸载。
