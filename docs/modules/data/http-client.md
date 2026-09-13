# AtomUI.City.Data HTTP Client 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `HTTP Client` 相关实现决策，不重新定义模块边界。

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

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data HTTP Client 设计

适用范围：HTTP / REST / Web API、HttpClientFactory、delegating handler、认证、上传下载、进度和 HTTP 错误映射。

### 1. 定位

HTTP 是 Data 第一批一等 transport。

HTTP client 负责 REST、Web API、文件上传下载和普通 request/response 数据访问。HTTP 能力必须进入 Data pipeline，而不是让 ViewModel 直接使用裸 `HttpClient`。

当前实现包含 `IHttpClientFactory` named client、request factory、credential header、取消感知 response mapper、status/error mapping、pipeline circuit policy 和大载荷进度。具体 Security provider 可以在 `IAccessTokenProvider.GetTokenAsync` 内实现 token refresh/single-flight；Data 不持有 token 生命周期，也不因 HTTP 401 自动刷新和重放请求。

### 2. HttpClientFactory

HTTP transport 基于 `IHttpClientFactory`。

规则：

- 支持 named client。
- 支持 typed client。
- 支持 delegating handler。
- handler lifetime 由 `HttpClientFactory` 管理。
- Data pipeline 不绕开 `HttpClientFactory` 自己创建长期 handler。

### 3. HTTP Pipeline

HTTP 请求经过：

```text
Data request context
-> auth metadata
-> HttpRequestMessage
-> HttpClient delegating handlers
-> response
-> DataError mapper
-> DataResult
```

Data pipeline 管理跨 transport 的生命周期、认证、缓存、resilience 和诊断；HTTP delegating handler 管理 HTTP-specific middleware。

### 4. 认证

HTTP 认证通过 Security credential 注入。

规则：

- Authorization header 由 Data/Security 管线注入。
- Token 不写日志。
- 匿名请求不强制取 token。
- 401 映射为 AuthenticationRequired，由应用认证编排器决定后续 refresh 或 challenge；Data 不自动重放当前请求。
- 403 返回 authorization forbidden。

### 5. 上传下载

Data 1.0 的 HTTP 大载荷由 `DataLargePayloadClient` 实现：

- Upload progress。
- Download progress。
- Streaming content。
- Range request。
- Temporary file。
- Cancellation。

进度通知必须节流，避免 UI 高频刷新。

### 6. 错误映射

| HTTP | DataError |
|---|---|
| 400 | BadRequest。 |
| 401 | AuthenticationRequired。凭据刷新失败可由 credential provider 独立返回 AuthenticationExpired。 |
| 403 | AuthorizationForbidden。 |
| 404 | NotFound。 |
| 409 | Conflict。 |
| 422 | ValidationFailed。 |
| 408 / timeout | Timeout。 |
| 429 | PolicyRejected。 |
| 503 | ServiceUnavailable。 |
| 504 | Timeout。 |
| 5xx | ServerError。 |
| network error | NetworkUnavailable 或 TransportError。 |
| response body IO 中断 | TransportError；满足 retry 与幂等条件时可重试。 |
| response payload 格式错误 | SerializationError。 |

### 7. 缓存

HTTP request/response 可以使用 Data cache。

缓存 key 必须包含 principal revision、auth scheme、client id、operation name、参数 hash 和 plugin contribution。

### 8. 测试策略

当前测试覆盖 named client、auth header、status mapping、取消、response body IO/mapper failure、resilience 和大载荷传输：

- typed client。
- auth header 注入。
- 401 映射且不自动 refresh / replay。
- 403 forbidden。
- HTTP status 映射。
- upload/download cancellation。
- progress throttle。
- cache hit/miss。
