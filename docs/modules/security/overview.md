# AtomUI.City.Security

文档等级：Level 2
成熟度：Implemented
执行边界：Host runtime security service
程序集：`AtomUI.City.Security`
源码：`src/AtomUI.City.Security`
测试：`tests/AtomUI.City.Security.Tests`

## 模块定位

认证状态、权限、授权策略、路由守卫、命令授权和数据访问凭据入口。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- Security 不实现登录 UI。
- 认证状态以 immutable snapshot 发布。
- 授权评估不操作 UI 或导航。
- access token 失败返回 AccessTokenResult。
- 认证 snapshot 必须防御输入与输出 principal mutation，并移除 `BootstrapContext`。
- Security 诊断不得包含 token、refresh token、完整 principal 或用户 claims。

## 模块目标

- 集中表达当前用户和认证状态。
- 统一权限、策略、路由和命令授权。
- 通过 Core `IHostDiagnostics` 提供稳定诊断码，并隔离事件观察者失败。
- 提供多账号文件持久化与全局活动账号切换（`AUC-SECURITY-008/009`）。

## 明确非目标

- 不内置 OAuth/OIDC 登录流程。
- 不存储密码。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：当前通过 DI、扩展方法和显式 descriptor/provider 使用模块能力；attribute、manifest、CLI 或模板必须以对应 Feature 状态为准。
- 插件开发者：当前由 Host 代为注册并按 contribution id 撤销；完整 manifest、capability 和 lease 接入属于未来 PluginSystem Feature。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

AtomUI.City.Security 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 与 PluginSystem 的关系

当前 Security 只提供 contribution id 撤销/tombstone 基线。插件 manifest、capability grant、ContributionLease 和 owner 编排属于 Future Integration，不能从本文档推断为已有能力。

## 与 Testing 的关系

`tests/AtomUI.City.Security.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| [architecture.md](architecture.md) | 核心不变量、对象模型、状态机、流程、失败矩阵、性能边界。 |
| [features.md](features.md) | Feature ID、实现合同、public contract、失败行为和验收标准。 |
| [api-contracts.md](api-contracts.md) | API family、关键方法、参数/返回/异常/取消/并发/Dispose 后行为。 |
| [lifecycle.md](lifecycle.md) | 模块特有生命周期、Host shutdown、插件动态变更和失败处理。 |
| [threading.md](threading.md) | 线程边界、UI dispatcher、后台任务、并发冲突和死锁规避。 |
| [diagnostics.md](diagnostics.md) | 现有诊断码、产品级目标诊断、上下文字段和测试断言。 |
| [testing.md](testing.md) | 具体测试矩阵、必须断言的行为、测试类型和缺口处理。 |
| [compatibility.md](compatibility.md) | public API、配置、manifest、snapshot、generated output、CLI envelope 和包布局兼容。 |
| [integration.md](integration.md) | 跨模块依赖方向、生命周期、线程和失败行为。 |
| [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) | Feature 到现有基线、缺口、必补测试和实现工作的追踪。 |

## 当前成熟度状态

Implemented

`AUC-SECURITY-001~009` 已有实现与合同测试。多账号能力提供默认跨平台文件 Provider、活动账号恢复/切换、离线受限模式和稳定诊断；默认凭据文件不承诺抵御同一操作系统用户权限下的本地读取。Security generator、Plugin capability/ContributionLease、系统安全保险库 Provider 和 Testing helper 不是当前已实现能力。单个功能点状态以 [features.md](features.md) 和 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 为准。
