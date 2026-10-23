# AtomUI.City.Data Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-DATA-001 | Request Pipeline | Completed | IDataRequestPipeline, DataRequestPipeline | DataPipelineTests |
| AUC-DATA-002 | HTTP Transport | Completed | HttpDataRequest<T>, HttpDataTransport | HttpDataTransportTests |
| AUC-DATA-003 | gRPC Unary Adapter | Completed | GrpcDataRequest<T>, GrpcDataTransport | GrpcDataTransportTests |
| AUC-DATA-004 | SignalR Invocation Adapter | Completed | SignalRDataRequest<T>, SignalRDataTransport | SignalRDataTransportTests |
| AUC-DATA-005 | Connection Lifecycle | Completed | DataConnectionManager, IDataConnection | DataConnectionLifecycleTests |
| AUC-DATA-006 | Authentication | Completed | IDataCredentialProvider, AccessTokenCredentialProvider | AccessTokenCredentialProviderTests |
| AUC-DATA-007 | Request Cache Baseline | Completed | IDataRequestCache, IDataExpiringRequestCache, DataCacheKey | DataRequestCacheTests; DataCacheConsistencyTests; DataPipelineTests |
| AUC-DATA-008 | Error Model | Completed | DataResult<T>, DataError | DataResultTests; DataDiagnosticsTests |
| AUC-DATA-009 | DI Registration | Completed | DataServiceCollectionExtensions | DataRegistrationTests |
| AUC-DATA-010 | Host Lifecycle Integration | Completed | DataModule, DataConnectionManager | DataHostIntegrationTests |
| AUC-DATA-011 | Native gRPC and Streaming | Completed | GrpcChannelConnection, NativeGrpcClient, IDataStream<T> | DataStreamingTests; DataDogfoodTests |
| AUC-DATA-012 | SignalR Realtime Connection | Completed | SignalRRealtimeConnection, IRealtimeConnectionTransport, IDataSubscription | DataDogfoodTests |
| AUC-DATA-013 | Operation Concurrency Policies | Completed | DataConcurrencyPolicy, IDataOperationScheduler | DataConcurrencyTests |
| AUC-DATA-014 | Advanced Resilience Policies | Completed | IDataResiliencePolicyProvider, IDataFallbackProvider | DataResilienceTests |
| AUC-DATA-015 | Cache Consistency and Invalidation | Completed | IDataCacheInvalidator, DataConsistencyOptions | DataCacheConsistencyTests; DataPluginLifecycleTests |
| AUC-DATA-016 | Client Descriptors and Generation | Completed | DataClientDescriptor, DataClientDescriptorCatalog, generated registrar | AtomUICityIncrementalGeneratorDataTests; DataClientDescriptorTests; Data AOT fixture |
| AUC-DATA-017 | Plugin Data Contributions | Completed | DataContributionRegistry, DataContributionLease | DataPluginLifecycleTests |
| AUC-DATA-018 | Large Payload and Progress | Completed | DataLargePayloadClient, DataTransferOptions | DataLargePayloadTests; DataDogfoodTests |
| AUC-DATA-019 | Pipeline Extensibility and Capability | Completed | IDataRequestHandler, IDataCapabilityAuthorizer | DataRequestHandlerTests; DataPluginLifecycleTests |
| AUC-DATA-020 | Testing Infrastructure and Dogfood | Completed | ScriptedDataTransport, ScriptedDataCredentialProvider, RecordingDataRequestHandler, FakeDataConnection | DataTestDoublesTests; DataDogfoodTests |
| AUC-DATA-021 | AOT-safe SignalR JSON Contract | Completed | SignalRJsonContract, SignalRPayloadMetadataException | SignalRJsonContractTests; Data NativeAOT protocol gate |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 每个长连接必须声明 DataConnectionOwner。 | 必须有实现、测试或工程门禁证据。 |
| 请求取消后不得写入 State、缓存或 UI。 | 必须有实现、测试或工程门禁证据。 |
| 认证在 transport 执行前完成。 | 必须有实现、测试或工程门禁证据。 |
| HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。 | 必须有实现、测试或工程门禁证据。 |
| 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-DATA-001 Request Pipeline

Feature ID: `AUC-DATA-001`
Status: Completed
Goal: 请求上下文、credential、cache、transport、retry 和 result mapping。
Public Contract: IDataRequestPipeline, DataRequestPipeline
Runtime / Build Behavior: 请求上下文、credential、cache、transport、retry 和 result mapping；transport exception 必须映射为 TransportError 后参与 retry policy。
Failure Behavior: 取消、credential failed、cache failed、missing transport、timeout、transport exception。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataPipelineTests`。
Required Assertions: 断言执行顺序、取消不写缓存、retry diagnostics 和 transport exception retry。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-002 HTTP Transport

Feature ID: `AUC-DATA-002`
Status: Completed
Goal: HTTP request/response 到 DataResult 映射。
Public Contract: HttpDataRequest<T>, HttpDataTransport
Runtime / Build Behavior: HTTP request/response 到 DataResult 映射；401、403、404、409、422、429、503、504 和 5xx 必须映射为稳定 DataErrorKind。
Failure Behavior: 非成功状态码、request/context mismatch、null request message、validation failed、timeout、cancel。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `HttpDataTransportTests`。
Required Assertions: 断言 status -> DataErrorKind 映射、request/context 一致性和已取消 token 不进入用户 factory，包含 validation failed 和 gateway timeout。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-003 gRPC Unary Adapter

Feature ID: `AUC-DATA-003`
Status: Completed
Goal: 通过显式 invoker 接入 gRPC unary，并完成 status 到 DataResult 映射；不代表原生 channel 或 streaming 已实现。
Public Contract: GrpcDataRequest<T>, GrpcDataTransport
Runtime / Build Behavior: gRPC status 到 DataResult 映射；`GrpcStatusCode` 数值必须匹配 gRPC protocol 标准状态码。
Failure Behavior: status error、request/context mismatch、null invoker result、deadline、cancel、resource exhausted、precondition failed、data loss。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `GrpcDataTransportTests`。
Required Assertions: 断言 request/context 一致性、已取消 token 不进入 invoker、null result 防御、GrpcStatusCode 标准数值和完整 status -> DataErrorKind 映射。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-004 SignalR Invocation Adapter

Feature ID: `AUC-DATA-004`
Status: Completed
Goal: 通过显式 invoker 接入 SignalR hub invocation；不代表原生 HubConnection、server push 或 reconnect 已实现。
Public Contract: SignalRDataRequest<T>, SignalRDataTransport
Runtime / Build Behavior: SignalR invocation context 必须包含 hub、method、request context、credential 和 cancellation token；连接生命周期异常映射为稳定 DataErrorKind。
Failure Behavior: request/context mismatch、connection closed、reconnect failed、invoke failed、timeout、cancel。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `SignalRDataTransportTests`。
Required Assertions: 断言 request/context 一致性、已取消 token 不进入 invoker、invocation context、connection closed 和 reconnect failed。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-005 Connection Lifecycle

Feature ID: `AUC-DATA-005`
Status: Completed
Goal: 连接 owner、启动、停止、失败和释放。
Public Contract: DataConnectionManager, IDataConnection
Runtime / Build Behavior: 连接 owner、显式注册序号、启动、逆序停止、失败和释放；已停止连接重复 stop 必须幂等，registration revoke 失败后允许重试。
Failure Behavior: owner dispose、重复 stop、start failed、stop failed、同调用链重入快速失败。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataConnectionLifecycleTests`。
Required Assertions: 断言状态转换、owner 释放、重复 stop 幂等、外部并发事务合并、内部重入不死锁、严格逆序关闭和 revoke retry。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-006 Authentication

Feature ID: `AUC-DATA-006`
Status: Completed
Goal: 从 Security 获取 token 或 credential。
Public Contract: IDataCredentialProvider, AccessTokenCredentialProvider
Runtime / Build Behavior: 从 Security 获取 token 或 credential；token provider 非取消异常必须映射为 Unavailable credential result。
Failure Behavior: token missing、token failed、token provider exception。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `AccessTokenCredentialProviderTests`。
Required Assertions: 断言 credential before transport、status 映射、provider failure 映射。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-007 Caching

Feature ID: `AUC-DATA-007`
Status: Completed
Goal: 提供 canonical request identity、TTL、精确和多维批量失效。
Public Contract: IDataRequestCache, IDataExpiringRequestCache, IDataCacheInvalidator, DataCacheKey, DataCacheFingerprint
Runtime / Build Behavior: key 覆盖 endpoint/method/payload fingerprint、transport、access mode、principal/permission/plugin/client/policy revision；内存缓存支持 TTL 和按 owner/revision 批量失效。
Failure Behavior: read failed、write failed、invalid key。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataCacheConsistencyTests; DataPipelineTests; DataPluginLifecycleTests`。
Required Assertions: 断言 canonical identity、TTL、principal isolation、hit/miss、mutation 和 plugin revoke invalidation。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-008 Error Model

Feature ID: `AUC-DATA-008`
Status: Completed
Goal: DataResultStatus、DataErrorKind 和 mapper。
Public Contract: DataResult<T>, DataError
Runtime / Build Behavior: DataResultStatus、DataErrorKind 和 mapper；success result 不携带 error，failed/cancelled/stale result 不携带 value；DataError kind 和 message 必须有效。
Failure Behavior: unknown exception、cancelled、timeout、invalid error kind、blank error message。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataResultTests; DataDiagnosticsTests`。
Required Assertions: 断言 result 不混用 success/error，invalid error metadata 被拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-009 DI Registration

Feature ID: `AUC-DATA-009`
Status: Completed
Goal: 默认 pipeline、factory、transport、diagnostics 注册。
Public Contract: DataServiceCollectionExtensions
Runtime / Build Behavior: 默认 pipeline、factory、HTTP/gRPC/SignalR transport、diagnostics 注册；重复 AddData 不重复默认 transport。
Failure Behavior: 重复注册、override、缺失 credential provider。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataRegistrationTests`。
Required Assertions: 断言默认服务、重复注册和 pre-registration override。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-DATA-010 Host Lifecycle Integration

Feature ID: `AUC-DATA-010`
Status: Completed
Goal: Data runtime 通过 `DataModule` 接入 City Host，并在 Host shutdown 时确定性关闭全部连接。
Public Contract: DataModule, DataConnectionManager, DataConnectionRegistration
Runtime / Build Behavior: shutdown 先关闭默认请求管线的 runtime gate 并阻止新连接注册；连接按显式注册序号逆序关闭；单个关闭失败不阻断其他连接；registration revoke 幂等且失败后可重试。
Failure Behavior: 多个关闭失败聚合；单个失败保留原异常；owner 已停止后的注册返回 PolicyRejected。
Threading / Cancellation: 锁内只发布事务 Task，用户 start/stop 在锁外执行；外部并发调用共享事务，同一异步调用链重入快速失败；取消终止后续 stop，无更早失败时传播 OperationCanceledException。
Diagnostics: 复用 AUCDATA002-003、012-015。
Tests: `DataConnectionLifecycleTests; DataHostIntegrationTests`。
Required Assertions: Host stop 后拒绝新请求、并发启停、重入保护、启动回滚、显式逆序关闭、关闭继续清理、registration revoke/retry。
Acceptance Criteria: Host 生命周期、失败隔离和并发幂等由测试证明。

## Completed 1.0 Features

`AUC-DATA-011` 至 `AUC-DATA-021` 已由正式 contract、专项测试和真实 headless/NativeAOT fixture 完成验收；
`AUC-DATA-003/004` 的委托适配入口继续作为兼容层保留。

| Feature ID | 完成合同 |
| --- | --- |
| AUC-DATA-011 | 基于官方 gRPC client 的 unary/channel/server/client/bidi streaming、metadata、deadline、owner、backpressure 和并发释放事务。 |
| AUC-DATA-012 | 基于官方 SignalR client 的 HubConnection、invoke、server push、订阅撤销、reconnect、token/account switch 和 owner shutdown；JSON payload 元数据由 AUC-DATA-021 约束。 |
| AUC-DATA-013 | AllowConcurrent、DisallowConcurrent、Queue、CancelPrevious、LatestWins、KeyedSerial 全部具有确定性执行与竞态测试。 |
| AUC-DATA-014 | timeout/retry 之外补齐 circuit breaker、fallback、rate limit、策略作用域和诊断。 |
| AUC-DATA-015 | canonical cache identity、TTL、operation/mutation/principal/permission/plugin/route/version invalidation、stale write suppression 和失败隔离。 |
| AUC-DATA-016 | typed/generated client descriptor、继承 operation、原子 generated catalog、无运行时程序集扫描。 |
| AUC-DATA-017 | 插件 client/handler/cache/connection contribution 可拒绝新操作、撤销、取消并卸载。 |
| AUC-DATA-018 | upload/download progress、流式 IO、临时文件、range/resume、声明长度完整性、取消和大文件内存上限。 |
| AUC-DATA-019 | 固定阶段内可组合 handler、单次 continuation、metadata validation、plugin capability 和扩展异常映射。 |
| AUC-DATA-020 | Testing 包 test doubles、真实本地 HTTP/gRPC/SignalR headless fixture、压力与竞态门禁。 |
| AUC-DATA-021 | 应用提供 source-generated SignalR JSON 合同；缺失元数据统一 fail-closed；真实自定义 DTO invoke/response/push 双 TFM NativeAOT 通过。 |

### AUC-DATA-011 Native gRPC And Streaming

`GrpcChannelConnection` 与 `NativeGrpcClient` 基于官方 gRPC client 提供 unary、server/client/bidi streaming、metadata、deadline、credential、owner 和有界 backpressure。流为单消费者，释放幂等；client stream 的并发 complete 合并为一个事务，client/duplex stream 的并发 dispose 共享事务并等待当前 writer 后释放 call。

### AUC-DATA-012 SignalR Realtime Connection

`SignalRRealtimeConnection` 提供 HubConnection start/stop、invoke、server push subscription、automatic reconnect、principal switch 与 owner shutdown。状态观察者和订阅 handler 均不在生命周期门禁内执行；撤销后拒绝在途或后续消息。

### AUC-DATA-013 Operation Concurrency Policies

`DataOperationScheduler` 实现 AllowConcurrent、DisallowConcurrent、Queue、CancelPrevious、LatestWins 和 KeyedSerial。Queue 使用显式 FIFO 事务链并限制未完成队列长度；LatestWins 抑制旧结果，CancelPrevious 取消旧 operation。

### AUC-DATA-014 Advanced Resilience Policies

`IDataResiliencePolicyProvider`、circuit breaker、fixed-window rate limit 与 `IDataFallbackProvider` 进入 pipeline。认证/授权失败不会触发 fallback；诊断 sink 或 fallback 失败不会覆盖原 transport failure。

### AUC-DATA-015 Cache Consistency And Invalidation

`DataConsistencyOptions` 支持 mutation 成功后的批量失效与 optimistic apply/confirm/rollback。operation、principal、permission、plugin、route reason、client/policy revision 均可作为失效条件；默认内存 cache 通过 mutation epoch 防止在途 query 在失效后回写旧结果。插件请求自动绑定其 active contribution id，显式不匹配会被拒绝；自定义 cache 不支持批量失效时发出 `AUCDATA035`。

### AUC-DATA-016 Client Descriptors And Generation

`DataClientAttribute` 与 `DataOperationAttribute` 由 incremental generator 生成确定性的 typed descriptor registrar 和 manifest，并包含 client interface 继承的 attributed operation。运行时通过 `RegisterGenerated<TRegistrar>` 原子装载，任一 descriptor 失败时回滚本批注册；不执行程序集扫描，NativeAOT fixture 已完成发布和运行验证。

### AUC-DATA-017 Plugin Data Contributions

`DataContributionRegistry` 为插件签发不可伪造的 origin/lease，统一持有 client、descriptor、handler、connection 和 cache owner。Revoke 先拒绝新请求、取消并等待在途请求，再逆序关闭连接并清除注册与缓存；handler 内发起自身撤销只启动事务，不等待自身退出。

### AUC-DATA-018 Large Payload And Progress

`DataLargePayloadClient` 使用固定大小缓冲区执行 upload/download，支持节流进度、HTTP range resume、unsupported-range policy、响应声明长度校验和临时文件所有权。取消或失败时先释放文件句柄，再删除未提交临时文件。

### AUC-DATA-019 Pipeline Extensibility And Capability

`IDataRequestHandler` 按 `Order` 组成固定链；每个 handler 每次 attempt 只能调用 continuation 一次。动态插件 handler 由 `IDataRequestHandlerSource` 提供。capability 与 contribution 活性校验在 credential/cache 之前执行，属于 pipeline 本体合同，即使直接构造 pipeline 也不能通过缓存短路绕过；handler 异常统一映射并参与 resilience，handler source/authorizer failure 在 transport 前映射为 `PolicyRejected`。

### AUC-DATA-020 Testing Infrastructure And Dogfood

`AtomUI.City.Testing` 提供 scripted transport/credential、recording handler 和 fake connection。`AtomUI.City.Data.HeadlessApp` 在本地真实启动 HTTP/1.1、HTTP/2 gRPC 和 SignalR，覆盖并发、四种 gRPC 调用、WebSocket 断线重连、身份切换、handler 重入 stop 和大载荷；`AtomUI.City.Data.AotApp` 以原生进程验证 generated catalog、HTTP、gRPC 以及自定义 DTO SignalR invoke/response/push 路径。

### AUC-DATA-021 AOT-safe SignalR JSON Contract

Feature ID: `AUC-DATA-021`
Status: Completed
Goal: 让应用显式提供 SignalR 业务 payload 的 source-generated JSON 元数据，并在所有运行模式中拒绝反射 fallback。
Public Contract: `SignalRJsonContract`, `SignalRPayloadDirection`, `SignalRPayloadMetadataException`, `SignalRConnectionOptions.JsonContract`
Runtime / Build Behavior: 一个或多个 `IJsonTypeInfoResolver` 按声明顺序组成不可变 JSON 合同；City 将该合同安装到 `JsonHubProtocol`，并在 invocation argument、response 与 server message 进入协议前验证根类型元数据。
Failure Behavior: 缺少元数据时，invoke 返回 `SerializationError`，subscribe 抛出 `SignalRPayloadMetadataException`，二者均写入 `AUCDATA036`；CoreCLR 与 NativeAOT 行为一致，不回退反射序列化。
Threading / Cancellation: 合同创建后只读且可跨连接并发使用；元数据预检发生在网络操作前，不改变调用方 cancellation 语义。
Diagnostics: `AUCDATA036`。
Tests: `SignalRJsonContractTests`; Data 真实协议 NativeAOT gate。
Required Assertions: resolver 组合顺序、空合同拒绝、argument/response/message 缺失元数据 fail-closed、自定义 DTO invoke/response/push 在 `net8.0` 与 `net10.0` 原生进程通过。
Acceptance Criteria: 公开 API、迁移说明、专项测试和双 TFM 真实 SignalR 原生门禁全部完成。
