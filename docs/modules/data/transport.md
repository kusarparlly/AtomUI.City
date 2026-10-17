# AtomUI.City.Data Transport 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Transport` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-011 | Native gRPC and Streaming | DataStreamingTests; DataDogfoodTests |
| AUC-DATA-012 | SignalR Realtime Connection | DataDogfoodTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Transport 设计

适用范围：Data transport 抽象、request/response、streaming、realtime connection、transport metadata 和生命周期差异。

### 1. 定位

Transport 是 Data 与外部数据源交互的传输层抽象。

Data 1.0 目标必须支持；当前完成度以 [features.md](features.md) 为准：

- HTTP request / response。
- gRPC unary 和 streaming。
- SignalR realtime connection。

Transport 只负责传输，不负责业务状态，不解释权限，不直接更新 UI。

### 2. Transport 分类

| 类型 | 说明 | 代表实现 |
|---|---|---|
| `IRequestResponseTransport` | 单次请求/响应。 | HTTP、gRPC unary、SignalR hub invoke。 |
| `NativeGrpcClient` + `IDataStream<T>` | 有开始和结束的 typed stream。 | gRPC server/client/bidi streaming。 |
| `IRealtimeConnectionTransport` | 长连接和实时推送。 | SignalR HubConnection。 |

`IRequestResponseTransport` 承载 HTTP 和委托兼容 adapter；`NativeGrpcClient`/`IDataStream<T>` 与 `IRealtimeConnectionTransport` 分别承载原生 gRPC streaming 和 SignalR realtime public contract。

Transport 分类影响生命周期、取消、错误映射、缓存和测试策略。

### 3. Request / Response

Request / Response transport 输出 `DataResult<T>`。

规则：

- 每次调用创建独立 `DataRequestContext` 逻辑 operation transaction，并联动可选 `ParentScope` token。
- 必须接收 `CancellationToken`。
- 直接调用 transport 时，`DataRequestContext` 必须由同一个 request 创建；不匹配时返回 `PolicyRejected`，防止跨 operation 误用 credential/metadata。
- 已取消 token 不得进入 request factory、response mapper 或 transport invoker；外部调用返回后必须再次观察取消。
- 必须支持 timeout。
- 可以使用 retry、cache 和 error mapping。
- 结果提交前必须检查 parent scope。

### 4. Streaming

Streaming transport 输出 stream handle 或 async stream abstraction。

规则：

- standalone `DataStream` 可通过 `DataStreamOptions.ParentScope` 绑定取消；gRPC stream 的底层 channel 必须声明 connection owner。
- stream 必须支持取消。
- stream item 回调不能直接访问 UI。
- stream 必须有 backpressure policy。
- stream 正常完成和失败进入诊断；本地 cancellation/dispose 正常结束 pump，不伪造 failed item。

### 5. Realtime Connection

Realtime connection transport 输出 `IDataConnection`。

规则：

- 连接生命周期必须显式声明。
- 连接状态变化必须可观察。
- reconnect 策略必须显式配置。
- 用户切换或插件停用必须关闭相关连接。
- server push message 进入 State 或 EventBus 必须通过显式 mapper。

### 6. Transport Metadata

传输 metadata 分层承载：

- `DataRequest<T>`：transport/client/operation/access、auth、cache、resilience、concurrency、consistency、origin 和可选 `ParentScope`。
- `DataRequestContext`：每次调用的 operation id、attempt、已解析 credential 和 operation-local metadata；有效令牌由 transport 方法参数交付。
- `HttpDataRequest`、`NativeGrpcDataRequest`、`SignalRDataRequest`：各协议的 factory/invoker 与映射逻辑。
- generated `DataClientDescriptor` / `DataOperationDescriptor`：静态 client/operation metadata，不包含 endpoint、runtime operation id、serializer 或 connection 实例。

### 7. 错误映射

Transport 层错误必须映射成 DataError。

| Transport | 典型错误 |
|---|---|
| HTTP | status code、network error、timeout、serialization error。 |
| gRPC | status code、deadline exceeded、stream cancelled、metadata error。 |
| SignalR | connection closed、reconnect failed、hub invoke error、protocol error。 |

### 8. 测试策略

Data 1.0 的公开替身由 `AtomUI.City.Testing` 提供：

- `ScriptedDataTransport`。
- `ScriptedDataCredentialProvider`。
- `RecordingDataRequestHandler`。
- `FakeDataConnection`。

测试项目内部的受控 stream/server fixture 不构成公开 API。当前测试覆盖 request/response transport 的成功、失败、取消、超时、context 边界以及 streaming/realtime lifecycle；真实协议链路由 Data headless fixture 覆盖。
