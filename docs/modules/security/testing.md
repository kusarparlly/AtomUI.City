# AtomUI.City.Security Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程和当前已实现的订阅/contribution 行为必须有专项测试；插件、连接、dispatcher、source generator、build 和 template 仅在相应 Feature 适用时纳入门禁。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Security 不实现登录 UI，只定义状态、权限、授权和 token contract。 | 测试必须断言无 Presentation 依赖。 |
| 认证状态以 immutable snapshot 发布，跨线程读取必须一致。 | 必须覆盖输入/输出 principal mutation、BootstrapContext 清除、并发 revision 顺序和观察者异常隔离。 |
| 授权评估不操作 UI、不执行导航、不访问 VisualTree。 | Route guard 测试只断言结果，不触发导航。 |
| Route、Command、Data 只通过 Security public contract 集成。 | 集成测试必须断言边界。 |
| 多账号凭据只能进入声明的账号凭据文件，不得进入普通配置、State、日志或诊断。 | 文件 Provider 合同测试必须验证路径隔离并扫描捕获的可观察输出。 |
| 账号切换只能原子发布一个全局活动账号。 | 故障注入和并发测试不得观察到跨账号混合状态。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-SECURITY-001 | Contract; Concurrency | AuthenticationStateTests | 断言 snapshot 双向不可变、Actor chain 保留/隔离、所有层级 BootstrapContext 清除、Refreshing/Expired 原子继承 token hint、状态切换、revision 有序通知、观察者隔离、重复设置和 logout。 | 半认证状态、未认证 principal、观察者异常、并发提交。 | Completed |
| AUC-SECURITY-002 | Contract | AuthenticationStateTests; AuthorizationEvaluatorTests | 断言 authenticated、anonymous、多个 identity、claims 稳定读取和 mutation 隔离。 | principal 缺失、unauthenticated identity 在前、外部篡改。 | Completed |
| AUC-SECURITY-003 | Contract; Concurrency | PermissionRegistryTests; PermissionCheckerTests; SecurityDiagnosticTests | 断言注册、重复、未注册、`SecurityClaimTypes.Permission`、contribution 撤销、revision 有序通知、观察者隔离和 checker result。 | 未注册权限、accessor 缺失/异常、重复权限、contribution revoked 后重新注册、观察者和诊断 sink 异常。 | Completed |
| AUC-SECURITY-004 | Contract | AuthorizationEvaluatorTests; AuthorizationPolicyTests; AuthorizationResultTests; AuthorizationPolicyProviderTests | 断言成功、Denied/Forbidden 区分、输入 snapshot 后校验、取消、多 requirement、contribution tombstone 和诊断。 | policy 缺失、可变/空/null requirement、非法 Failed kind、provider 异常、非调用方取消异常、预取消。 | Completed |
| AUC-SECURITY-005 | Contract | RouteAuthorizationGuardTests | 断言 allow、deny、redirect login、取消、contribution 撤销、诊断和 Routing 无 Security 反向依赖。 | 无权限、需要登录、provider 异常、非调用方取消异常。 | Completed |
| AUC-SECURITY-006 | Contract; Concurrency | CommandAuthorizationSourceTests | 断言状态变化、禁用/隐藏、失败构造隔离与订阅回滚、Dispose 全量退订与失败聚合、descriptor/policy contribution 继承与冲突拒绝、contribution 撤销、观察者隔离和诊断。 | descriptor 缺失、contribution 冲突、订阅/回滚/退订失败、权限撤销、provider 异常、非调用方取消异常、重复 Dispose。 | Completed |
| AUC-SECURITY-007 | Contract; Integration | SecurityRegistrationTests; AccessTokenCredentialProviderTests | 断言成功、null result、异常、不可用、调用前后取消、诊断脱敏、DI 默认 provider 和 Data 前置条件。 | token 缺失、provider 不可用、非调用方取消异常、token 泄漏。 | Completed |
| AUC-SECURITY-008 | Contract; RuntimeLifecycle | AccountPersistenceTests; FileCredentialStoreTests | 断言多账号 round-trip、进程重启恢复、账号隔离、路径约束、原子写入、删除无残留，以及凭据不进入普通配置、State、日志或诊断。 | 文件损坏、高版本 schema、非法路径、IO 失败、凭据缺失、写入取消和部分删除。 | Completed |
| AUC-SECURITY-009 | Contract; Integration | AccountSessionManagerTests; AccountSwitchIntegrationTests | 断言恢复、成功/重复/并发切换、显式刷新、失败/取消回滚、单次 revision/通知、离线受限模式和账号删除。 | 账号不存在、凭据/权限加载失败、过期权限、刷新非当前账号、刷新预取消、离线 session 的有效次级 resource token、旧账号在途 token 读取失效和删除活动账号。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
