# AtomUI.City.Data gRPC Client 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `gRPC Client` 相关实现决策，不重新定义模块边界。

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

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data gRPC Client 设计

适用范围：gRPC unary、server streaming、client streaming、bidirectional streaming、deadline、metadata、channel lifecycle 和 status 映射。

### 1. 定位

gRPC 是 Data 第一批一等 transport。

除显式 invoker unary 兼容 adapter 外，`GrpcChannelConnection` 和 `NativeGrpcClient` 已接入官方 gRPC channel、metadata/deadline、credential 及 server/client/bidi streaming。

gRPC client 负责强类型 RPC、低延迟服务调用和 streaming。Data 统一管理 gRPC 调用的生命周期、认证、deadline、错误映射和诊断。

### 2. 调用类型

支持：

| 类型 | Data 抽象 |
|---|---|
| Unary call | `DataResult<T>`。 |
| Server streaming | `IDataStream<T>` 或等价 stream handle。 |
| Client streaming | client stream writer + final `DataResult<T>`。 |
| Bidirectional streaming | duplex stream handle。 |

### 3. Deadline 和取消

gRPC call 必须支持 deadline 和 cancellation。

规则：

- `GrpcCallOptions.DeadlineUtc` 显式传入 gRPC deadline；request pipeline 总 timeout 则通过 cancellation 映射为 Data `Timeout`。
- 调用方把自己的 operation/parent scope cancellation token 传入 native call。
- gRPC `DeadlineExceeded` 稳定映射为 `DeadlineExceeded`；Data pipeline 自身总超时映射为 `Timeout`。
- 用户取消映射为 `Cancelled`。
- 插件停用取消插件 gRPC call。

### 4. Metadata 和认证

认证信息通过 gRPC metadata 注入。

规则：

- credential 由调用方显式提供；应用通常通过 Security/Data credential adapter 获取。
- metadata 不能记录敏感值。
- 认证失败不会在 native stream 内自动 refresh/retry；调用方获取新 credential 后重建 call/stream。
- streaming call 中 token 过期通常需要结束并重新建立 stream，不能假设原 stream 自动续期。

### 5. Channel Lifecycle

gRPC channel 可以跨多个 call。

规则：

- channel owner 必须明确。
- channel state 由 `GrpcChannelConnection.State` 表达；manager 记录 start/stop failure diagnostics。
- Plugin owner 停止时关闭插件 channel。
- Host 不长期持有插件私有 channel callback。

### 6. Status 映射

| gRPC status | 数值 | DataError |
|---|---:|---|
| OK | 0 | 仅用于成功结果。 |
| Cancelled | 1 | unary adapter 映射为 Cancelled；native streaming 取消映射为 StreamCancelled。 |
| Unknown | 2 | Unknown。 |
| InvalidArgument | 3 | ValidationFailed。 |
| DeadlineExceeded | 4 | DeadlineExceeded。 |
| NotFound | 5 | NotFound。 |
| AlreadyExists | 6 | Conflict。 |
| PermissionDenied | 7 | AuthorizationForbidden。 |
| ResourceExhausted | 8 | PolicyRejected。 |
| FailedPrecondition | 9 | Conflict。 |
| Aborted | 10 | Conflict。 |
| OutOfRange | 11 | ValidationFailed。 |
| Unimplemented | 12 | ServerError。 |
| Internal | 13 | ServerError。 |
| Unavailable | 14 | ServiceUnavailable。 |

原生 `RpcException.Status.Detail` 允许为空；native client 必须把空白 detail 视为未提供，并使用稳定的默认错误消息，不能让空 detail 击穿 status 映射。
| DataLoss | 15 | ServerError。 |
| Unauthenticated | 16 | AuthenticationRequired。 |

### 7. Streaming

gRPC streaming 必须遵守：

- `DataStreamOptions.ParentScope` 可选绑定 lifecycle cancellation；未绑定时调用方显式 Dispose。
- Backpressure policy。
- Stream completion/failure diagnostics。
- No direct UI update from stream callback。
- client/duplex writer 串行；并发 dispose 等待当前 write，随后仅释放底层 call 一次。

### 8. 测试策略

当前测试覆盖委托式 unary 成功、取消、timeout、完整 status mapping，以及原生 gRPC 的以下行为：

- unary success。
- deadline exceeded。
- cancellation。
- unauthenticated / permission denied。
- server streaming completion。
- stream cancellation。
- plugin unload cancellation。
