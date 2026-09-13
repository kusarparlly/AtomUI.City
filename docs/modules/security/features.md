# AtomUI.City.Security Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-SECURITY-001 | Authentication State Store | Completed | AuthenticationStateStore, AuthenticationStateSnapshot | AuthenticationStateTests |
| AUC-SECURITY-002 | Current Principal Access | Completed | ICurrentPrincipalAccessor, SecurityPrincipals | AuthenticationStateTests |
| AUC-SECURITY-003 | Permission Registry and Checker | Completed | PermissionRegistry, IPermissionChecker, SecurityClaimTypes | PermissionRegistryTests; PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy Evaluation | Completed | AuthorizationEvaluator, AuthorizationPolicy | AuthorizationEvaluatorTests; AuthorizationPolicyTests |
| AUC-SECURITY-005 | Route Authorization Guard | Completed | SecurityRouteGuard, IRouteAuthorizationPolicyProvider | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | Completed | CommandAuthorizationSource, CommandAuthorizationDescriptor | CommandAuthorizationSourceTests |
| AUC-SECURITY-007 | Access Token Provider | Completed | IAccessTokenProvider, AccessTokenResult | SecurityRegistrationTests; AccessTokenCredentialProviderTests |
| AUC-SECURITY-008 | Multi-Account File Persistence | Completed | SecurityAccountKey, IAccountSessionStore, ICredentialStore | AccountPersistenceTests; FileCredentialStoreTests |
| AUC-SECURITY-009 | Active Account Switching and Restore | Completed | IAccountSessionManager, AccountSessionSnapshot, AccountSwitchResult | AccountSessionManagerTests; AccountSwitchIntegrationTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Security 不实现登录 UI，只定义状态、权限、授权和 token contract。 | 必须有实现、测试或工程门禁证据。 |
| 认证状态以 immutable snapshot 发布，跨线程读取必须一致。 | 必须有实现、测试或工程门禁证据。 |
| 授权评估不操作 UI、不执行导航、不访问 VisualTree。 | 必须有实现、测试或工程门禁证据。 |
| Route、Command、Data 只通过 Security public contract 集成。 | 必须有实现、测试或工程门禁证据。 |
| Token 和 refresh token 只能写入声明的账号凭据文件，不得进入普通配置、State、日志或诊断。 | 必须由路径隔离、原子写入和可观察输出泄漏测试持续证明。 |
| 一个 City Host 同一时刻只允许发布一个全局活动账号。 | 切换必须原子提交，失败或取消不得暴露半切换状态。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-SECURITY-001 Authentication State Store

Feature ID: `AUC-SECURITY-001`
Status: Completed
Goal: 集中表达当前用户、认证状态和状态变更通知。
Public Contract: AuthenticationStateStore, AuthenticationStateSnapshot
Runtime / Build Behavior: 认证状态以 cloned immutable snapshot 发布；输入和 getter 输出 principal 均隔离，Actor chain 被保留并隔离，任意层级 `BootstrapContext` 不进入 snapshot；Unknown、Anonymous、Authenticating、Authenticated、Refreshing、Expired、SignedOut、Failed 必须清晰区分。
Failure Behavior: provider 失败不能产生半认证状态；Failed 和 SignedOut 必须清除 principal、scheme 和 expiry token hint。
Threading / Cancellation: 状态更新可来自后台；通知按 revision 有序 drain，用户观察者在内部锁外执行，单个观察者异常被隔离。
Diagnostics: authentication diagnostics 必须包含 old state、new state 和 reason。
Tests: `AuthenticationStateTests`
Required Assertions: 断言 snapshot/Actor chain 不可变、BootstrapContext 清除、状态切换、订阅通知、重复设置和 logout。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-002 Current Principal Access

Feature ID: `AUC-SECURITY-002`
Status: Completed
Goal: 为业务代码提供当前 principal 的同步读取边界。
Public Contract: ICurrentPrincipalAccessor, SecurityPrincipals
Runtime / Build Behavior: 读取返回当前 snapshot 中 principal；无用户时返回 anonymous principal；`SecurityPrincipals.Anonymous` 每次返回独立 unauthenticated principal。
Failure Behavior: principal 缺失、claims 格式异常按 anonymous 或 failed result 处理，不能抛随机异常。
Threading / Cancellation: 读取必须无阻塞；后台线程读取看到一致 snapshot。
Diagnostics: principal diagnostics 只包含 principal kind 或 pseudonymous identity hash，不记录完整 principal/claims；hash 不是匿名化保证。
Tests: `AuthenticationStateTests`
Required Assertions: 断言 authenticated、anonymous、claims 读取和并发 snapshot。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-003 Permission Registry and Checker

Feature ID: `AUC-SECURITY-003`
Status: Completed
Goal: 注册权限定义并提供权限检查。
Public Contract: PermissionRegistry, IPermissionChecker, PermissionDescriptor, SecurityClaimTypes
Runtime / Build Behavior: 权限按 stable name 注册；principal 使用 `SecurityClaimTypes.Permission` claim 表达已授予权限；插件权限带 contribution id；checker 只返回 AuthorizationResult，不执行 UI 或导航。`DefaultPolicy` 和 `IsHostOnly` 当前是描述性 metadata，不由 registry/evaluator 自动执行。
Failure Behavior: 未注册权限返回 Failed/PermissionNotFound；重复权限返回注册失败；贡献撤销后，同一 contribution id 的新权限注册必须被拒绝。
Threading / Cancellation: registry 读并发安全；contribution 通过 id 撤销且不能重新注册；通知按 revision 有序，观察者失败被隔离；checker 必须观察取消 token。
Diagnostics: registry mutation 诊断必须包含 permission name、contribution id、operation 和 revision；授权拒绝诊断仅记录 pseudonymous principal identity hash，不记录 claims，该 hash 不能被当作匿名化保证。
Tests: `PermissionRegistryTests; PermissionCheckerTests`
Required Assertions: 断言注册、重复、未注册、插件撤销和 checker result。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-004 Authorization Policy Evaluation

Feature ID: `AUC-SECURITY-004`
Status: Completed
Goal: 把 requirement、permission、principal 和 resource 汇总为授权结果。
Public Contract: AuthorizationEvaluator, AuthorizationPolicy, AuthorizationRequirement
Runtime / Build Behavior: evaluator 可直接评估 immutable-principal AuthorizationRequest，也可按 policy name 从 IAuthorizationPolicyProvider 读取 policy 后顺序评估非空 requirement 并输出 AuthorizationResult；Policy 必须先捕获 requirement snapshot 再校验，避免可变输入形成空策略；任一 authenticated identity 均表示已认证。
Failure Behavior: policy 缺失返回 Failed/PolicyNotFound；requirement 不满足返回 Challenge、Denied 或 Forbidden；provider 抛异常映射为 Failed/EvaluatorFailed。
Threading / Cancellation: 评估可以异步取消；预取消不调用 provider；只有调用方 token 已取消才返回 Cancelled，其他 OperationCanceledException 返回 Failed。
Diagnostics: authorization diagnostics 必须包含 policy name、requirement kind 和 resource。
Tests: `AuthorizationEvaluatorTests; AuthorizationPolicyTests`
Required Assertions: 断言成功、拒绝、失败、取消、多 requirement 和 provider 异常。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-005 Route Authorization Guard

Feature ID: `AUC-SECURITY-005`
Status: Completed
Goal: 把 Security 授权接入 Routing guard，但不执行导航本身。
Public Contract: SecurityRouteGuard, SecurityRouteGuardOptions, IRouteAuthorizationPolicyProvider
Runtime / Build Behavior: guard 按 route id 向 provider 查询 policy 并读取当前 principal；无 policy 返回 allow；有 policy 时调用 evaluator 并映射为 allow/reject/redirect/cancel/failed；配置 LoginRouteId 后 Challenge 映射为 redirect，否则映射 authentication-required reject。
Failure Behavior: 无权限返回 Forbidden reject；未登录默认返回 AuthenticationRequired reject，配置 login route 后返回 redirect；provider 或 evaluator 未声明异常映射为 guard failed。
Threading / Cancellation: guard 必须观察导航 token；取消后不继续评估。
Diagnostics: route auth diagnostics 必须包含 route id、policy name 和 result status。
Tests: `RouteAuthorizationGuardTests`
Required Assertions: 断言 allow、deny、redirect login、取消和 Routing 无 Security 反向依赖。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-006 Command Authorization

Feature ID: `AUC-SECURITY-006`
Status: Completed
Goal: 把权限变化映射到命令可用性和未授权行为。
Public Contract: CommandAuthorizationSource, CommandAuthorizationDescriptor
Runtime / Build Behavior: command descriptor 声明 policy；Security 发布 CommandAuthorizationState，Presentation/MVVM 只消费禁用或隐藏状态；source 构造订阅失败必须回滚，Dispose 尝试全部退订并聚合失败。
Failure Behavior: descriptor 缺失允许命令；descriptor 缺省继承 policy contribution，显式冲突时构造失败；权限撤销和用户状态变化触发 CommandAuthorizationChanged；descriptor provider 或 evaluator 异常返回 Failed/EvaluatorFailed，不向 UI 冒泡；构造期订阅失败先隔离 source、回滚所有已尝试订阅并聚合/诊断失败，Dispose 继续尝试全部退订并在最后聚合失败。
Threading / Cancellation: 状态变更可来自后台并按 source revision 有序发布；取消返回 Cancelled；UI 更新由 Presentation dispatcher 处理；Dispose 后释放订阅并停止新通知。
Diagnostics: command auth diagnostics 必须包含 command id、policy 和 change reason。
Tests: `CommandAuthorizationSourceTests`
Required Assertions: 断言状态变化、禁用/隐藏策略、contribution 继承/冲突、订阅回滚/释放和权限撤销。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-007 Access Token Provider

Feature ID: `AUC-SECURITY-007`
Status: Completed
Goal: 为 Data 等模块提供 token 获取合同。
Public Contract: IAccessTokenProvider, AccessTokenResult, DelegateAccessTokenProvider
Runtime / Build Behavior: token provider 返回 None/Success/Required/Expired/Failed/Unavailable/Cancelled；Data 在 transport 前调用并映射到 DataCredentialResult；诊断不包含 token。
Failure Behavior: token 缺失或刷新失败返回 AccessTokenResult；DelegateAccessTokenProvider 把 null result 或未声明异常映射为 Failed，不抛给 Data。
Threading / Cancellation: 获取 token 必须在 delegate 前后观察 CancellationToken；只有调用方 token 已取消才返回 Cancelled；并发 refresh 由具体 provider 合并或明确拒绝。
Diagnostics: token diagnostics 必须包含 request scope、status 和 expiry hint。
Tests: `SecurityRegistrationTests; AccessTokenCredentialProviderTests`
Required Assertions: 断言成功、失败、不可用、取消、DI 默认 provider 和 Data 集成前置条件。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-008 Multi-Account File Persistence

Feature ID: `AUC-SECURITY-008`
Status: Completed
Goal: 按稳定账号身份把多个账号的资料、认证凭据和权限快照持久化到应用本地数据目录，并保证账号间数据隔离。
Public Contract: SecurityAccountKey, AccountProfileSnapshot, PersistedPermissionSnapshot, IAccountSessionStore, ICredentialStore
Runtime / Build Behavior: 账号 identity 由 scheme、authority、tenant id 和 subject id 组成；默认 `FileAccountSessionStore` 把资料和权限写入 `accounts/<account-hash>/account.json`，默认 `FileCredentialStore` 把每个资源的 token/refresh token 写入 `credentials/<account-hash>/<resource-hash>.json`；全部文档带 schema version，并通过同目录临时文件原子替换；权限快照携带 revision、签发时间和过期时间。
Failure Behavior: 持久化格式损坏、格式版本过高、路径非法、账号不存在、凭据缺失或 IO 失败必须返回稳定失败；删除账号必须删除其凭据、权限快照和资料。
Threading / Cancellation: 同一账号的写入和删除必须串行化；取消后不得提交部分文件、活动账号指针或凭据引用；并发读取只能观察旧版本或完整新版本。
Diagnostics: 持久化诊断必须包含脱敏 account identity、operation、store kind、schema version 和 failure kind，严禁包含 token、refresh token 或密码。
Tests: `AccountPersistenceTests; FileCredentialStoreTests`
Required Assertions: 断言多账号 round-trip、进程重启恢复、账号隔离、路径约束、原子写入、损坏/高版本拒绝、IO 失败、删除无残留，以及日志、诊断和普通 State 中不存在凭据。
Acceptance Criteria: Security 提供跨平台文件存储合同和默认文件 Provider；不保存密码；文档明确当前文件 Provider 不抵御同一操作系统用户权限下的本地读取，系统安全保险库列为后续版本增强项。

## AUC-SECURITY-009 Active Account Switching and Restore

Feature ID: `AUC-SECURITY-009`
Status: Completed
Goal: 在已持久化的多个账号之间恢复、切换或刷新唯一的全局活动账号，并原子发布对应 principal、token 上下文和权限快照。
Public Contract: IAccountSessionManager, AccountSessionSnapshot, AccountSwitchResult, AccountSwitchResultStatus
Runtime / Build Behavior: `AccountSessionManager` 作为 DI singleton 让一个 City Host 同一时刻只有一个活动账号；`RestoreAsync` 恢复最后活动账号；`SwitchAccountAsync` 先完整读取并验证资料、权限和凭据，再更新活动指针并发布一个 immutable session；同账号普通切换保持幂等。应用/provider 从服务器更新账号与权限缓存后，调用 `RefreshAccountAsync` 强制重载当前账号并发布一个新 revision；传入的 account key 用于阻止账号切换竞态，刷新不修改活动指针。session 与 authentication 使用同一 revision。Route、Command、Data、State 和 Presentation 的联动仍由各模块读取合同或应用 bridge 完成，Security 不直接引用这些上层模块。
Failure Behavior: 目标账号不存在、凭据或权限加载失败、权限快照不兼容、操作取消或刷新目标已不再是当前账号时保留原活动账号；删除当前账号后进入 Anonymous/SignedOut，不自动选择其他账号。
Threading / Cancellation: 切换作为单一异步事务串行执行；取消、失败或并发竞争不得产生混合账号数据；旧账号在途 refresh 和账号绑定操作必须在提交切换前取消或失效。
Diagnostics: 切换诊断必须包含脱敏 previous/target account identity、operation id、online/offline mode、result status 和 failure stage。
Tests: `AccountSessionManagerTests; AccountSwitchIntegrationTests`
Required Assertions: 断言启动恢复、成功切换、重复切换幂等、失败/取消回滚、并发切换、单次 revision/通知、token/权限隔离、删除活动账号和离线受限切换；离线受限 session 必须拒绝默认及仍有效的次级 resource token。
Acceptance Criteria: 离线时只允许加载未损坏的缓存身份和权限用于 UI 与客户端预检查；缺失/过期凭据发布 Expired authentication，过期权限不得进入 principal permission claims；过期 token 和需要服务器确认的操作必须拒绝；重新联网后应用/provider 必须先原子替换服务器结果缓存，再通过 `RefreshAccountAsync` 发布一个完整的新 session revision。
