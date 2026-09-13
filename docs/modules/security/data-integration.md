# AtomUI.City.Security Data Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Data Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。
- 授权失败返回明确 result，不能直接操作 UI。
- 当前权限声明来自 registry；plugin capability 属于未来 PluginSystem 集成。
- 认证状态通过 `IAuthenticationStateProvider.StateChanged` 发布；当前只有 Command source 直接订阅，Route/Data 在每次操作中读取当前 Security contract，其他联动由应用 bridge 负责。

## Public Contract

- 只允许通过 `AtomUI.City.Security` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
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
| AUC-SECURITY-001 | Authentication State | AuthenticationStateTests |
| AUC-SECURITY-002 | Current Principal | AuthenticationStateTests |
| AUC-SECURITY-003 | Permission Registry and Checker | PermissionRegistryTests; PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy | AuthorizationPolicyTests; AuthorizationEvaluatorTests |
| AUC-SECURITY-005 | Route Guard | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | CommandAuthorizationSourceTests |
| AUC-SECURITY-007 | Access Token Provider | SecurityRegistrationTests; AccessTokenCredentialProviderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Security Data Integration 设计

适用范围：Data 请求认证注入、AccessTokenProvider、401/403 处理、认证刷新和 Data 管线边界。

### 1. 定位

Data integration 负责让 Data 请求管线使用 Security 提供的认证信息。

Security 不实现 HTTP client、重试、缓存和错误模型。Data 也不解释用户权限，只把认证失败和授权失败反馈给 Security 或调用方。

### 2. 请求认证注入

Data 请求流程：

```text
Data request
-> request auth metadata
-> IAccessTokenProvider.GetTokenAsync
-> attach header / credential
-> send request
-> handle 401 / 403
```

规则：

- Token 获取必须异步。
- Token 获取必须支持取消。
- Token 不应写入普通日志。
- 请求认证 scheme 由 Data client metadata 或 Host 配置决定。
- 匿名请求不能强制获取 token。

### 3. AccessTokenProvider

`IAccessTokenProvider` 返回的不是长期可缓存字符串，而是请求级 credential 结果。

结果建议包含：

- 成功 token。
- 不需要 token。
- 需要登录。
- Token 过期。
- 获取失败。
- 取消。

Data 管线根据结果决定继续请求、challenge、失败或取消。
`Failed` 表示 provider 尝试获取 token 但失败；`Unavailable` 表示当前没有可用 provider 或 token source。Data credential bridge 将二者都转换成 credential unavailable，但保留 message。

### 4. 401 / 403

默认语义：

| 状态 | 说明 | 默认处理 |
|---|---|---|
| 401 | 认证无效、过期或需要登录。 | Data/应用认证编排器决定 refresh 或 challenge；Security 不自动刷新。 |
| 403 | 认证有效但权限不足。 | 返回 authorization failure，不自动重试。 |

具体 token provider 如支持 401 refresh，应声明并发合并策略；Security 默认 `AccountSessionManager` 从当前 Online 活动账号的 credential store 读取 token，但不实现 refresh；无活动账号时返回 Unavailable，`OfflineRestricted` 时对所有 resource 返回 Expired 且不得返回仍在有效期内的次级凭据。

403 不应自动 refresh，除非 Host 显式配置。

### 5. Data Error Model

Security 只提供认证和授权语义。Data 负责把结果映射成 Data error model。

建议映射：

```text
AuthenticationRequired
AuthenticationExpired
AuthorizationForbidden
CredentialUnavailable
```

UI 表达由 Presentation 或应用决定。

### 6. 插件 Data client

本节是未来 PluginSystem/Data 集成目标；当前 Security 没有 capability store 或插件 token broker。

插件 Data client 使用认证信息必须声明 metadata。

规则：

- 插件不能直接读取 Host token 存储。
- 插件只能通过 Security/Data contract 请求 credential。
- Host 可以按 capability 限制插件访问的 Data client。
- 插件停用时取消未完成请求。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| Token 获取取消 | 请求取消。 |
| Token 获取失败 | Data auth failure。 |
| 应用/provider 的 401 refresh 成功 | 是否重试由 Data resilience 策略控制。 |
| 应用/provider 的 401 refresh 失败 | 编排器返回稳定失败并决定是否发布 Expired、SignedOut 或 Failed。 |
| 403 | 返回 Forbidden，不自动重试。 |

### 8. 测试策略

测试必须覆盖：

- 匿名请求不获取 token。
- 受保护请求注入 token。
- Token 获取取消。
- 当前 Data bridge 对 Success/Failed/Unavailable/Cancelled 的映射。
- 401 refresh、并发合并、403 策略由具体 Data/provider Feature 测试。
- 插件 Data client capability 在对应 PluginSystem Feature 建立后测试。
