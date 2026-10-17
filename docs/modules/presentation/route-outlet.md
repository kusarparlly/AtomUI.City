# AtomUI.City.Presentation Route Outlet

## 定位

RouteOutlet 是 Routing-Presentation adapter 与 Avalonia physical target 之间的单次事务提交边界。Router snapshot 在调用前已提交；Outlet 只返回 Presentation 结果，不更新 Router。

## Admission 和所有权

- `CommitAsync` 可从任意线程调用。
- plan 单次使用；调用开始时 ownership 从 adapter 转给 Outlet。
- 同一 Outlet 只有一个 in-flight，默认最多 32 pending，严格 FIFO。
- pending 满载拒绝最新 plan，返回 `OutletQueueFull` 并释放其全部候选资源。
- `CommitAsync(plan, cancellationToken)` 只有一个取消入口。WindowSession 创建的 Outlet 自动把调用方 token 与 Window Scope token 合成为有效令牌；独立 Outlet 使用调用方 token。
- admission 后取消会终止尚未 final commit 的事务；Outlet 先恢复 previous View、释放 candidate 和 linked CTS，再向调用方抛出取消。普通 `cancellationToken` 不具有 wait-only 语义。

## Replace 事务

```text
validate outlet/effective cancellation
-> old Entry leave guard
-> subscribe visual receipt + temporary SetContent on UI
-> candidate ActivateAsync outside UI
-> transfer candidate to Entry + publish current (final commit)
-> dispose previous Entry
```

Clear 在 UI dispatcher 清空 content 并原子清除 current Entry；Window close 可使用已经完成 guard 的内部 approved clear。

## 回滚与状态

final commit 前普通失败：UI dispatcher 恢复 previous View，释放 candidate，状态 OutOfSync。final commit 前取消：执行同样回滚，成功后恢复为 Empty/Committed 并报告取消。恢复或候选释放失败：状态 Faulted，记录 `AUCPRS036`。final commit 后 previous cleanup 失败：记录 `AUCPRS037`，保持新 Entry 和成功结果。

failure presenter 只展示结构化故障，不决定导航；presenter 失败记录 `AUCPRS038` 并使 Outlet Faulted。OutOfSync 可由调用方依据当前 Router snapshot 显式重试；Faulted 不再接受 commit。

## Stop

Dispose 先发布唯一 terminal task 并拒绝新 admission，control item 排在已接受工作之后；排队但尚未开始的事务在看到 stopping 后释放候选并失败。最终清空 physical target、释放 current Entry，进入 Stopped；清理失败进入 Faulted。

## 测试

覆盖 primary/named target、FIFO、queue full、plan reuse、caller/Window Scope cancellation、取消后回滚完成性、temporary attach、activation failure、rollback failure、same-handle no-op、post-commit cleanup、presenter failure、concurrent dispose、snapshot 和诊断字段。
