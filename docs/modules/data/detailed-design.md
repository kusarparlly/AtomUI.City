# AtomUI.City.Data Detailed Design 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Detailed Design` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。

## Public Contract

- 只允许通过 `AtomUI.City.Data` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- `DataConnectionOwnerKind` 只允许 Application、Window、Navigation、Route、Activation、Plugin 或 Manual。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-DATA-001 | Request Pipeline | DataPipelineTests |
| AUC-DATA-002 | HTTP Transport | HttpDataTransportTests |
| AUC-DATA-003 | gRPC Transport | GrpcDataTransportTests |
| AUC-DATA-004 | SignalR Transport | SignalRDataTransportTests |
| AUC-DATA-005 | Connection Lifecycle | DataConnectionLifecycleTests |
| AUC-DATA-006 | Authentication | AccessTokenCredentialProviderTests |
| AUC-DATA-010 | Host Lifecycle Integration | DataHostIntegrationTests |
| AUC-DATA-011..021 | Completed 1.0 capabilities | 详见 features.md 与 testing.md |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Detailed Design

适用范围：多传输数据访问、请求管线、HTTP、gRPC、SignalR、异步线程、并发、长连接、缓存、错误模型、认证集成、插件边界和测试策略。

### 1. 定位

`AtomUI.City.Data` 是框架级数据访问基础设施。

Data 不提供 DDD Repository 作为默认范式，不定义领域模型，不替代业务应用自己的 Application Service、Repository 或 Query Service。它只保证所有数据访问都能进入统一生命周期、统一错误处理、统一认证注入和统一诊断链路。

Data 1.0 把 HTTP、gRPC、SignalR 都作为一等访问方式支持；HTTP、官方 gRPC 四种 call shape、官方 SignalR connection 与委托兼容 transport 均已落地：

| 访问方式 | 主要用途 |
|---|---|
| HTTP | REST、Web API、文件上传下载、普通请求响应。 |
| gRPC | 强类型 RPC、unary call、server/client/bidi streaming。 |
| SignalR | 实时连接、服务端推送、双向消息、hub method invoke。 |

核心链路：

```text
ViewModel / Command / Resolver
-> Data client
-> DataRequestContext / optional ParentScope
-> Data request pipeline
-> Security credential
-> Transport
-> Resilience / cache / error mapping
-> DataResult / DataStream / DataConnection
-> State / ViewModel / Resolver result
```

### 2. 设计原则

- .NET-first：优先基于 `HttpClientFactory`、Options、DI、`CancellationToken`、typed client、handler pipeline。
- Multi-transport：HTTP、gRPC、SignalR 都是一等 transport，不把 Data 设计成 HTTP wrapper。
- Pipeline-first：所有请求必须进入 Data pipeline，不能让 ViewModel 直接散落使用裸 transport。
- Lifecycle-aware：每次请求由 `DataRequestContext` 承载独立逻辑 operation transaction，并可绑定 `ParentScope`；standalone stream 可选绑定 `ParentScope`，SignalR subscription 随 connection owner 撤销，长连接必须声明显式 owner。
- Security-integrated：认证凭据只通过 Security 获取，Data 不直接管理登录态。
- State-separated：Data 不隐式写全局 State；请求完成后由调用方或显式 adapter 更新 State。
- AOT-first：Source Generator 只生成稳定的 client/operation metadata registrar；运行时不扫描程序集，也不生成业务 client 实现。
- Plugin-aware：插件 Data client 必须可撤销，运行中请求可取消，不能持有 Host 私有凭据。
- Thread-safe：transport callback 不直接访问 UI，不捕获 UI `SynchronizationContext`。
- Testable：请求管线、transport、认证、缓存、重试、长连接和竞态都必须可替换测试。

### 3. 非目标

Data 不负责：

- 领域模型设计。
- DDD Repository 默认实现。
- 应用服务分层。
- UI loading 展示。
- 认证状态管理。
- 权限策略解释。
- ViewModel 状态管理。
- 离线同步业务策略。
- 数据库 ORM 默认封装。

### 4. 核心抽象

| 类型 | 职责 |
|---|---|
| `IDataClient` | 数据客户端统一标识。 |
| `IDataClientFactory` | 按 contract type 获取已注册 typed client。 |
| `IDataRequestPipeline` | 执行请求管线。 |
| `DataRequest<T>` / `DataRequestContext` | 调用方请求 descriptor 与单次 operation context。 |
| `DataResult<T>` | 标准请求结果，不返回裸异常。 |
| `DataError` | 标准错误模型。 |
| `IRequestResponseTransport` | 请求/响应传输，例如 HTTP、gRPC unary。 |
| `NativeGrpcClient` / `IDataStream<T>` | typed gRPC streaming 调用与 owned response stream。 |
| `IRealtimeConnectionTransport` | 实时连接传输，例如 SignalR。 |
| `IDataConnection` | 长连接实例抽象。 |
| `IDataSubscription` | streaming 或 SignalR 订阅句柄。 |
| `DataConnectionManager` | 管理长连接注册、启动、逆序停止和撤销。 |
| `IDataRequestHandler` | 管线处理器。 |
| `DataErrorMapper` | 把 HTTP/gRPC transport status 转换成 DataError。 |
| `IDataRequestCache` / `IDataCacheInvalidator` | 请求结果缓存、TTL 和显式失效。 |
| `IDataResiliencePolicyProvider` | timeout、retry、circuit breaker、rate limit 和 fallback 策略解析。 |
| `IDataDiagnostics` | 请求诊断、耗时、错误、correlation id。 |

命名不加 `City` 前缀。

协议序列化由 `HttpDataRequest` mapper、protobuf marshaller 和 SignalR client 各自负责，不存在 Data 级 `IDataSerializer`。`NativeGrpcClient` 和 `SignalRRealtimeConnection` 是原生协议入口，委托式 transport 继续作为兼容 adapter。精确边界、失败语义与释放规则见 [api-contracts.md](api-contracts.md)。

### 5. 访问模式

Data 不能只抽象成 `SendAsync`。第一版至少区分三类访问模式：

| 模式 | 适用 | 第一批实现 |
|---|---|---|
| Request / Response | 查询、提交、命令式调用 | HTTP、gRPC unary、SignalR hub invoke。 |
| Streaming | 服务端流、客户端流、双向流 | gRPC streaming。 |
| Realtime Connection | 服务端推送、频道订阅、实时事件 | SignalR。 |

统一链路：

```text
Data client
-> Data operation context
-> Security credential
-> Transport-specific execution
-> DataResult / DataStream / DataConnection
-> State / EventBus / ViewModel
```

### 6. 请求管线

推荐管线：

```text
Runtime gate / concurrency admission
-> Resolve resilience policy
-> Validate ParentScope and contribution capability
-> Resolve authentication credential
-> Cache lookup
-> Circuit/rate admission
-> Apply optimistic update
-> Ordered handlers and transport
-> Retry / fallback / consistency finalization
-> Cache write
-> Final stale/cancellation check
-> Return DataResult and emit diagnostics
```

详细规则见：[request-pipeline.md](request-pipeline.md)。

### 7. 异步和线程

Data 请求属于 Operation。所有耗时 transport 操作必须在后台或 transport 自身异步上下文中执行，不能阻塞 UI Thread。

关键约束：

- 禁止 `.Result` / `.Wait()` / sync-over-async。
- transport callback 不能直接访问 ViewModel 或 UI。
- 请求结果提交前必须检查 parent scope 是否仍然有效。
- Scope 已取消后的 late result 必须被抑制，不能更新 State 或 ViewModel。
- 结果进入 UI 前必须通过 State subscription、Presentation binding 或 dispatcher 显式调度。

详细规则见：[async-and-threading.md](async-and-threading.md)。

### 8. 并发策略

每个 Data operation 应声明并发策略：

| 策略 | 说明 |
|---|---|
| `AllowConcurrent` | 默认允许并发。 |
| `DisallowConcurrent` | 正在执行时拒绝新请求。 |
| `Queue` | 排队顺序执行。 |
| `CancelPrevious` | 新请求取消旧请求。 |
| `LatestWins` | 允许并发，但只有最新结果可提交。 |
| `KeyedSerial` | 同一个 resource key 串行，不同 key 并行。 |

详细规则见：[concurrency.md](concurrency.md)。

### 9. 长连接和实时流

HTTP 和 gRPC unary 是单次 Operation。gRPC streaming、SignalR connection 和 SignalR subscription 是长期资源，必须显式声明生命周期。

连接生命周期：

```text
Application
Window
Navigation
Route
Activation
Plugin
Manual
```

长连接不采用隐式 application lifetime；`DataConnectionOwner` 必须由 Host 配置或调用方显式声明，并由对应 lifecycle hook 调用 manager stop。

详细规则见：

- [connection-lifecycle.md](connection-lifecycle.md)
- [streaming-and-realtime.md](streaming-and-realtime.md)

### 10. Security 集成

Data 不直接读取 token 存储。

```text
Data request
-> auth metadata
-> IAccessTokenProvider
-> credential result
-> attach credential
-> send request
```

401 / 403 语义：

| 状态 | 默认语义 | 默认处理 |
|---|---|---|
| 401 | 认证失效或需要登录 | 映射认证错误；本次 operation 不在 Data 内刷新重试。 |
| 403 | 已认证但权限不足 | 返回 forbidden，不自动重试。 |

Security 可以在 `IAccessTokenProvider` 返回凭据前执行自己的 single-flight refresh；Data 不读取 token store，也不直接触发 refresh。

详细规则见：[security-integration.md](security-integration.md)。

### 11. 缓存和一致性

进入 request pipeline 的 query 可以显式启用结果缓存。Streaming 和 SignalR 不进入该缓存；latest snapshot 或状态投影由应用 adapter/State 明确实现。

缓存必须按主体、权限、插件贡献和 client version 隔离。

详细规则见：

- [caching.md](caching.md)
- [consistency-and-cache-invalidation.md](consistency-and-cache-invalidation.md)

### 12. 错误模型

`DataResult<T>` 不应该让预期失败走异常。

典型错误：

```text
Cancelled
Timeout
NetworkUnavailable
CredentialUnavailable
AuthenticationRequired
AuthenticationExpired
AuthorizationForbidden
BadRequest
ServiceUnavailable
ConnectionFailed
ConnectionClosed
ReconnectFailed
StreamCancelled
DeadlineExceeded
Unavailable
SerializationError
PluginUnavailable
LocalStorageError
Unknown
```

详细规则见：[error-model.md](error-model.md)。

### 13. AOT 和 Source Generator

Data generator 根据 `[DataClient]` 和 `[DataOperation]` 生成：

- assembly-level `GeneratedDataClientManifestAttribute`。
- 实现 `IDataClientDescriptorRegistrar` 的确定性 registrar。
- `DataClientDescriptor` 的 client id、client type、transport kind 和 version。
- `DataOperationDescriptor` 的 operation name、request/response type、access/concurrency、timeout、retry、cache 和 authentication metadata。
- Data client interface 继承得到的 attributed operation。

生成器不生成业务 client/proxy、协议 serializer、endpoint、stream/connection 配置或插件注册代码。应用必须显式调用 `RegisterGenerated<TRegistrar>`；该批注册失败时整体回滚，运行时不扫描程序集发现 Data client。

### 14. 测试策略

`AtomUI.City.Testing` 当前提供四个通用替身：

- `ScriptedDataTransport`。
- `ScriptedDataCredentialProvider`。
- `RecordingDataRequestHandler`。
- `FakeDataConnection`。

cache、resilience、官方协议和 Host/plugin 生命周期使用模块测试内的定向 fake 与真实 headless fixture 验证，不宣称为 Testing 包公开 API。

必须覆盖竞态、取消、重试、缓存、401/403、streaming backpressure、SignalR reconnect、插件卸载和无 UI 调度环境。

详细规则见：[diagnostics-and-testing.md](diagnostics-and-testing.md)。
