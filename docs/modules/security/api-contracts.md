# AtomUI.City.Security API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Authentication | AuthenticationStateStore, AuthenticationStateSnapshot | 当前用户状态。 | snapshot 对输入和输出 principal 都做防御性复制，跨线程读取一致。 |
| Permission | PermissionRegistry, IPermissionChecker | 权限定义和检查。 | 未注册权限稳定失败；撤销 contribution 后拒绝同一 contribution 重新注册。 |
| Authorization | AuthorizationEvaluator, AuthorizationPolicy | 策略评估。 | 不操作 UI 或导航。 |
| Route/Command Integration | SecurityRouteGuard, CommandAuthorizationSource | 把授权结果暴露给 Routing 和 Presentation/MVVM。 | 只返回 result 或 state，不执行 UI。 |
| Token | IAccessTokenProvider, AccessTokenResult | 为 Data 等模块提供 token。 | 失败返回 result，不抛随机异常。 |
| Multi-Account File Persistence | SecurityAccountKey, IAccountSessionStore, ICredentialStore | 把多个账号、凭据和权限快照持久化到应用本地数据目录。 | 账号数据严格隔离；写入原子提交；凭据不得复制到普通配置、State、日志或诊断。 |
| Account Session | IAccountSessionManager, AccountSessionSnapshot, AccountSwitchResult | 枚举、恢复、切换、刷新和删除账号，并维护唯一全局活动账号。 | 失败或取消保留原账号；成功只发布一次完整 session revision。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| AuthenticationStateStore.SetAnonymous / SetAuthenticating / SetAuthenticated / SetRefreshing / SetExpired / SetSignedOut / SetFailed | 发布认证状态。 | 接收 principal 的方法不允许 null；Authenticated/Refreshing/Expired 需要至少一个 authenticated identity；SetFailed 只接收非空 failure message。 | AuthenticationStateSnapshot。 | Failed 和 SignedOut 清除 principal、scheme、expiry；Refreshing/Expired 在同一提交锁内继承未显式覆盖的当前 scheme/expiry；外部 principal mutation 和通过 getter 取得的 principal mutation 都不影响已发布 snapshot；identity/claim/Actor chain 被复制，但任何层级的 `BootstrapContext` 均不进入 snapshot。 | 同步提交无 token。 | 等价重复设置幂等；成功变更按 revision 顺序通知；观察者异常被隔离并写 `AUCSEC002`，不能回滚已提交状态或阻断其他观察者。Store 校验 snapshot 内容，但不强制应用认证流程的状态转换图。 |
| ICurrentPrincipalAccessor.Principal / SecurityPrincipals.Anonymous | 同步读取当前 principal 与 anonymous principal。 | 无。 | ClaimsPrincipal。 | 未认证状态返回 unauthenticated principal；`SecurityPrincipals.Anonymous` 不返回可污染的 singleton。 | 无 token，同步无阻塞。 | 读取返回当前 snapshot principal；已读取 principal 不随后续状态变化改变。 |
| PermissionRegistry.Add / Remove / RemoveByContribution | 注册、移除或按 contribution 撤销权限。 | PermissionDescriptor 必须有 stable name；Host 代表插件注册时必须携带 contribution id；提供的 name 和 contribution id 不得为空白。 | Add/Remove 返回 bool；RemoveByContribution 返回移除数量。 | 重复 name 返回 false；已撤销 contribution 的新注册返回 false；未找到权限或重复撤销返回 false/0。 | 同步 API 无 token。 | 读并发安全，写串行；成功变更递增 revision 并按 revision 顺序发布 Changed；观察者异常被隔离并写 `AUCSEC102`。 |
| IPermissionChecker.CheckAsync / CheckCurrentAsync | 检查 `SecurityClaimTypes.Permission` claim。 | principal、permission name；Current 变体依赖 current principal accessor。 | AuthorizationResult。 | 未注册或撤销后权限返回 Failed/PermissionNotFound；未配置 accessor、accessor/evaluator 异常或 evaluator null result 返回 Failed/EvaluatorFailed。 | 在读取 accessor 和调用 evaluator 前后观察 token；只有调用方 token 已取消才返回 Cancelled。 | 无共享 mutable result。 |
| IAuthorizationEvaluator.EvaluateAsync / EvaluatePolicyAsync | 评估 policy。 | AuthorizationRequest 包含 principal、policy、resource；policy 从捕获的 requirement snapshot 校验至少一个非 null requirement；EvaluatePolicyAsync 接收 principal、policy name、resource、contribution。 | AuthorizationResult。 | policy 缺失返回 Failed/PolicyNotFound；requirement failed 返回 Challenge、Denied 或 Forbidden；provider 未声明异常（包括调用方未取消时抛出的 OperationCanceledException）映射为 Failed/EvaluatorFailed。`AuthorizationResult.Failed` 拒绝属于 Allowed/Challenge/Denied/Forbidden/Cancelled 状态的 failure kind。 | 必须观察调用方 token；只有该 token 已请求取消时才返回 Cancelled；预取消不调用 provider。 | request 对 principal 做防御性复制；任一 authenticated identity 均视为已认证；不修改全局状态。 |
| SecurityRouteGuard.CanEnterAsync | 将 route policy 转成 guard result。 | RouteGuardContext；SecurityRouteGuardOptions 可配置 LoginRouteId 和登录 redirect 的 NavigationOptions。 | RouteGuardResult。 | 无 policy 返回 Allow；未登录默认返回 AuthenticationRequired reject，配置 LoginRouteId 后返回 Redirect；无权限返回 Forbidden reject；provider/evaluator 未声明异常返回 Failed。 | 必须观察 token；取消返回 Cancel。 | 不执行导航，只返回 Routing result；Routing 程序集不得反向引用 Security。 |
| ICommandAuthorizationSource.GetStateAsync / CheckExecutionAsync | 将 command descriptor 转成授权状态或执行前授权结果。 | CommandAuthorizationContext；descriptor provider 可按 command id 返回 policy、未授权行为和 denied message key；descriptor 缺省继承 policy contribution，显式 contribution 与 policy 冲突时拒绝构造。 | CommandAuthorizationState 或 AuthorizationResult。 | 无 descriptor 返回 Allowed；Challenge/Denied/Forbidden/Failed 让 CanExecute=false；Hide 策略让 IsVisible=false；descriptor provider 或 evaluator 未声明异常返回 Failed/EvaluatorFailed。 | 必须观察调用方 token；只有该 token 已请求取消时才返回 Cancelled。 | 构造期订阅失败先隔离 source，再回滚所有已尝试订阅并聚合/诊断回滚失败；认证状态、descriptor 和 permission registry 变化按 source revision 发布；Dispose 标记 source 后尝试全部退订、完成通知队列，并在退订失败时抛 AggregateException；重复 Dispose 幂等。 |
| IAccessTokenProvider.GetTokenAsync | 获取 access token。 | AccessTokenRequest 包含 resource、scheme 和 operation。 | AccessTokenResult，状态包含 None、Success、Required、Expired、Failed、Unavailable、Cancelled。 | token 缺失、不需要 token、刷新失败、provider 不可用和取消都有独立 status；默认 `AccountSessionManager` 在 `OfflineRestricted` 会话中对所有 resource 返回 Expired 且不读取/返回仍有效的次级凭据；null result 或未声明异常返回 Failed 并保存 Exception。 | 调用前后观察 token；只有调用方 token 已请求取消时才返回 Cancelled。 | 并发 refresh 合并或拒绝由具体 provider 明确；诊断只包含 resource、scheme、status 和 expiry hint，不包含 token。 |
| IAccountSessionStore.List/Get/Save/RemoveAsync + Get/SetLastActiveAccountAsync | 管理多个账号资料、权限快照和最后活动指针。 | account key 由 scheme、authority、tenant、subject 构成；profile 和 permission 必须属于同一 key。 | `SecurityStoreResult<T>`。 | NotFound、InvalidData、UnsupportedSchema、AccessDenied、IoFailed、Cancelled 均为稳定 status。 | IO 前后观察 token；原子替换前取消不提交目标文件。 | 默认 singleton 串行化 mutation；读取只返回完整文档；删除和活动指针清理幂等。 |
| ICredentialStore.Get/Save/Remove/RemoveAllAsync | 按账号和资源管理凭据。 | resource 和 account identity 被散列为受限路径；credential schema 必须受支持。 | `SecurityStoreResult<T>`。 | 缺失、损坏、高版本、IO 和取消返回稳定 status；不把 token 写入诊断。 | IO 和原子替换前观察 token。 | 默认 singleton 串行化 mutation；同目录临时文件替换；删除幂等。 |
| IAccountSessionManager.ListAccountsAsync | 枚举已保存账号和权限快照。 | 无敏感参数。 | 只读 `AccountRecordSnapshot` 列表结果。 | 任何损坏账号使本次枚举返回稳定 store failure，不返回半列表。 | 观察文件 IO token。 | 不改变当前会话。 |
| IAccountSessionManager.Restore/SwitchAccount/RefreshAccount/RemoveAccountAsync | 恢复、切换、强制刷新或删除账号。 | `AccountSwitchOptions` 显式控制离线受限模式和凭据 resource；刷新必须传入期望仍处于活动状态的 account key。 | `AccountSwitchResult`。 | 预期失败不抛随机异常；失败/取消保留当前 session；刷新非当前账号稳定失败；删除当前账号后 SignedOut 且不自动选择其他账号。 | 每个外部 store 调用均传递 token；活动指针提交后操作视为成功提交。 | 所有账号事务按 Host 串行；同账号重复切换幂等；显式刷新成功只发布一个新 revision；认证和 session 观察者只看到完整同 revision 快照。 |
| SecurityServiceCollectionExtensions.AddSecurity | 注册 Security 默认服务。 | 可选 `SecurityPersistenceOptions`；默认 root 为 `IApplicationContext.AppDataPath/security`。 | 原 `IServiceCollection`。 | 路径非法在 options/store 构造边界拒绝。 | 注册阶段无 IO。 | 默认注册 file stores、singleton account manager，并把 manager 作为默认 `IAccessTokenProvider`；调用方预先注册的合同实现不被覆盖。 |

`InMemoryAuthorizationPolicyProvider`、`InMemoryCommandAuthorizationDescriptorProvider` 和 `InMemoryRouteAuthorizationPolicyProvider` 的 `RemoveByContribution` 会永久标记当前实例中的 contribution 为 revoked；同一 contribution 后续注册返回 false。Command descriptor 和 Route policy 的批量撤销分别发布全量 Command 刷新或立即影响下一次 Route guard 查询。

## Multi-Account API Contracts

以下类型属于已完成的 `AUC-SECURITY-008/009` 公开合同；后续改变成员、默认行为、schema、identity 规范化、失败状态或诊断语义必须同步 compatibility 和迁移说明。

| Type | Purpose | Required Behavior |
| --- | --- | --- |
| `SecurityAccountKey` | 稳定标识一个持久化账号。 | 由 scheme、authority、可选 tenant id 和 subject id 构成；不可使用显示名作为 identity；支持稳定相等性和磁盘 key 编码。 |
| `AccountProfileSnapshot` | 保存可展示的非敏感账号资料。 | immutable；不得包含 token、refresh token、密码或平台保护密钥。 |
| `PersistedPermissionSnapshot` | 保存当前账号的权限缓存。 | immutable；包含 permission set、revision、issued-at、expires-at 和 schema version；不得被解释为服务器授权结果。 |
| `IAccountSessionStore` | 持久化账号资料、权限快照和最后活动账号指针。 | 按 account key 隔离；使用版本化原子替换；损坏或高版本格式返回稳定失败；取消后不留部分提交。 |
| `ICredentialStore` | 按账号和资源把认证凭据持久化到声明的账号凭据文件。 | 使用受限路径、schema version 和原子替换；凭据不得进入普通配置、State、日志或诊断；删除必须幂等。 |
| `IAccountSessionManager` | 枚举已保存账号并恢复、切换、刷新或删除账号。 | 一个 Host 只发布一个活动账号；事务串行且原子；失败/取消回滚；删除活动账号后进入 Anonymous/SignedOut。 |
| `AccountSessionSnapshot` | 发布当前活动账号的完整只读会话。 | 同一 revision 中 principal、credential context、permission snapshot 和 online/offline mode 必须属于同一账号。 |
| `AccountSwitchResult` / `AccountSwitchResultStatus` | 表达恢复或切换结果。 | 明确区分 Success、NotFound、CredentialUnavailable、PermissionUnavailable、InvalidData、Cancelled 和 Failed，不以随机异常表达预期失败。 |

方法合同摘要：

| Method | Purpose | Failure / Cancellation | Concurrency / Atomicity |
| --- | --- | --- | --- |
| `IAccountSessionStore.List/Get/Save/RemoveAsync` | 管理多个账号资料、权限快照和活动指针。 | IO、schema、损坏和取消返回稳定结果；不得泄漏凭据。 | 同账号 mutation 串行；读者只观察完整旧版本或完整新版本。 |
| `ICredentialStore.Get/Save/RemoveAsync` | 管理账号凭据文件。 | 路径、IO、schema、损坏或取消返回稳定结果；不得写入声明目录之外。 | 同一 credential key 的 mutation 串行；写入原子替换；删除幂等。 |
| `IAccountSessionManager.RestoreAsync` | 启动时恢复最后活动账号。 | 无账号进入 Anonymous；缓存损坏或凭据不可用返回明确失败。 | 只发布一次完整恢复结果。 |
| `IAccountSessionManager.SwitchAccountAsync` | 切换到指定已保存账号。 | 任一步失败或取消均保留原活动账号。 | 切换事务串行；成功只递增一次 session/authentication revision。 |
| `IAccountSessionManager.RefreshAccountAsync` | 在应用完成远端认证/权限刷新并原子保存账号数据后，强制重载当前账号。 | account key 必须仍是当前账号；失败、取消、账号已切换或缓存无效时保留原 session。 | 与切换事务共用串行门；成功只发布一次完整的新 session/authentication revision，不修改最后活动账号指针。 |
| `IAccountSessionManager.RemoveAccountAsync` | 删除指定账号及其全部本地数据。 | 部分删除必须报告且可重试；不得自动激活其他账号。 | 删除当前账号时先阻止新账号绑定操作，再进入 Anonymous/SignedOut。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AccessTokenRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AccessTokenResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AccessTokenResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AccountCredentialContext` | 支持类型 | 只公开 resource、scheme、expiry 和是否存在凭据；不得新增 token 内容。 |
| `AccountCredentialSnapshot` | 关键 contract | token 仅允许在 credential store 边界使用；可观察文本必须脱敏。 |
| `AccountProfileSnapshot` | 支持类型 | 只能包含非敏感展示资料和 schema version。 |
| `AccountRecordSnapshot` | 支持类型 | profile 和 permission 必须属于同一账号。 |
| `AccountSessionChangedEventArgs` | 支持类型 | previous/current/operation id 语义不得无迁移改变。 |
| `AccountSessionManager` | 关键 contract | 单活动账号、事务串行、失败保持旧 session 和有序锁外通知属于兼容承诺。 |
| `AccountSessionMode` | 支持类型 | Anonymous、Online、OfflineRestricted 枚举值属于兼容面。 |
| `AccountSessionSnapshot` | 关键 contract | 同 revision 内 profile、principal、permission 和 credential context 必须一致且只读。 |
| `AccountSwitchOptions` | 支持类型 | `AllowOffline=false` 和默认 credential resource 语义属于兼容面。 |
| `AccountSwitchResult` | 支持类型 | status、session、operation id、failure stage 和异常语义属于兼容面。 |
| `AccountSwitchResultStatus` | 支持类型 | Success、NotFound、CredentialUnavailable、PermissionUnavailable、InvalidData、Cancelled、Failed 不得复用语义。 |
| `AuthenticationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateSnapshot` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateStore` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationEvaluator` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequirement` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequirementKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationChangeReason` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandUnauthorizedBehavior` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DelegateAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAccountSessionManager` | 关键 contract | 列表、恢复、切换、刷新、删除和 token provider 行为属于兼容面。 |
| `IAccountSessionStore` | 关键 contract | 账号/权限/活动指针的稳定结果和原子文件语义属于兼容面。 |
| `IAuthenticationStateProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAuthorizationEvaluator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICommandAuthorizationDescriptorProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICommandAuthorizationSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICurrentPrincipalAccessor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICredentialStore` | 关键 contract | 按账号和 resource 隔离、稳定结果、原子写入及幂等删除属于兼容面。 |
| `IPermissionChecker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPermissionRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouteAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryCommandAuthorizationDescriptorProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryRouteAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `FileAccountSessionStore` | 默认 Provider | 默认目录布局、schema 和稳定失败需兼容或提供迁移。 |
| `FileCredentialStore` | 默认 Provider | 凭据目录布局、schema、原子替换和脱敏规则需兼容或提供迁移。 |
| `PermissionChecker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionRegistry` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionRegistryChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PersistedPermissionSnapshot` | 关键 contract | permission set、revision、issued/expiry 和 schema 语义属于兼容面。 |
| `SecurityAccountKey` | 关键 contract | identity 字段、规范化、相等性和存储 key 编码不得无迁移改变。 |
| `SecurityFailureKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityClaimTypes` | 关键 contract | Permission claim type 不得无迁移地改变。 |
| `SecurityDiagnosticIds` | 关键 contract | code 和语义不得复用；新增诊断必须同步 diagnostics、testing 和 compatibility。 |
| `SecurityPersistenceOptions` | 支持类型 | root 和默认 credential resource 的默认值与解析规则属于兼容面。 |
| `SecurityPrincipals` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityRouteGuard` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityRouteGuardOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityRouteGuardResultCodes` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityStoreResult<T>` | 支持类型 | 预期文件失败通过稳定 status 返回，不改为随机异常。 |
| `SecurityStoreResultStatus` | 支持类型 | Success、NotFound、InvalidData、UnsupportedSchema、AccessDenied、IoFailed、Cancelled 不得复用语义。 |
| `UnavailableAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
