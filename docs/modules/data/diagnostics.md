# AtomUI.City.Data Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCDATA001` | RequestRetry | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA002` | ConnectionRegistered | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA003` | ConnectionStopped | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA004` | RequestCompleted | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA005` | RequestFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA006` | CacheReadFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA007` | CacheWriteFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA008` | CacheHit | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA009` | CacheMiss | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA010` | CacheInvalidated | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA011` | ClientMissing | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA012` | ConnectionStopFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA013` | ConnectionStartFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA014` | ConnectionStarted | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA015` | ConnectionRegistrationRejected | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA016` | ClientRegistered | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA017` | ClientUnregistered | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA018` | ClientUnregistrationMissing | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA019` | RequestStaleSuppressed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA020` | RequestCancelled | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA021` | CircuitOpened | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA022` | CircuitRejected | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA023` | RateLimitRejected | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA024` | FallbackApplied | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA025` | FallbackFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA026` | BackpressureDropped | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA027` | StreamCompleted | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA028` | StreamFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA029` | ContributionRegistered | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA030` | ContributionRevoked | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA031` | ContributionRejected | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA032` | HandlerFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA033` | TransferProgressFailed | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA034` | TransferCompleted | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA035` | CacheInvalidationUnsupported | `src/AtomUI.City.Data/Diagnostics.cs` |
| `AUCDATA036` | SignalRPayloadMetadataMissing | `src/AtomUI.City.Data/Diagnostics.cs` |

## 产品级必须诊断的失败

- credential provider 缺失、失败或返回 unavailable：返回 `CredentialUnavailable`，不调用 transport；明确要求登录时返回 `AuthenticationRequired`。
- transport timeout：返回 `Timeout` 并记录 `operationId`；generated descriptor 提供静态 client/operation metadata，不包含 endpoint。
- connection owner dispose：关闭该 owner 的 connection；request/stream cancellation 由 parent scope 或 contribution lease 传播。
- 请求取消：返回 Cancelled，不写缓存和状态。
- SignalR argument、response 或 server message 缺少静态 JSON 元数据：记录 connection id、method、payload type 与 direction；invoke 返回 `SerializationError`，subscribe 抛 `SignalRPayloadMetadataException`。

## 上下文字段

`DataDiagnosticRecord` 的稳定字段是 `Code`、`Message`、`Severity`、`OperationId`、`ClientId`、`OperationName`、`TransportKind`、`Attempt` 和 `ErrorKind`。上层模块通过这些 identity 与自己的 route/plugin/state 诊断关联，不向 Data record 增加领域字段。

默认 `InMemoryDataDiagnostics` 容量为 4096；满载后按 FIFO 淘汰最早记录，并通过 `DroppedCount` 暴露淘汰总数。诊断 sink 异常必须被框架隔离。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Data.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
