# AtomUI.City.Security Authentication 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Authentication` 相关实现决策，不重新定义模块边界。

## 设计决策

- 授权失败返回明确 result，不能直接操作 UI。
- 当前权限声明必须来自 registry；plugin capability 属于未来 PluginSystem 集成。
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

## AtomUI.City.Security Authentication 设计

适用范围：认证状态、当前主体、登录、登出、刷新、恢复会话、Token 获取和状态通知。

### 1. 定位

Authentication 子模块负责描述当前应用是否有已认证主体，以及认证状态如何变化。

它不实现具体身份协议，不提供登录 UI，不决定用户管理业务。应用可以接入本地账号、企业 SSO、OIDC、设备认证或自定义认证方式，但必须统一汇入 Security 的认证状态模型。

### 2. 认证状态机

认证状态词汇如下。它描述可发布的 snapshot 状态，不是由 `AuthenticationStateStore` 强制执行的转换图；具体认证编排器负责决定允许的转换：

```text
Unknown
-> Anonymous
-> Authenticating
-> Authenticated
-> Refreshing
-> Expired
-> SignedOut
-> Failed
```

状态说明：

| 状态 | 说明 |
|---|---|
| `Unknown` | 应用刚启动，认证缓存或会话尚未恢复。 |
| `Anonymous` | 明确无登录主体。 |
| `Authenticating` | 正在执行登录或认证恢复。 |
| `Authenticated` | 当前有有效主体。 |
| `Refreshing` | 正在刷新 token 或会话。 |
| `Expired` | 当前主体或凭据过期。 |
| `SignedOut` | 已登出，凭据已清理。 |
| `Failed` | 认证流程失败。 |

认证状态变化必须可诊断、可订阅、可测试。

`AuthenticationStateStore` 发布的 `AuthenticationStateSnapshot` 必须克隆传入的 `ClaimsPrincipal`，每次读取 `Principal` 也返回防御性副本。调用方修改输入 principal 或读取结果均不得改变已发布 snapshot；标准 identity、claim 和 Actor chain 语义必须保留，但 Actor chain 任意层级的 `ClaimsIdentity.BootstrapContext` 都可能携带凭据，不得被复制到 snapshot。

重复设置等价状态必须幂等：不递增 revision，不重复触发 `StateChanged`。例如已经处于 Anonymous 时再次 `SetAnonymous()` 必须返回当前 snapshot。

### 3. 当前主体

当前主体使用 `ClaimsPrincipal` 表达。

规则：

- 未登录时返回空主体或 anonymous principal，但不能返回 null。
- `SecurityPrincipals.Anonymous` 必须返回独立 unauthenticated principal，调用方修改其中一个 anonymous principal 不得污染后续读取。
- `ClaimsPrincipal` 表达身份、claim、role 等通用信息。
- Security 不内置业务用户模型。
- 业务用户信息应由应用或 Data 模块按需加载。
- 插件不能修改 Host root principal。

`ICurrentPrincipalAccessor` 只提供读取入口。当前写入通过 `AuthenticationStateStore` 完成；读取到的 principal 是当时 snapshot 的独立副本，后续状态变化和调用方 mutation 都不能反向修改彼此。

### 4. 认证流程边界

当前源码没有 `IAuthenticationService`。应用提供认证协议适配器，并在自己的 orchestration 中组合：

```text
SignInAsync
SignOutAsync
RefreshAsync
RestoreAsync
ChallengeAsync
```

上述名称是应用流程示意，不是 Security 当前 public API。规则：

- 所有方法接收 `CancellationToken`。
- 登录流程不能阻塞 UI Thread。
- 登录 UI 由 Presentation 或应用提供，Security 只发起 challenge 或返回认证请求。
- 登出必须取消未完成 refresh，并清理 principal、scheme、expiry 等 token hint。
- 恢复会话发生在 Application 启动或解锁时。

### 5. Token 和凭据

Data 管线通过 `IAccessTokenProvider` 获取认证信息。

规则：

- Token 不能作为普通全局状态随意暴露。
- Token 获取必须支持取消。
- 具体应用 token provider 可以在 token 快过期时触发 refresh；当前默认 provider 不实现 refresh。
- 默认 `AccountSessionManager` 在当前会话为 `OfflineRestricted` 时不签发任何 resource 的 token，即使凭据文件中某个次级 resource 尚未到期；需要服务器确认的操作必须等应用完成联网刷新并提交新的 Online session。
- 具体 provider 如实现 refresh，必须明确并发请求合并或拒绝策略。
- 具体认证编排器负责在 refresh 失败后发布 Expired、SignedOut 或 Failed。
- Provider 失败进入 Failed 时必须清理 principal、scheme 和 expiry，不能保留半认证状态。
- `AUC-SECURITY-008/009` 定义凭据存储抽象、账号隔离规则和会话编排；默认提供应用本地数据目录中的文件 Provider，应用可以在 DI 中替换 Provider。
- 默认文件 Provider 允许 access token 和 refresh token 进入声明的账号凭据文件，以支持跨进程恢复；凭据不得复制到普通配置、State、日志或诊断。
- Security 不保存用户密码。当前文件 Provider 不承诺抵御同一操作系统用户权限下的恶意进程或本地文件读取，该限制必须在安全模型中明确声明。

### 6. 多账号持久化与切换

本节是已实现的 `AUC-SECURITY-008/009` 合同。Security 支持在磁盘上持久化多个账号，但一个 City Host 进程同一时刻只发布一个全局活动账号，所有窗口共享该活动主体。

稳定账号身份由以下字段组成：

```text
AuthenticationScheme + Authority + TenantId + SubjectId
```

其中 `TenantId` 可以为空，其余字段必须在写入边界完成规范化和校验。显示名、头像引用、最后使用时间等非敏感资料不能参与账号唯一性判断。

持久化边界：

- `IAccountSessionStore` 保存账号资料、最后活动账号指针和权限快照；文件位于应用本地数据目录，必须使用带 schema version 的原子替换格式。
- `ICredentialStore` 在应用本地数据目录中按账号保存 token、refresh token 和凭据过期信息；凭据文件必须有独立路径、schema version 和原子替换语义。
- 权限快照按账号身份隔离，并包含 permission set、revision、issued-at 和 expires-at；它只服务于离线 UI 和客户端预检查，不替代服务器授权。
- 删除账号必须同时删除账号资料、活动指针引用、权限快照和账号凭据文件。

账号切换流程：

```text
Switch requested
-> atomically load account profile and permission snapshot
-> load credential file
-> drain manager-owned old credential resolution before switch commit
-> atomically publish active session and authentication snapshot
-> notify Route / Command / Data / State / Presentation
```

加载、验证、取消或凭据文件读写任一步失败时，原活动账号保持不变。成功切换或刷新只能发布一次 session/authentication revision，不允许观察到新 principal 配旧 token 或旧权限的通知中间状态。manager 自己的 token 读取与恢复/切换/刷新/删除使用同一事务 gate：已经开始的读取先完成，事务提交后新读取只能看到新账号 revision；读取返回前仍复核 session revision。应用自行创建的账号绑定业务任务必须捕获 revision 或使用自己的 cancellation owner，在 revision 改变后拒绝提交。删除当前活动账号后进入 Anonymous/SignedOut，不自动选择其他账号。

离线时允许切换到已缓存账号，但会话必须标记为受限模式。受限模式可以驱动本地 UI、菜单和命令预检查；缺失或过期凭据把 authentication 标记为 Expired；过期权限快照保留在 session 供展示和故障说明，但不得复制为当前 principal 的有效 permission claims。过期 token、过期/未缓存授权以及需要服务器确认的业务操作必须拒绝。重新联网后必须刷新认证和权限，并以服务器结果替换缓存。应用/provider 原子保存服务器返回的账号与权限快照后，必须调用 `IAccountSessionManager.RefreshAccountAsync` 强制重载当前账号；普通同账号 `SwitchAccountAsync` 保持幂等，不承担刷新职责。

操作系统安全保险库不属于当前版本目标。后续版本可以为同一 `ICredentialStore` 合同增加 Windows Credential Manager 或数据保护、macOS Keychain、Linux Secret Service Provider，并提供从文件 Provider 原子迁移后删除旧凭据文件的流程。

### 7. 状态通知

`AuthenticationStateStore` 通过 `StateChanged` 发布带 revision 的有序通知。当前 `CommandAuthorizationSource` 直接订阅该事件；Route Guard 和 Data pipeline 在下一次操作时读取当前 Security contract。Presentation、State 和其他业务联动由应用 bridge 显式订阅，不是 Security 的内建广播目标；未来 Plugin capability checker 另行登记 Feature。

```text
AuthenticationStateStore changed
-> publish ordered revision notification
-> Command authorization source invalidates
-> application bridges update any additional consumers
```

### 8. Desktop 场景

桌面应用必须考虑：

- 应用启动时恢复认证状态。
- 应用从休眠或锁屏恢复后重新校验。
- 用户主动切换账号。
- 离线状态下保留有限认证信息。
- 本地缓存损坏。
- 多窗口共享当前主体。

Security 不决定这些策略的具体 UI，只提供状态、错误和扩展点。

### 9. 错误策略

| 场景 | 默认处理 |
|---|---|
| 登录取消 | 返回 canceled，不进入 fatal error。 |
| 登录失败 | 状态进入 Failed，记录诊断。 |
| Refresh 失败 | 由具体 provider/认证编排器返回失败并决定发布 Expired、SignedOut 或 Failed。 |
| Token 不可用 | Data 管线收到 authentication unavailable。 |
| 恢复会话失败 | 进入 Anonymous 或 Failed，按 Host 策略决定。 |
| 凭据文件不可读写 | 返回稳定存储失败，不改变当前活动账号。 |
| 账号切换加载失败或取消 | 保留原活动账号，不发布部分状态。 |
| 权限快照过期或损坏 | 拒绝作为有效授权输入；离线会话进入受限或失败状态。 |

### 10. 测试策略

测试替身：

- Fake authentication state provider。
- 应用认证适配器 fake（不是当前 Security public 类型）。
- Fake access token provider。
- Test principal builder。

必须覆盖：

- 启动恢复为 anonymous。
- 登录成功后主体变化。
- 登出清理 token。
- snapshot 输入/输出 mutation 隔离、Actor chain 保留与隔离、所有层级 `BootstrapContext` 清除和多 identity 认证判断。
- 并发状态变更按 revision 通知，观察者异常不破坏提交。
- 状态变化触发 Command 刷新；Route/Data 在下一次操作中读取最新状态。
- 具体认证 Provider 在提供 refresh 时必须自行覆盖并发合并和失败状态变化；这不是当前默认 Provider 的既有能力。
- 覆盖多账号持久化、删除、重启恢复和跨账号隔离。
- token、refresh token 只出现在声明的凭据文件中，不得进入普通配置、State、日志或诊断。
- 账号切换成功、失败回滚、取消、并发和单次 revision 发布。
- 离线受限切换、过期权限快照和重新联网刷新。
