# AtomUI.City.Data Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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
| AUC-DATA-020 | Testing Infrastructure and Dogfood | DataTestDoublesTests; DataDogfoodTests |
| AUC-DATA-021 | AOT-safe SignalR JSON Contract | SignalRJsonContractTests; Data NativeAOT protocol gate |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Diagnostics and Testing 设计

适用范围：Data 诊断字段、错误记录、测试替身、竞态测试、无 UI 测试和插件卸载测试。

### 1. 定位

Data 必须可诊断、可测试。

请求失败不能只表现为 ViewModel 没数据。必须能说明失败发生在哪个 client、哪个 operation、哪个 transport、哪个 retry attempt、哪个 scope 或哪个插件贡献。

### 2. 诊断字段

`DataDiagnosticRecord` 的稳定 1.0 portable envelope 直接提供：

- OperationId / correlation identity。
- DataClientId。
- Operation name。
- Transport kind。
- Retry attempt。
- DataError kind。

cache、resilience、backpressure、connection、handler、contribution 和 transfer 状态通过稳定诊断码表达。RouteId、ActivationScopeId、PluginId 等上层领域上下文不得硬编码进 Data 基础 record；adapter 通过 operation/client/contribution identity 与对应模块诊断关联。

敏感信息不能写入日志：token、password、完整 credential、完整 authorization header。

### 3. 测试替身

Data 1.0 的通用 test doubles 由 `AtomUI.City.Testing` 提供；协议级行为由 Data headless fixture 验证：

- `ScriptedDataTransport`：脚本化 request/response transport。
- `ScriptedDataCredentialProvider`：脚本化 credential 结果。
- `RecordingDataRequestHandler`：记录 handler 调用并可委托后续链路。
- `FakeDataConnection`：可控制 start/stop 的连接替身。

cache、resilience、stream producer、plugin host 和 protocol server 是测试项目内部 fixture，不属于 `AtomUI.City.Testing` 的公开承诺。`InMemoryDataDiagnostics` 是 Data 正式 API，其默认容量为 4096，满后淘汰最早记录并累计 `DroppedCount`。

### 4. 竞态测试

并发、streaming、plugin 和 dogfood 测试必须覆盖：

- 请求完成时 Scope 已取消，结果不提交。
- `CancelPrevious` 旧请求返回晚于新请求。
- `LatestWins` 只提交最新结果。
- SignalR handler 在后台线程回调。
- gRPC stream 慢消费者触发 backpressure。
- credential provider 并发调用不绕过 Data capability/runtime gate；token refresh 合并由提供该能力的具体 provider 自行测试证明。
- 插件卸载时仍有请求、连接、订阅。
- cache 按 principal 隔离。
- mutation retry 被禁止。
- UI dispatcher 不存在时 Data 仍可测试。

### 5. Transport 测试

transport 单元测试与真实 headless fixture共同覆盖：

- HTTP 200 / 401 / 403 / 404 / 409 / 5xx。
- gRPC status mapping。
- gRPC deadline exceeded。
- gRPC server streaming。
- SignalR connect / reconnect / closed。
- SignalR server push。
- upload / download progress。

### 6. 无 UI 测试

Data 测试不得依赖真实 AtomUI/Avalonia UI。

规则：

- 不要求 UI dispatcher 存在。
- 使用受控 transport、credential、connection 和 `TaskCompletionSource` 驱动竞态。
- 真实 HTTP/gRPC/SignalR 协议由无 UI headless fixture 验证。
- Data 不提供 UI dispatcher，也不声明 UI dispatch target。

### 7. 插件卸载测试

插件生命周期测试必须覆盖：

- 插件请求取消。
- 插件在途 request 取消并 drain。
- 插件 SignalR connection stop。
- 插件 callbacks 清理。
- 插件 cache revoke。
- 无插件私有类型引用残留。
