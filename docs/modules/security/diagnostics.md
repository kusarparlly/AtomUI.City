# AtomUI.City.Security Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Code | Severity | Meaning | Required Context |
| --- | --- | --- | --- |
| `AUCSEC001` | Info | 认证状态完成一次有效变更。 | `previousState`, `currentState`, `reason`, `revision`, `scheme`, `principalKind` |
| `AUCSEC002` | Error | 认证状态观察者抛出异常。 | `eventType`, `observerType`, `exceptionType` |
| `AUCSEC101` | Info | 权限注册表完成一次有效变更。 | `operation`, `permissionName`, `contributionId`, `revision` |
| `AUCSEC102` | Error | 权限注册表观察者抛出异常。 | `eventType`, `observerType`, `exceptionType` |
| `AUCSEC200` | Warning | 授权评估得到 Challenge、Denied 或 Forbidden。 | `policyName`, `requirementKind`, `requirementName`, `resourceName`, `resultStatus`, `failureKind`, `principalIdHash` |
| `AUCSEC201` | Error | 授权评估器或 policy provider 失败。 | `policyName`, `resourceName`, `resultStatus`, `failureKind`, `exceptionType` |
| `AUCSEC300` | Info/Warning | Route 授权完成且未发生框架失败。 | `routeId`, `policyName`, `resultStatus`, `resultCode` |
| `AUCSEC301` | Error | Route 授权 provider/evaluator 失败。 | `routeId`, `policyName`, `resultStatus`, `exceptionType` |
| `AUCSEC400` | Info | Command 授权输入发生变化。 | `commandId`, `changeReason`, `revision` |
| `AUCSEC401` | Error | Command descriptor provider/evaluator 或 source 订阅生命周期失败。 | 评估失败：`commandId`, `resourceName`, `contributionId`, `exceptionType`；生命周期失败：`operation`, `exceptionType`。 |
| `AUCSEC402` | Error | Command 授权观察者抛出异常。 | `eventType`, `observerType`, `exceptionType` |
| `AUCSEC403` | Info/Warning/Error | Command 授权评估完成。 | `commandId`, `policyName`, `resultStatus`, `failureKind`, `contributionId` |
| `AUCSEC500` | Info/Error | Access token 请求完成；delegate 返回 Failed 时为 Error。 | `resourceName`, `scheme`, `operationName`, `status`, `expiresAt` |
| `AUCSEC501` | Error | Access token provider 抛出未声明异常或返回非法结果。 | `resourceName`, `scheme`, `operationName`, `status`, `exceptionType` |
| `AUCSEC600` | Info | 账号资料、权限或活动指针文件操作成功。 | `operation`, `storeKind`, `accountIdHash`, `schemaVersion`, `resultStatus` |
| `AUCSEC601` | Error | 账号资料、权限或活动指针文件损坏、schema 不兼容或 IO 失败。 | `operation`, `storeKind`, `accountIdHash`, `schemaVersion`, `resultStatus`, `exceptionType` |
| `AUCSEC610` | Info | 凭据文件操作成功。 | `operation`, `storeKind`, `accountIdHash`, `schemaVersion`, `resultStatus` |
| `AUCSEC611` | Error | 凭据文件损坏、schema 不兼容或 IO 失败。 | `operation`, `storeKind`, `accountIdHash`, `schemaVersion`, `resultStatus`, `exceptionType` |
| `AUCSEC700` | Info | 账号恢复、切换或删除成功完成。 | `operation`, `operationId`, `previousAccountIdHash`, `targetAccountIdHash`, `mode`, `resultStatus` |
| `AUCSEC701` | Error | 账号恢复、切换或删除失败。 | `operation`, `operationId`, `previousAccountIdHash`, `targetAccountIdHash`, `mode`, `resultStatus`, `failureStage`, `exceptionType` |
| `AUCSEC702` | Error | 账号 session 观察者抛出异常。 | `eventType`, `observerType`, `exceptionType` |

这些 code 由 `SecurityDiagnosticIds` 公开并写入 Core 的 `IHostDiagnostics`。诊断写入失败不能改变认证状态、授权结果、Route/Command 结果或 token 结果。

## 产品级必须诊断的失败

- 多账号文件损坏、schema 过高和原子提交失败使用 `AUCSEC601/AUCSEC611`。
- 账号切换失败阶段和原 session 保留结果使用 `AUCSEC701`。

## 上下文字段

只记录定位所需的最小字段。主体 identity 使用截断 SHA-256 `principalIdHash` 做假名化关联；它不是匿名化，对低熵或可枚举 identity 仍可能被字典关联。禁止记录 access token、refresh token、密码、完整 credential、完整 principal 或用户 claims。当前字段以诊断码表为准，规划功能不得复用现有码。

## 诊断缺口处理

- 如果已完成 Feature 的关键失败没有对应 Result 或诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Security.Tests` 必须断言诊断 code、关键 context、观察者异常隔离，以及 token/主体明文不进入诊断。
