# AtomUI.City.Data API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Request Pipeline | IDataRequestPipeline, DataRequest<T>, DataRequestContext | 统一请求执行。 | runtime/concurrency gate -> capability -> credential -> cache -> resilience -> handler/transport -> consistency/cache commit 顺序稳定；transient transport exception 按 retry policy 处理。 |
| Transports | HttpDataTransport, GrpcDataTransport, SignalRDataTransport, NativeGrpcClient, SignalRRealtimeConnection | HTTP 原生传输、兼容委托适配和官方 gRPC/SignalR client。 | 直接调用时 request/context 必须属于同一操作；取消不得进入用户 delegate；原生 stream/subscription 必须有界、可撤销并受 owner 管理。 |
| Connection | DataConnectionManager, IDataConnection, DataConnectionOwner, DataConnectionRegistration | 长连接生命周期。 | owner 和唯一 connection id 必填；同一连接启停串行；注册句柄撤销幂等；关闭失败继续清理。 |
| Authentication | IDataCredentialProvider, AccessTokenCredentialProvider | 从 Security 获取 credential。 | 匿名请求不取 token；token provider 非取消异常映射为 Unavailable credential result。 |
| Caching | IDataRequestCache, IDataExpiringRequestCache, IDataCacheInvalidator, DataCacheKey | 请求缓存与一致性。 | canonical key 组成稳定；TTL、operation/principal/permission/plugin/client/policy revision 隔离与批量失效可执行；插件 key 自动绑定 active contribution；默认内存 cache 抑制跨 invalidation 的 stale write。 |
| Concurrency / Resilience | IDataOperationScheduler, IDataResiliencePolicyProvider, IDataFallbackProvider | 并发准入和故障策略。 | 六种并发策略确定性执行；fallback 不得掩盖认证授权失败；policy failure 不得进入 transport。 |
| Descriptors / Generation | DataClientDescriptorCatalog, IDataClientDescriptorRegistrar | AOT-safe client/operation metadata。 | generated registrar 显式、原子注册；失败回滚；继承 interface operation 纳入 descriptor；不执行运行时程序集扫描；重复 client/operation identity 拒绝。 |
| Plugin Contributions | DataContributionRegistry, DataContributionLease, IDataCapabilityAuthorizer | 插件数据能力租约。 | origin token 由 Host 签发；活性在 credential/cache 前校验；revoke 拒绝新请求、取消并等待在途请求、关闭连接并清除注册/缓存。 |
| Large Payload | DataLargePayloadClient, DataTransferOptions, DataTemporaryFile | 固定内存上传下载。 | 固定缓冲区、进度节流、range/resume、声明长度完整性校验、取消清理；未 Commit 临时文件随 lease 删除。 |
| Error Model | DataResult<T>, DataError, DataErrorKind | 统一结果和错误模型。 | success result 不携带 error；failed/cancelled/stale result 不携带 value；partial 同时携带可用 value 和 error，且不自动重试或缓存；DataError 拒绝未知 kind 和空白 message。 |
| DI Registration | DataServiceCollectionExtensions | 默认 Data 服务注册。 | AddData 注册 pipeline、credential adapter、client factory、cache、connection manager、diagnostics 和 HTTP/gRPC/SignalR transports；未注册 `IAccessTokenProvider` 时 adapter 内部使用 unavailable fallback，但不占用全局 token provider 合同；因此模块注册顺序为 Data -> Security 时仍由 Security provider 接管；重复调用不重复默认 transport；pre-registration override 保留优先级。 |
| Diagnostics | DataDiagnosticIds | 数据访问诊断。 | AUCDATA001-035 稳定。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IDataRequestPipeline.SendAsync | 执行请求。 | request/token。 | DataResult<T>。 | capability/credential/cache/handler/transport/error mapping 失败；transport/handler exception 映射为 TransportError 并可按策略 retry；handler source/authorizer failure 映射 PolicyRejected；Host stopping 返回 PolicyRejected。 | 必须观察 token；Host stop 取消并 drain 在途请求；取消后不写缓存。 | 并发请求独立 operationId；每个 handler 每次 attempt 只能调用 continuation 一次。 |
| IRequestResponseTransport.SendAsync | 执行传输。 | context/request。 | DataResult<T>。 | timeout/network/protocol 映射。 | 必须观察 token。 | 实现声明线程安全。 |
| DataConnectionManager.Register | 注册受 owner 管理的长连接。 | connection。 | 成功时返回 manager 绑定的 registration。 | ownerless、重复 id、stopped owner 返回 PolicyRejected；未知 state 抛 ArgumentOutOfRangeException。 | 不适用。 | 注册序号单调；相同 id 唯一。 |
| DataConnectionManager.StartOwnerAsync | 按注册顺序启动 owner 的连接。 | owner/token。 | ValueTask。 | 用户 start 失败向调用方传播，并逆序回滚本次已经启动的连接。 | 创建共享事务的首调用 token 驱动底层 start；后续调用 token 只取消自身等待。 | 外部并发共享单连接事务；同一异步调用链重入快速失败。 |
| DataConnectionManager.StopOwnerAsync / StopAllAsync | 按注册逆序停止连接。 | owner/token 或 token。 | ValueTask。 | 单个 stop 失败不阻断其他连接；多个失败聚合。 | 创建共享事务的首调用 token 驱动底层 stop；后续调用 token 只取消自身等待；批量取消终止后续 stop，并按既有失败决定抛取消或聚合异常。 | 外部并发共享单连接事务；重复停止幂等。 |
| DataConnectionRegistration.RevokeAsync | 撤销 manager 返回的连接注册并停止底层连接。 | 无。 | ValueTask。 | stop 失败保留原异常；诊断失败不改变结果。 | 使用确定性 shutdown token。 | 幂等；并发 revoke 合并为一次。 |
| DataClientRegistry.Register / Unregister | 管理按 contract type 索引的 client。 | typed client。 | void / bool。 | 空 client/id 拒绝；缺失 unregister 返回 false。 | 不适用。 | 线程安全；同一 contract type 后注册者替换前注册者。 |
| IDataOperationScheduler.ExecuteAsync | 执行 operation concurrency policy。 | request/delegate/token。 | DataResult<T>。 | queue full 或 duplicate operation 返回 PolicyRejected；scheduler 释放后拒绝新请求。 | 排队和 operation 均观察 token；scheduler 释放会取消所有活动 operation。 | FIFO/KeyedSerial 串行；CancelPrevious 取消旧操作；LatestWins 抑制旧结果；并发替换、完成和释放不重复释放 cancellation source。 |
| NativeGrpcClient unary/streaming | 执行官方 gRPC call。 | method/payload/options/credential/token。 | DataResult 或 owned stream。 | disconnected、RPC status、deadline 和 protocol failure 稳定映射。 | call 与 stream 观察 token。 | client-stream concurrent Complete 合并；complete 后 write 拒绝；stream 并发 Dispose 共享清理事务并等待当前 write 后释放 call。 |
| SignalRRealtimeConnection Start/Stop/Invoke/Subscribe/SwitchPrincipal | 管理 realtime 连接。 | method/arguments/options/token。 | DataResult、subscription 或 ValueTask。 | disconnected invoke 返回 ConnectionClosed；stop 后拒绝新 subscription；释放后 mutation 抛 ObjectDisposedException。 | transport、subscription 和 owner stop 观察 token。 | 生命周期串行；并发 dispose 共享事务；状态观察者锁外分发；观察者重入 dispose 不等待自身。 |
| DataContributionLease.RevokeAsync | 撤销插件全部数据贡献。 | 无。 | ValueTask。 | 多个清理失败聚合；诊断失败隔离。 | 在途请求使用 contribution cancellation；停止连接后等待 handler 退出再释放注册。 | 并发 revoke 共享事务且幂等；handler 同调用链重入只启动事务，避免等待自身。 |
| DataLargePayloadClient Upload/Download | 固定缓冲区传输并报告进度。 | request/stream/progress/options/token。 | DataResult<DataTransferReceipt/DataTemporaryFile>。 | network/disk/range/progress failure 稳定映射或隔离；响应声明长度与实际字节不一致返回 StreamProtocolError。 | 取消返回 Cancelled；临时文件释放后删除。 | 每次调用独立 operationId；不持有调用方 stream。 |

`HttpDataRequest<TResponse>` 同时保留单参数 response mapper 兼容入口，并提供接收 `CancellationToken` 的 mapper；新代码应使用后者，使响应体读取和反序列化可取消。

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AccessTokenCredentialProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataAccessMode` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataAuthenticationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataAuthenticationMode` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataAuthenticationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCacheKey` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCacheLookup<TResponse>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCacheOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataClientRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataConnectionManager` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataConnectionRegistration` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataModule` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataConnectionOwner` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataConnectionOwnerKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataConnectionState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCredential` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCredentialResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCredentialResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataErrorKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataErrorMapper` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataRequest<TResponse>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataRequestContext` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataRequestPipeline` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataResilienceOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataResult<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataTransportKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataDiagnosticRecord` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryDataDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GrpcCallResult<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GrpcDataRequest<TResponse>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GrpcRequestContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GrpcDataTransport` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GrpcStatusCode` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HttpDataRequest<TResponse>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HttpDataTransport` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataClient` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataClientFactory` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataConnection` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataCredentialProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataRequestCache` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IDataRequestPipeline` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRequestResponseTransport` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryDataRequestCache` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SignalRDataRequest<TResponse>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SignalRInvocationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SignalRConnectionClosedException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SignalRReconnectFailedException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SignalRDataTransport` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DataCacheEntryOptions`, `DataCacheInvalidation`, `DataCacheInvalidationReason`, `DataCacheInvalidationResult` | 缓存 contract | TTL、匹配维度或默认失效原因变化必须更新本文档和 compatibility。 |
| `IDataCacheInvalidator`, `IDataExpiringRequestCache`, `DataCacheFingerprint` | 缓存 contract | identity、批量失效或兼容 fallback 变化必须更新本文档和 compatibility。 |
| `DataConcurrencyPolicy`, `DataConcurrencyOptions`, `DataOperationDelegate<T>`, `IDataOperationScheduler`, `DataOperationScheduler` | 并发 contract | 顺序、queue bound、取消或 suppression 变化必须更新本文档和 compatibility。 |
| `DataConsistencyOptions`, `IDataOptimisticUpdate` | 一致性 contract | apply/confirm/rollback 与 invalidation 时机变化必须更新本文档和 compatibility。 |
| `DataCircuitBreakerOptions`, `DataRateLimitOptions`, `DataResiliencePolicyScope`, `IDataResiliencePolicyProvider`, `DefaultDataResiliencePolicyProvider` | resilience contract | 作用域、阈值和默认策略变化必须更新本文档和 compatibility。 |
| `DataFallbackResult<T>`, `IDataFallbackProvider`, `NoDataFallbackProvider` | fallback contract | fallback eligibility 与认证授权隔离规则变化必须更新本文档和 compatibility。 |
| `DataBackpressurePolicy`, `DataStreamOptions`, `IDataStream<T>`, `DataStream<T>` | streaming contract | buffer bound、单消费者、终止和释放语义变化必须更新本文档和 compatibility。 |
| `GrpcCallOptions`, `GrpcChannelConnection`, `NativeGrpcClient`, `NativeGrpcDataRequest<TRequest,TResponse>`, `IGrpcClientStream<TRequest,TResponse>`, `IGrpcDuplexStream<TRequest,TResponse>` | native gRPC contract | metadata/deadline、连接前置条件和 stream lifecycle 变化必须更新本文档和 compatibility。 |
| `SignalRConnectionOptions`, `DataConnectionStateChangedEventArgs`, `IRealtimeConnectionTransport`, `SignalRRealtimeConnection` | realtime contract | reconnect、principal switch、状态通知和 owner shutdown 变化必须更新本文档和 compatibility。 |
| `DataSubscriptionOptions`, `DataSubscriptionErrorPolicy`, `IDataSubscription` | subscription contract | backpressure、handler failure、revoke 或 completion 变化必须更新本文档和 compatibility。 |
| `DataClientAttribute`, `DataOperationAttribute`, `DataOperationDescriptor`, `DataClientDescriptor`, `DataClientDescriptorCatalog` | descriptor contract | attribute schema 或 generated metadata 变化必须更新本文档和 compatibility。 |
| `IDataClientDescriptorRegistrar`, `GeneratedDataClientManifestAttribute` | generator contract | manifest version、registrar shape 或装载方式变化必须更新本文档和 compatibility。 |
| `DataCapability`, `DataRequestOrigin`, `DataRequestOriginKind`, `IDataCapabilityAuthorizer`, `DefaultDataCapabilityAuthorizer` | capability contract | capability 位、Host/plugin origin 和 deny 语义变化必须更新本文档和 compatibility。 |
| `DataContributionRegistry`, `DataContributionLease` | plugin contract | lease ownership、撤销顺序和 stale-origin 行为变化必须更新本文档和 compatibility。 |
| `DataRequestHandlerDelegate<T>`, `IDataRequestHandler`, `IDataRequestHandlerSource` | pipeline extension contract | handler order、取消或异常映射变化必须更新本文档和 compatibility。 |
| `DataTransferStage`, `DataRangeUnsupportedPolicy`, `DataTransferProgress`, `DataTransferOptions`, `DataTransferReceipt`, `DataTemporaryFile`, `DataLargePayloadClient` | transfer contract | buffer、progress、range、temporary ownership 或 cleanup 变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- contract-defined client/operation/owner/connection/cache id 和 credential 字段必须在边界校验空值、空白和非法枚举值；开放 metadata `Items` 的键只遵循底层 `IDictionary` 合同。
- 枚举未知值必须拒绝或映射为明确失败结果。
- `DataRequest<T>` 的 Authentication、Cache、Resilience、Concurrency、Consistency 和 Origin 不接受 `null`；timeout/TTL 必须大于零，retry count 不得为负。
- `DataRequest<T>.Items` 是可由调用方填充、随 request 实例共享的线程安全 metadata；`DataRequestContext.Items` 是每次 pipeline operation 独立的线程安全工作区，两者不会自动复制。
- `DataRequestContext.SetCredential` 仅修改当前 operation context；同一 context 不得并发复用于多个请求。
- `DataCredential.ToString()` 必须脱敏 Parameter，不能把 token 写入日志或调试输出。
- `DataCacheInvalidation.Keys` 拒绝空集合和 null element；精确失效诊断报告真实删除数。
- `DataCacheInvalidation.ForOperation/ForClientVersion/ForPolicyVersion` 提供对应 key 维度的定向失效；默认内存 cache 的 invalidation 与 pipeline write 线性化，epoch 变化时跳过陈旧写入。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、网络、插件代码和 handler 调用前后观察取消。
- 取消后 pipeline 不得写缓存或确认 optimistic update；应用收到取消/抑制结果后不得提交 State、EventBus 或 UI。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。
- 父 `LifecycleScope` 停止优先映射为 `StaleSuppressed`；即使 transport 同时抛出取消或其他异常，也不得把 late result 暴露为普通失败。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## 当前 API 边界

`GrpcDataTransport` 与 `SignalRDataTransport` 是显式 invoker 兼容入口；原生能力分别由 `NativeGrpcClient` 和
`SignalRRealtimeConnection` 提供。Data 不依赖 PluginSystem、State 或 Presentation：插件通过 lease/origin public contract 接入，
状态与 UI 投影由应用 adapter 负责。generated descriptor 只生成 metadata registrar，不生成业务接口实现，也不执行运行时程序集扫描。

`IDataDiagnostics` 是观察通道。框架调用诊断 sink 时必须隔离 sink 异常，诊断实现不可改变请求、缓存、注册或连接生命周期结果。`InMemoryDataDiagnostics` 默认有界容量为 4096，满载淘汰最早记录并增加 `DroppedCount`。

## Deprecated API

| API | Deprecated Since | Replacement | Removal Earliest | Migration | Diagnostic |
| --- | --- | --- | --- | --- | --- |
| `new DataConnectionRegistration(IDataConnection)` | 1.0 preview | `DataConnectionManager.Register` | 2.0 | 通过 manager 注册并使用返回的 registration；直接构造仅保留为无 manager 绑定、撤销为空操作的兼容句柄。 | `CS0618` |
| `DataErrorKind.StreamCompleted` | 1.0 preview | 正常 stream completion，不创建 `DataError` | 2.0 | completion 通过 stream 生命周期正常结束表达，不返回 failed result。 | `CS0618` |

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
