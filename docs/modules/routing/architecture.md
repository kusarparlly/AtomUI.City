# AtomUI.City.Routing Architecture

## 所有权地图

| 对象 | 生命周期 | 所有者 |
| --- | --- | --- |
| `RouteDescriptor` | 不可变 | generated manifest / application |
| `RouteGraphSnapshot` | 不可变、可被旧事务继续持有 | `RouteRegistry` 发布 |
| `RouteContributionLease` | contribution 生命周期 | 模块或插件激活流程 |
| `NavigationScope` | 窗口、Outlet 或应用定义的导航会话 | DI scope / application |
| `NavigationSnapshot` | 每次成功提交替换 | `NavigationScope` |
| `NavigationResult` | 单次调用 | 调用方 |
| `NavigationCommitReceipt` | 单次成功提交的只读恢复凭证 | issuing `NavigationScope`；调用方只持有，不构造 |
| `NavigationRestoreResult` | 单次条件恢复调用 | 调用方 |

`NavigationScope` 不是“一次导航事务”。它是长期导航会话；每个 `Navigate*`、`Back`、`Forward` 调用才是一笔串行事务。

## 运行流程

```text
capture RouteGraphSnapshot
-> match route / bind parameters
-> match policies
-> route middleware enter (root -> leaf)
-> leave guards (leaf -> root)
-> enter guards (root -> leaf)
-> resolvers (root -> leaf, declaration order)
-> prepare NavigationSnapshot and success result
-> route middleware exit (leaf -> root)
-> atomically publish NavigationSnapshot + journal entry + commit generation
-> return NavigationResult with NavigationCommitReceipt
```

任何非成功结果都不执行 commit。每层 Middleware 的 `next` 都有独立调用窗口，成功提交要求整条链与 terminal 均满足一次性调用合同；已经启动但未 await 的 `next` 仍属于当前事务，gate 会等待它结束；该层 middleware 返回后再调用捕获的 `next` 会失败。`next` 返回后 middleware 抛异常、返回非成功/外部 operation 结果或重复调用 `next`，prepared state 都会被丢弃。Resolver data 与 route、parameters、graph version 在同一个 `NavigationSnapshot` 中发布。

## 并发模型

- 每个 `NavigationScope` 使用一个异步 gate 串行事务。
- `CancelPrevious` 在锁外取消旧事务，并用世代号保证最新请求胜出。
- `Queue` 异步等待 gate 并串行执行；1.0 不承诺线程调度公平性或严格 FIFO。
- `RejectIfBusy` 不等待，返回 `CITY-NAVIGATION-BUSY`。
- 同一笔仍活跃的异步调用链重入同一 scope 返回 `CITY-NAVIGATION-REENTRANT`；继承 ExecutionContext 的后台任务在原事务结束后不再被误判为重入。
- 多线程读取 `CurrentSnapshot` 使用原子引用发布。
- `TryRestoreAsync` 使用同一异步 gate 串行化；只有 receipt 对应的 generation 和 snapshot 仍为 current 才恢复，并以新 generation 使 receipt 单次有效。
- `DisposeAsync` 返回同一完成事务，并等待运行中的外部代码退出。

## Commit 边界

Routing commit 只包含 active route descriptor、只读 parameters、graph version、reuse key、resolver data 和 journal 更新。

`NavigationCommitReceipt` 是对该 commit 的条件恢复能力，不是第二套导航或长期 undo token。恢复原子替换为提交前 snapshot、route graph 与 back/current/forward checkpoint；不重跑 match、guard、resolver 或 middleware。任何后续 commit/restore 已推进 generation 时，旧 receipt 稳定拒绝，不覆盖较新的用户导航。

ViewModel activation、ActivationScope、DI child scope、Outlet 控件和 VisualTree commit 不在 Routing 事务内，由 Presentation/MVVM 在消费 target 后处理。Routing 只依据 `OutletName` 选择 route descriptor。

## AOT

Route Map 由 `AtomUI.City.Generators` 在编译期转换为 `GeneratedRoutingRouteManifest` 和 partial route methods。运行时只消费确定类型和 descriptor，不依赖程序集扫描、`Activator.CreateInstance` 或命名约定。
