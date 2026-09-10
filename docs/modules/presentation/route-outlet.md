# AtomUI.City.Presentation Route Outlet

## 定位

RouteOutlet 是 Routing-Presentation adapter 与 Avalonia physical target 之间的单次事务提交边界。Router snapshot 在调用前已提交；Outlet 只返回 Presentation 结果，不更新 Router。

## Admission 和所有权

- `CommitAsync` 可从任意线程调用。
- plan 单次使用；调用开始时 ownership 从 adapter 转给 Outlet。
- 同一 Outlet 只有一个 in-flight，默认最多 32 pending，严格 FIFO。
- pending 满载拒绝最新 plan，返回 `OutletQueueFull` 并释放其全部候选资源。
- caller token 在 admission 后只取消等待；lifecycle token 可以在 final commit 前终止事务。

## Replace 事务

```text
validate outlet/lifecycle
-> old Entry leave guard
-> subscribe visual receipt + temporary SetContent on UI
-> candidate ActivateAsync outside UI
-> transfer candidate to Entry + publish current (final commit)
-> dispose previous Entry
```

Clear 在 UI dispatcher 清空 content 并原子清除 current Entry；Window close 可使用已经完成 guard 的内部 approved clear。

## 回滚与状态

final commit 前失败：UI dispatcher 恢复 previous View，释放 candidate，状态 OutOfSync。恢复或候选释放失败：状态 Faulted，记录 `AUCPRS036`。final commit 后 previous cleanup 失败：记录 `AUCPRS037`，保持新 Entry 和成功结果。

failure presenter 只展示结构化故障，不决定导航；presenter 失败记录 `AUCPRS038` 并使 Outlet Faulted。OutOfSync 可由调用方依据当前 Router snapshot 显式重试；Faulted 不再接受 commit。

## Stop

Dispose 先发布唯一 terminal task 并拒绝新 admission，control item 排在已接受工作之后；排队但尚未开始的事务在看到 stopping 后释放候选并失败。最终清空 physical target、释放 current Entry，进入 Stopped；清理失败进入 Faulted。

## 测试

覆盖 primary/named target、FIFO、queue full、plan reuse、caller/lifecycle cancellation、temporary attach、activation failure、rollback failure、same-handle no-op、post-commit cleanup、presenter failure、concurrent dispose、snapshot 和诊断字段。
