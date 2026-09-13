# AtomUI.City.Security Detailed Design 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Detailed Design` 相关实现决策，不重新定义模块边界。

## 设计决策

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
| AUC-SECURITY-008 | Multi-Account File Persistence | AccountPersistenceTests; FileCredentialStoreTests |
| AUC-SECURITY-009 | Active Account Switching and Restore | AccountSessionManagerTests; AccountSwitchIntegrationTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Security Detailed Design

适用范围：认证状态、当前主体、权限声明、授权策略、Route Guard、Command 权限联动、Data 认证集成，以及明确标记为 Future 的 Plugin capability、AOT/source generator 和测试工具方向。

### 1. 定位

`AtomUI.City.Security` 是框架级认证状态与授权决策模块。

Security 不实现具体身份系统，不提供登录 UI，不内置用户、租户、组织和角色管理业务。它只提供统一 contract，让 Routing、Mvvm Command、Data、PluginSystem、Presentation 都使用同一套认证和权限判断。

核心链路：

```text
Authentication state
-> Principal / claims
-> Permission / policy evaluation
-> Routing Guard / Command CanExecute / Data auth
-> Presentation display feedback
```

模块边界：

- Security 负责认证状态、当前主体、权限、Policy 和授权结果。
- Routing 负责导航事务和 Guard 执行，不解释权限语义。
- Mvvm 提供 Command 状态接入点，不解释权限语义。
- Data 负责请求管线、重试、缓存和错误模型，通过 Security 获取认证信息。
- PluginSystem 负责插件发现、加载和卸载；当前 Security 只提供 contribution-aware registry/provider，capability 集成属于未来 Feature。
- Presentation 负责登录、拒绝访问、命令禁用和权限提示等 UI 表达，不做授权决策。

### 2. 设计原则

- .NET-first：优先使用 `ClaimsPrincipal`、`Claim`、Options、DI、Hosted service、CancellationToken。
- Security makes decisions：认证和授权判断只能由 Security 统一执行。
- UI-independent：Security 不直接引用 AtomUI/Avalonia，不打开窗口，不显示对话框。
- Observable：认证状态、主体变化和权限变化必须可观察。
- AOT-compatible：当前显式 registry/provider 路径不依赖反射扫描；未来 generator 必须先登记 Feature ID。
- Plugin-aware：插件可以贡献权限点和授权元数据，但 Host 解释、授权和撤销。
- Desktop-aware：支持本地会话、Token 缓存引用、离线状态、锁定/解锁、用户切换和应用恢复。
- Diagnostics-first：授权失败必须能解释是未登录、权限不足、Policy 异常、插件撤销还是认证过期。

### 3. 非目标

Security 不负责：

- 登录界面。
- 用户管理业务。
- 角色管理业务。
- 租户、组织、部门等业务模型。
- 具体 OAuth/OIDC/SAML 客户端实现。
- 密码存储策略实现。
- 插件沙箱。
- Data 请求重试、缓存和错误模型。
- UI 可见性具体样式。

这些由应用、Data、PluginSystem、Presentation 或平台层实现。

### 4. 核心抽象

| 类型 | 职责 |
|---|---|
| `IAuthenticationStateProvider` | 提供当前认证状态，并发布变化通知。 |
| `ICurrentPrincipalAccessor` | 读取当前 `ClaimsPrincipal`。 |
| `IPermissionChecker` | 检查 permission 是否满足。 |
| `IAuthorizationPolicyProvider` | 提供 policy descriptor。 |
| `IAuthorizationEvaluator` | 执行 policy 判断。 |
| `IAccessTokenProvider` | 为 Data 管线提供 token 或 credential。 |
| `AuthenticationStateStore` | 保存并有序发布当前认证状态快照；应用认证适配器显式调用其状态变更方法。 |
| `PermissionRegistry` / in-memory providers | 管理当前 Host 中权限、Policy、Route 和 Command descriptor；通过 contribution id 撤销。 |
| `SecurityDiagnosticIds` + Core `IHostDiagnostics` | 输出稳定认证和授权诊断；Security 不另造诊断存储。 |
| `IAccountSessionStore` / `ICredentialStore` | 可替换的多账号资料、权限和凭据持久化合同；默认实现为版本化原子文件 Provider。 |
| `IAccountSessionManager` | 枚举、恢复、切换、显式刷新和删除账号，并发布唯一完整活动 session。 |

命名不加 `City` 前缀。

当前源码没有 `IAuthenticationService`、`ISecurityStateStore`、`ISecurityContributionRegistry` 或 `ISecurityDiagnostics`。登录、登出和 refresh 网络流程由应用/provider 实现；若未来需要统一 orchestration，必须先分配独立 Feature ID，不能把设想类型写成现有合同。

### 5. 认证状态

认证状态建议统一建模为：

```text
Unknown
Anonymous
Authenticating
Authenticated
Refreshing
Expired
SignedOut
Failed
```

状态快照包含：

- `ClaimsPrincipal`。
- Authentication scheme。
- Refreshing / Expired 状态。
- 过期时间。

快照不包含 token、refresh token、`BootstrapContext`、诊断对象或 contribution owner。Security 是认证状态源；State 模块可以同步非敏感派生状态供 ViewModel/UI 订阅，但不能成为认证状态权威写入方。

详细规则见：[authentication.md](authentication.md)。

### 6. 权限、Policy 和 Capability

第一版区分三层：

| 概念 | 说明 |
|---|---|
| Permission | 稳定权限点，例如 `settings.read`、`project.build`。 |
| Policy | 当前组合规则：需要登录、permission、claim 或 role。 |
| Capability | PluginSystem 的未来 Host 授权概念；Security 当前没有 capability requirement API。 |

Permission 是可声明、可本地化、可诊断的稳定标识。Policy 是运行时决策规则。Capability 目标由未来 PluginSystem 集成 Feature 建模，不能通过当前 `AuthorizationRequirement` 表达。

详细规则见：

- [permissions.md](permissions.md)
- [authorization.md](authorization.md)
- [plugin-integration.md](plugin-integration.md)

### 7. Routing 集成

Routing 不解释权限，只执行 Security 提供或 Security 驱动的 Guard。

```text
Route matched
-> routeId policy lookup
-> Security route guard
-> AuthorizationEvaluator
-> Allow / Reject / Redirect / Cancel / Failed
```

结果语义：

| 结果 | Routing 行为 |
|---|---|
| Allow | 继续导航。 |
| Reject | 导航 rejected，保持当前页面。 |
| Redirect | 交给 NavigationTransaction 统一重定向。 |
| Challenge authorization result | Guard 配置登录路由时映射 Redirect，否则映射 Reject/AuthenticationRequired。 |
| Failed | 导航 failed，进入诊断。 |

详细规则见：[route-integration.md](route-integration.md)。

### 8. Command 集成

Command 的 `CanExecute` 可以接入 Security，但 Mvvm 不实现权限逻辑。

```text
Command auth metadata
-> Security command authorization source
-> CanExecute recompute
-> Presentation updates enabled / disabled state
```

权限 registry、登录态和 command descriptor 变化会触发 Security Command 状态刷新。当前路由、ViewModel active、Validation 和 Operation 等其他维度由 MVVM/应用组合并触发。Presentation 只展示禁用、隐藏或提示，不做权限判断。

详细规则见：[command-integration.md](command-integration.md)。

### 9. Data 集成

Data 管线通过 Security 获取认证信息：

```text
Data request
-> AccessTokenProvider
-> attach auth header / credential
-> send request
-> 401 / 403 handling
-> application/provider refresh orchestration or authorization failure
```

401 默认表示认证失效或需要刷新，但 Security 不自动执行 refresh；Data/应用认证编排器负责选择 refresh 或 challenge。403 默认表示认证有效但权限不足。具体 UI 反馈由 Presentation 或应用决定。

详细规则见：[data-integration.md](data-integration.md)。

### 10. PluginSystem 集成（Future）

当前 Security 只实现 permission、policy、route policy 和 command descriptor 的 contribution id 登记、批量撤销及 tombstone。完整插件 owner、capability、manifest 和跨 registry 撤销事务属于未来 PluginSystem 集成。目标上插件可以贡献：

- Permission descriptor。
- Policy requirement descriptor。
- Route auth metadata。
- Command auth metadata。
- Data client auth metadata。

插件不能：

- 自己解释全局权限。
- 绕过 Host Security。
- 修改 Host root principal。
- 把权限结果静态缓存到 Host。
- 把插件私有类型泄漏到 Host policy contract。

未来 PluginSystem 停用插件时必须编排撤销其权限、Policy、Route/Command 授权元数据，并聚合清理失败。当前各内存 provider 只能独立 `RemoveByContribution`；Command provider 会发布刷新，Route 在下一次查询时生效。

详细规则见：[plugin-integration.md](plugin-integration.md)。

### 11. Presentation 集成

Presentation 只消费 Security 结果：

- Route 被拒绝后的拒绝访问视图。
- Challenge 后的登录交互。
- Command 禁用、隐藏或提示。
- 权限不足提示。
- 用户信息展示。
- 登录态切换后的 UI 刷新。

Presentation 不直接读取权限存储，不解释 Policy。

### 12. AOT 和 Source Generator

当前 Security runtime 使用显式 registry/provider，不扫描程序集，因此现有路径没有反射发现依赖。仓库只有通用 Generator feature 名称，没有 Security permission/policy generator、manifest schema 或对应测试。

Security 专属 generator 是未来候选能力，不属于 `AUC-SECURITY-001~009` 的 Completed 范围。施工前必须新增 Feature ID，并一次性定义 permission/policy/route/command manifest、诊断码、generated output 兼容性和 AOT 测试。

### 13. 错误策略

| 场景 | 默认处理 |
|---|---|
| 未登录访问受保护路由 | 配置登录 route 时 Redirect，否则 Reject/AuthenticationRequired。 |
| 权限不足 | Reject / Forbidden。 |
| Token 过期 | 具体应用/provider 决定 refresh；当前 Security 不自动刷新或登出。`OfflineRestricted` session 对所有 resource 返回 Expired，不得签发仍有效的次级 token。 |
| Policy 抛异常 | Failed，进入 diagnostics。 |
| 跨 provider 的插件权限撤销失败 | 未来 PluginSystem 编排应聚合错误并继续清理；当前 provider 仅返回 bool/count。 |
| Data 401 | Data/应用认证编排器决定 refresh、challenge 或退出登录。 |
| Data 403 | 返回 authorization failure，不自动重试。 |

Security 错误不能静默吞掉，必须进入授权结果或诊断。

### 14. 测试策略

仓库当前没有 Security 专属 Testing helper 包。以下是未来候选能力，施工前必须分配 Feature ID：

- Fake principal。
- Fake authentication state provider。
- Fake permission checker。
- Fake policy evaluator。
- Route guard test helper。
- Command authorization test helper。
- Data auth pipeline test helper。
- Plugin permission contribution test host。

必须覆盖：

- 匿名、已登录、过期及状态失败。
- Route allow / reject / redirect / cancel / failed。
- Command `CanExecute` 随权限变化刷新。
- Data 401 / 403 映射。
- 当前 contribution 注册、批量撤销和 tombstone；完整插件生命周期由未来 PluginSystem Feature 覆盖。
- 当前显式注册的重复权限、撤销和诊断。
- 多账号持久化与切换覆盖文件、并发、失败、取消和恢复测试。

详细规则见：[diagnostics-and-testing.md](diagnostics-and-testing.md)。
