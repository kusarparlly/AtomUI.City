# AtomUI.City.Routing Lifecycle

## 状态模型

Route Graph：`Candidate -> Validated -> Published -> Superseded`。候选验证失败时旧 graph 保持 Published。

单次导航成功路径：`Created -> Waiting -> Matching -> MiddlewareEnter/Guarding -> Resolving -> Prepared -> MiddlewareExit -> Committing -> Success|Redirected`。任一阶段可在 commit 前结束为 Rejected/Cancelled/Failed/NotFound。

条件恢复路径：`ReceiptIssued -> Waiting -> ValidateOwnerAndGeneration -> Restored|Rejected|Cancelled|Failed`。`Restored` 会发布新的 generation，因此同一 receipt 不能再次成功。

这些是行为阶段，不是当前 public enum。`NavigationResultStatus` 是调用完成状态。

## NavigationScope

- 创建时捕获初始 graph version 并发布 empty snapshot。
- 每次事务开始时从 `IRouteGraphProvider` 捕获最新 snapshot。
- 事务完成前不切换 graph。
- `Dispose` 标记停止并发起取消；不等待。
- `DisposeAsync` 标记停止、取消等待和运行中的事务，并等待用户 Guard/Resolver/Middleware 退出。
- 重复 `Dispose`/`DisposeAsync` 幂等；Dispose 后新导航返回 `CITY-NAVIGATION-SCOPE-DISPOSED`。
- receipt 不拥有 scope 生命周期；scope disposed、foreign scope、reentrant chain 或 receipt 已被后续 commit/restore supersede 时，条件恢复不会修改 snapshot/journal。

Routing 不创建 provisional RouteScope 或 ActivationScope。相关生命周期属于 Presentation/MVVM。

## Contribution

- `AddContribution` 先构建和验证候选 graph，再原子发布。
- `RouteContributionLease.Dispose` 撤销 contribution；失败时 lease 可重试。
- 撤销失败不替换 graph，也不丢失 service resolver。
- 成功撤销同时删除 Registry 对 contribution service resolver 的引用。
- 正在执行的事务继续使用其已捕获旧 snapshot；snapshot 本身不租赁 contribution service resolver，因此 PluginSystem 或直接 Registry 调用方必须在 revoke 和释放插件 Provider/ALC 前完成 drain。
