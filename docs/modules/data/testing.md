# AtomUI.City.Data Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、source generator、AOT 和真实协议行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 每个长连接必须声明 DataConnectionOwner。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 请求取消后不得写入 State、缓存或 UI。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 认证在 transport 执行前完成。 | 至少一个明确测试断言，不能只断言流程成功。 |
| HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-DATA-001 | Unit | DataPipelineTests | 断言执行顺序、取消不写缓存、parent scope late failure suppression、retry diagnostics 和 transport exception retry。 | 取消、scope stop、credential failed、cache failed、missing transport、timeout、transport exception。 | Completed |
| AUC-DATA-002 | Unit | HttpDataTransportTests | 断言 status -> DataErrorKind 映射、request/context 匹配和取消前不进入用户 factory，包含 validation failed 和 gateway timeout。 | 非成功状态码、context mismatch、validation failed、timeout、cancel。 | Completed |
| AUC-DATA-003 | Unit | GrpcDataTransportTests | 断言显式 unary invoker、request/context 匹配、取消前不进入 invoker、null result 防御、GrpcStatusCode 标准数值和完整 status -> DataErrorKind 映射。 | status error、context mismatch、null result、deadline、cancel、resource exhausted、precondition failed、data loss。 | Completed |
| AUC-DATA-004 | Unit | SignalRDataTransportTests | 断言显式 invocation context、request/context 匹配、取消前不进入 invoker、connection closed 和 reconnect failed 映射。 | context mismatch、connection closed、reconnect failed、invoke failed、timeout、cancel。 | Completed |
| AUC-DATA-005 | RuntimeLifecycle | DataConnectionLifecycleTests | 断言 owner、并发事务、同链重入快速失败、显式逆序关闭、启动回滚、关闭继续清理、注册撤销/重试和重复 stop 幂等。 | owner stop、重复 stop、start failed、stop failed、取消、重复 id。 | Completed |
| AUC-DATA-006 | Unit | AccessTokenCredentialProviderTests | 断言 credential before transport、status 映射、provider failure 映射和 credential string 脱敏。 | token missing、token failed、token provider exception、日志泄漏。 | Completed |
| AUC-DATA-007 | Unit | DataRequestCacheTests; DataCacheConsistencyTests; DataPipelineTests | canonical identity、TTL、principal isolation、hit/miss、批量/精确撤销、真实删除数和 cache-hit stale suppression。 | read failed、write failed、invalid/null key、owner stop。 | Completed |
| AUC-DATA-008 | Unit | DataResultTests; DataDiagnosticsTests | 断言 result 不混用 success/error，invalid error metadata 被拒绝，诊断码格式唯一且内存 recorder 有界。 | unknown exception、cancelled、timeout、invalid error kind、blank error message、诊断溢出。 | Completed |
| AUC-DATA-009 | Unit | DataRegistrationTests | 断言默认服务、重复注册和 pre-registration override。 | 重复注册、override、缺失 credential provider。 | Completed |
| AUC-DATA-010 | RuntimeLifecycle | DataHostIntegrationTests; DataConnectionLifecycleTests | 断言 Host stop 关闭连接、拒绝后续请求与注册、显式逆序、并发事务、重入保护和失败隔离。 | shutdown、并发 stop、取消、部分失败。 | Completed |
| AUC-DATA-011 | Contract/RuntimeLifecycle | DataStreamingTests; DataDogfoodTests | 官方 gRPC unary/channel/server/client/bidi stream、metadata、deadline、owner、backpressure、并发 dispose/write。 | cancel、protocol error、owner stop、并发 complete/dispose。 | Completed |
| AUC-DATA-012 | Contract/RuntimeLifecycle | DataDogfoodTests | 官方 HubConnection、push、subscription、真实断线 reconnect、token/account switch 和 handler 重入 stop。 | close、reconnect、owner stop、handler reentry。 | Completed |
| AUC-DATA-013 | Unit/RuntimeLifecycle | DataConcurrencyTests | 六种策略的确定性顺序、抑制、取消和 keyed isolation。 | race、queue full/cancel、duplicate mutation。 | Completed |
| AUC-DATA-014 | Unit/RuntimeLifecycle | DataResilienceTests | circuit breaker、半开探针取消恢复、cache/fallback ordering、rate limit 和 operation/client/global 策略作用域。 | open circuit、abandoned probe、fallback failure、credential short-circuit、rate rejected。 | Completed |
| AUC-DATA-015 | Unit/RuntimeLifecycle/PluginLifecycle | DataCacheConsistencyTests; DataPluginLifecycleTests | canonical identity、TTL、operation/mutation/principal/permission/plugin/route/version 失效和跨失效 stale write suppression。 | unsupported invalidator、TTL/invalidation race、identity collision。 | Completed |
| AUC-DATA-016 | Generator/RuntimeLifecycle | AtomUICityIncrementalGeneratorDataTests; DataClientDescriptorTests | descriptor、继承 operation、generated catalog 原子注册、确定性输出和 NativeAOT publish/run。 | invalid metadata、duplicate identity、partial registration、unsupported signature。 | Completed |
| AUC-DATA-017 | PluginLifecycle | DataPluginLifecycleTests | 插件贡献拒绝、撤销、取消并等待在途 operation、清理和 unload-ready lease。 | cancellation-ignoring request、self-revoking handler、revoked origin cache bypass、connection cleanup。 | Completed |
| AUC-DATA-018 | Contract/RuntimeLifecycle | DataLargePayloadTests; DataDogfoodTests | 流式 IO、进度、range/resume、声明长度完整性、取消、临时文件和内存上限。 | partial file、progress failure、range/length mismatch。 | Completed |
| AUC-DATA-019 | Unit/PluginLifecycle | DataRequestHandlerTests; DataPluginLifecycleTests | handler 顺序、单次 continuation、capability、扩展异常映射和撤销。 | denied capability、handler/source/authorizer failure、double next、direct-pipeline bypass。 | Completed |
| AUC-DATA-020 | Dogfood/RuntimeLifecycle | DataTestDoublesTests; DataDogfoodTests | Testing doubles 与真实本地 HTTP/gRPC/SignalR 压力场景。 | WebSocket fault/reconnect、high concurrency、reentrant stop。 | Completed |
| AUC-DATA-021 | Unit/NativeAOT/RealProtocol | SignalRJsonContractTests; Data NativeAOT protocol gate | source-generated JSON contract、resolver 顺序、受控 protocol 安装、自定义 DTO invoke/response/push。 | 空合同、argument/response/message 元数据缺失、双 TFM reflection fallback。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
