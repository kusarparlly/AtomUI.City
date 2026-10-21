# AtomUI.City.Routing Diagnostics

## AUCRT 诊断码

| Code | 语义 | 关键字段 |
| --- | --- | --- |
| `AUCRT001` | navigation started | operationId, target, targetKind, graphVersion |
| `AUCRT002` | navigation success/redirect completed | operationId, target, graphVersion, elapsedMilliseconds |
| `AUCRT003` | navigation non-success completed | operationId, target, graphVersion, errorCode, elapsedMilliseconds |
| `AUCRT004` | contribution graph published | contributionId, operation, graphVersion, routeCount |
| `AUCRT005` | resolver returned Failed | operationId, routeId, resolverType, errorCode |
| `AUCRT006` | pipeline component threw | operationId, routeId, componentType, stage, errorCode |
| `AUCRT007` | guard returned Reject or match policy returned false | operationId, routeId, componentType, stage, errorCode |
| `AUCRT008` | contribution graph rejected | contributionId, operation, graphVersion, graphError |
| `AUCRT009` | conditional restore succeeded | navigationId, generation, currentRouteId |
| `AUCRT010` | conditional restore rejected | navigationId, generation, errorCode, currentRouteId |
| `AUCRT011` | conditional restore failed or cancelled | navigationId, generation, errorCode, exceptionType |

诊断码只表达观测事件；导航业务错误仍由 `NavigationResult.Error.Code` 承载。

## 可靠性合同

- 诊断写入不发生在 Registry 或 lifecycle monitor lock 内；导航事件仍位于 transaction gate 生命周期中，以保持同一事务的事件顺序。
- `IHostDiagnostics.Write` 抛异常或已经 Complete 时，不改变 graph/navigation 结果。
- 取消不是 pipeline component failure。
- AUCRT001/002/003 的 graphVersion 是该导航捕获的版本；事务期间 Registry 发布新图不会改写完成事件版本。
- Behavior 的 DI/service-resolver 解析失败归属于其真实 stage，不归因给外层 middleware。
- restore diagnostics 与 graph/navigation 一样是观测性 side effect；sink 抛异常不能改变 restore result 或 generation。
- message 可优化；code 语义和字段名属于兼容合同。
