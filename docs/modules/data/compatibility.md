# AtomUI.City.Data Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、generated manifest/output、descriptor catalog 和 plugin contract。

## 模块兼容性硬边界

- 每个长连接必须声明 DataConnectionOwner。
- 请求取消后不得写入 State、缓存或 UI。
- 认证在 transport 执行前完成。
- HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。
- 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `DataRequestPipeline` 的 runtime/concurrency gate -> capability -> credential -> cache -> resilience -> handler/transport -> consistency/cache commit 顺序、取消后不写缓存、transient result retry 和 transport/handler exception retry diagnostics 进入 1.0 兼容承诺；单个 handler continuation 每次 attempt 只能调用一次。
- `HttpDataTransport` 的非成功 status 映射进入 1.0 兼容承诺，特别是 422 -> `ValidationFailed`、504 -> `Timeout`。
- `GrpcStatusCode` 数值与 status mapping、`NativeGrpcClient` 的 metadata/deadline、四种 call shape、stream backpressure 和 completion 行为进入 1.0 兼容承诺。
- `SignalRDataTransport` 的兼容 invoker mapping，以及 `SignalRRealtimeConnection` 的 reconnect、subscription、principal switch、状态通知和 owner shutdown 进入 1.0 兼容承诺。
- `DataConnectionManager` 必须拒绝 ownerless、重复 id、未知 state 或 stopped owner 的长连接；按注册顺序 start、按注册逆序 stop；同一连接的外部并发启停共享事务，首调用 token 驱动底层事务、后续 token 只取消自身等待，同一异步调用链重入快速失败；单个 stop 失败不阻断其他连接；注册句柄撤销幂等，失败后允许重试。
- `AccessTokenCredentialProvider` 必须在匿名请求时跳过 token provider，并把 token provider 非取消异常映射为 `Unavailable` credential result；`DataCredential.ToString()` 不得输出 credential parameter。
- `DataCacheKey` 的 canonical identity/value equality、安全 revision 隔离、TTL、operation/client-version/policy-version 定向失效和 `IDataCacheInvalidator` 匹配维度进入 1.0 兼容承诺；插件请求自动采用 signed origin 的 contribution id，显式不匹配在 lookup 前拒绝；默认内存 cache 抑制跨 invalidation 的 stale write。
- `DataResult<T>` 的 success/error 互斥语义、cancelled/stale 无 value、partial 同时携带 value/error，以及 `DataError` 对未知 kind 和空白 message 的拒绝行为进入 1.0 兼容承诺。
- `DataServiceCollectionExtensions.AddData` 的默认服务集合、重复调用幂等行为和 pre-registration override 优先级进入 1.0 兼容承诺。
- `AddData` 不注册全局 `IAccessTokenProvider` fallback；`AccessTokenCredentialProvider` 只在该合同缺失时内部使用 unavailable fallback，保证 Data -> Security 注册顺序不会遮蔽 Security 的真实 provider。
- `DataModule` 在 Host shutdown 时关闭 runtime gate、取消并 drain 在途请求，再关闭全部连接并阻止新注册；诊断 sink 异常不得改变主流程结果。
- 六种 `DataConcurrencyPolicy` 的排序、取消、抑制与 queue bound 进入 1.0 兼容承诺。
- resilience admission 位于 credential/cache 之后；circuit/rate rejection 可进入显式 fallback。circuit/rate/fallback policy、ordered handler 和 credential/cache 前置的 contribution-aware capability gate 进入 1.0 兼容承诺。
- generated data manifest version 1、registrar 形状、继承 interface operation、批注册失败回滚与“无运行时程序集扫描”进入 1.0 兼容承诺。
- contribution lease 的 Host-signed origin、撤销顺序、在途 operation drain、handler 自撤销重入与 stale-origin 拒绝进入 1.0 兼容承诺。
- 大载荷固定缓冲、进度字段、range fallback、响应声明长度校验和未提交临时文件清理进入 1.0 兼容承诺。
- gRPC client/duplex stream 的并发 dispose 共享事务，并等待当前 writer 退出后释放底层 call；SignalR dispose 共享事务且终态必须为 Stopped 或 Faulted。
- `InMemoryDataDiagnostics` 默认容量 4096、FIFO 淘汰和 `DroppedCount` 属于 1.0 默认行为。

## 数据格式兼容

- generated manifest/output 与外部持久化格式必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。
- `DataCapability` 枚举位不可复用；未知 capability 必须拒绝，不能静默降级。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。

当前废弃项：

- `DataConnectionRegistration(IDataConnection)` 自 1.0 preview 起由编译器 `CS0618` 提示；替代入口是 `DataConnectionManager.Register`，最早在 2.0 删除。直接构造的对象没有 manager 绑定，`RevokeAsync` 只能保持兼容性的空操作。
- `DataErrorKind.StreamCompleted` 自 1.0 preview 起由编译器 `CS0618` 提示，最早在 2.0 删除；正常 stream completion 不创建 `DataError`。
