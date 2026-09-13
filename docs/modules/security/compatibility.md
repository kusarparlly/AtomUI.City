# AtomUI.City.Security Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Security 不实现登录 UI。
- 认证状态以 immutable snapshot 发布。
- 授权评估不操作 UI 或导航。
- access token 失败返回 AccessTokenResult。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AuthenticationStateStore` 发布 cloned immutable snapshot、对每次 principal 读取返回防御性副本、保留并隔离 Actor chain 且不保留任意层级 `ClaimsIdentity.BootstrapContext`、等价重复设置幂等、Refreshing/Expired 原子继承当前 token hint、`SetFailed(string)` 与 SignedOut 清除 principal/token hint、StateChanged 按 revision 发布 previous/current snapshot 的行为进入 1.0 兼容承诺；Store 不强制认证流程转换图。
- `ICurrentPrincipalAccessor.Principal` 返回当前 snapshot principal 的防御性副本、已读取 principal 保持稳定且不能反向修改 store、`SecurityPrincipals.Anonymous` 每次返回独立 unauthenticated principal 的行为进入 1.0 兼容承诺。
- `PermissionRegistry` 使用大小写敏感权限名、成功变更递增 revision、重复注册返回 false、`RemoveByContribution` 撤销已有权限并阻止同一 contribution id 后续注册、重复撤销返回 0 的行为进入 1.0 兼容承诺。
- `PermissionChecker` 使用 `SecurityClaimTypes.Permission` claim type；对未注册或已撤销权限返回 `AuthorizationResultStatus.Failed` 和 `SecurityFailureKind.PermissionNotFound`，accessor/evaluator 异常返回 `EvaluatorFailed`，不执行 UI、导航或权限外副作用。
- `AuthorizationPolicy` 先捕获 requirement snapshot，再拒绝空集合或 null requirement，避免可变输入产生 fail-open；`IAuthorizationEvaluator.EvaluateAsync` 顺序评估 requirement，并把任一 authenticated identity 视为已认证；`EvaluatePolicyAsync` 通过 `IAuthorizationPolicyProvider` 读取 named policy，policy 缺失返回 `PolicyNotFound`，provider 未声明异常返回 `EvaluatorFailed`，预取消不调用 provider。
- `SecurityRouteGuard` 无 policy 时允许导航，Challenge 默认返回 `authentication-required` reject，配置 `SecurityRouteGuardOptions.LoginRouteId` 后返回 redirect；Forbidden 返回 `authorization-forbidden` reject，provider/evaluator 异常返回 `authorization-failed` failed result。
- `CommandAuthorizationDescriptor` 缺省继承 policy contribution，显式 contribution 与 policy 冲突时拒绝构造；`CommandAuthorizationSource` 无 descriptor 时允许命令，未授权时返回 disable/hide 状态；权限 registry、descriptor 和 authentication state 变化发布 `AuthorizationChanged`；provider/evaluator 异常返回 `Failed/EvaluatorFailed`；构造订阅失败先隔离 source 再回滚并聚合/诊断失败，Dispose 尝试全部退订、聚合失败且重复调用幂等。
- `AccessTokenResultStatus` 区分 `Failed` 与 `Unavailable`；`DelegateAccessTokenProvider` 对 null result 或未声明异常返回 `Failed` 并保留 exception，调用前后观察调用方 token；Data 凭据桥把 Failed token 映射为 credential unavailable。
- `AuthorizationResult.Forbidden` 使用 `SecurityFailureKind.Forbidden`；`AuthorizationResult.Denied` 使用 `SecurityFailureKind.RequirementFailed`。`Failed` 不接受 `None`、未知值或属于 AuthenticationRequired/Forbidden/RequirementFailed/Cancelled 状态的 failure kind。
- Authentication、Permission 和 Command 事件按各自 revision 有序发布；单个观察者异常不会回滚状态、阻断其他观察者或冒泡到 mutation 调用方。
- `SecurityDiagnosticIds` 中由 [diagnostics.md](diagnostics.md) 逐项登记的 code 与语义属于兼容面；编号区间中的空号不构成已发布诊断。诊断不得包含 token、refresh token、密码、完整 principal 或用户 claims。
- 当前内存 Policy、Command descriptor 和 Route policy provider 按 contribution 撤销后，拒绝同一 contribution 在该实例中重新注册。

## Multi-Account Compatibility

以下规则已随 `AUC-SECURITY-008/009` 进入兼容性承诺。

- `SecurityAccountKey` 的 scheme、authority、tenant id、subject id 组成和规范化规则属于持久化 identity：scheme 与绝对 URI 的 scheme/host 规范化为小写，非 URI authority、tenant 和 subject 保持大小写；发布后不得无迁移地改变。
- 账号资料与权限快照必须携带 schema version；reader 必须拒绝无法理解的高版本，升级必须提供原子迁移或保留旧数据。
- Token 和 refresh token 只允许进入声明的账号凭据文件，不得复制到普通配置、State、日志或诊断；凭据文件路径、schema version 和原子替换语义属于稳定合同。
- `IAccountSessionManager` 采用一个 Host 一个全局活动账号；切换或刷新失败/取消保留原 session，成功只发布一次完整 revision，不得改变为部分提交。普通同账号切换保持幂等；`RefreshAccountAsync` 是远端认证/权限缓存更新后强制重载当前账号的唯一显式入口，并以传入 account key 防止账号切换竞态。
- 离线权限快照只用于 UI 和客户端预检查；其过期语义、受限模式和重新联网后由服务器结果覆盖的行为属于兼容性合同。
- 删除当前账号进入 Anonymous/SignedOut 且不自动选择其他账号；删除非当前账号不得改变 active session。
- 诊断、迁移和备份输出必须使用脱敏账号 identity，任何凭据字段进入可观察文本都属于安全缺陷而非兼容行为。
- `AddSecurity()` 默认注册文件 store、`IAccountSessionManager`，并把该 manager 作为默认 `IAccessTokenProvider`；应用可在 DI 中替换 store 或 token provider。
- 默认 `AccountSessionManager` 只允许 Online session 签发 token；`OfflineRestricted` session 对所有 resource 稳定返回 Expired，不能因某个次级 resource 的本地凭据尚未到期而绕过离线限制。
- 当前文件 schema version 为 `1`；账号目录和资源文件名由稳定 SHA-256 编码产生，改变 identity 规范化或编码必须提供迁移。

当前版本的默认文件 Provider 不承诺抵御同一操作系统用户权限下的本地读取。系统安全保险库属于后续版本增强项；未来 Provider 必须继续实现 `ICredentialStore`，并在成功迁移后删除旧凭据文件，不能让同一凭据长期保留两份持久化副本。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
