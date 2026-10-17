# AtomUI.City.Data Async And Threading 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Async And Threading` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。
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

## AtomUI.City.Data Async and Threading 设计

适用范围：异步执行、线程边界、UI 调度、late result suppression、transport callback 和 sync-over-async 禁止规则。

### 1. 定位

Data 请求天然运行在多线程环境中。

HTTP、gRPC、SignalR 回调都不能假设发生在 UI Thread。Data 必须遵守 Core Threading 模型，保证请求不会阻塞 UI，不会在 Scope 已释放后回写旧结果，不会让插件线程或回调阻止卸载。

线程模型见：[Core Threading 设计](../core/threading.md)。

### 2. 基本规则

- Data 模块不能直接假设 UI dispatcher 存在。
- 请求必须接收 `CancellationToken`。
- 请求不能阻塞 UI Thread。
- 禁止 `.Result` / `.Wait()` / sync-over-async。
- transport callback 不能直接更新 ViewModel 或 UI。
- SignalR handler 不能直接写 UI。
- gRPC stream item handler 不能直接写 UI。
- 请求结果进入 State 或 ViewModel 前必须按目标调度策略投递。

### 3. Operation 语义

Data 1.0 的每个请求均具有 `OperationId`、linked `CancellationToken`、timeout、diagnostics 和 `ConcurrencyPolicy`；`ParentScope` 是可选项。`DataRequestContext` 是请求的逻辑 operation descriptor，不重复暴露 token；提供 `ParentScope` 时，pipeline 把调用方、scope、shutdown 和 timeout 合成为一个显式有效令牌：

- OperationId。
- ParentScope。
- CancellationToken。
- Timeout。
- ConcurrencyPolicy。
- Diagnostics。
- Result commit policy。

```text
Data request starts
-> DataRequestContext active
-> transport executes asynchronously
-> result returns
-> validate scope and concurrency state
-> commit or suppress result
```

### 4. Late Result Suppression

late result suppression 是强制规则。

```text
Data request starts
-> user navigates away
-> ActivationScope / RouteScope cancelled
-> request returns later
-> result must be ignored
```

被抑制的结果不能：

- 更新 ViewModel。
- 写入 State。
- 触发 Presentation UI 更新。
- 触发成功通知。

它只能记录诊断，必要时释放 transport resource。

### 5. DispatchPolicy

Data pipeline 不执行隐式 `Task.Run` 或 UI dispatch；同步前段在调用线程执行，异步 continuation 不捕获 UI context。

结果投递：

| 目标 | 推荐方式 |
|---|---|
| ViewModel property | 通过 UI dispatcher 或 Presentation binding 间接更新。 |
| State | 通过 State writer，并由 State subscription 决定 dispatch。 |
| EventBus | 发布事件时由 EventBus subscription 的 DispatchPolicy 决定。 |
| Diagnostics | 后台记录，不访问 UI。 |

### 6. Streaming 回调

gRPC server/duplex item 进入有界 `DataStream` pump；SignalR message 进入 `DataSubscription` dispatcher。

```text
Transport callback
-> DataStream pump / DataSubscription dispatcher
-> backpressure policy
-> mapper
-> State / EventBus / ViewModel boundary
```

不能在 transport callback 内执行业务 UI 逻辑。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| UI dispatcher 不存在 | Data 仍可运行，结果不直接投递 UI。 |
| Scope 已取消 | 抑制结果。 |
| callback 抛异常 | 进入 Data diagnostics；subscription 按 `DataSubscriptionErrorPolicy` 继续或停止。 |
| 取消 | 返回 Cancelled，不作为失败。 |
| sync-over-async | Data API 只提供 async contract；调用方不得使用 `.Result` / `.Wait()`。 |

### 8. 测试策略

当前测试同时覆盖取消、timeout、parent scope late result suppression、无 UI 执行、streaming/realtime 和六种并发策略：

- 无 UI dispatcher 时请求仍可完成。
- Scope 取消后结果不提交。
- `CancelPrevious` 旧请求晚返回。
- SignalR handler 在后台线程回调。
- gRPC stream item 在后台线程回调。
- 无 UI dispatcher 的 Data 测试。
