# 应用服务 API

本页汇总 Data、Security 与 Localization。

## Data

完整合同：[Data API Contracts](../modules/data/api-contracts.md)

使用 `DataModule` 接入 City Host，或在独立 DI 场景调用 `services.AddData()`。

| API family | 职责 |
| --- | --- |
| Request/Response | 描述请求、响应、错误、取消和进度 |
| Pipeline | handler、middleware 和 capability 验证 |
| Transport | HTTP、WebSocket、IPC 等请求响应传输抽象 |
| Connection | owner 化连接、启动、停止和状态观测 |
| Authentication | credential provider、challenge 和 retry 边界 |
| Resilience | retry、timeout、circuit breaker、fallback、rate limit |
| Cache | canonical identity、TTL 和批量失效 |
| Client descriptors | generated client descriptor 与 AOT catalog |

Data 结果通过明确状态表达成功、失败和取消。重试不得跨越不安全操作边界；流式 payload 必须保持有界内存，并把取消传递到底层 transport。

## Security

完整合同：[Security API Contracts](../modules/security/api-contracts.md)

通过 `services.AddSecurity()` 注册安全基础设施。

| API family | 主要类型 | 用途 |
| --- | --- | --- |
| Identity | principal/account/session contracts | 表达当前身份与账号边界 |
| Authorization | `IAuthorizationEvaluator`、`AuthorizationRequest`、`AuthorizationPolicy` | 评估权限和策略 |
| Requirement | `AuthorizationRequirement` 等 requirement 模型 | 组合策略条件 |
| Persistence | `SecurityPersistenceOptions`、store result | 持久化账号相关安全状态 |
| Routing | `SecurityRouteGuard`、`SecurityRouteGuardOptions` | 将授权结果接入路由 guard |

授权失败应返回稳定结果和诊断，而不是依赖 UI 弹窗或字符串异常判断。敏感状态不得进入普通 diagnostics context。

## Localization

完整合同：[Localization API Contracts](../modules/localization/api-contracts.md)

通过 `services.AddLocalization(...)` 注册服务，主要入口是 `ILocalizationService`。

| API family | 用途 |
| --- | --- |
| Culture | 切换当前 culture 并发布只读 `CultureState` 快照 |
| Lookup | 获取 key 对应的 localized value 与 fallback 信息 |
| Package | 注册 Host 或 owner 化语言包 |
| Scope | 为窗口、导航或业务上下文选择 culture |
| Diagnostics | 缺失 key、fallback、加载和撤销诊断 |

`CultureState` 通过公开的 observable state 读取，不依赖 `LocalizationService` 的具体实现成员：

```csharp
var localization = services.GetRequiredService<ILocalizationService>();
await localization.SetCultureAsync("zh-CN", cancellationToken);

var state = localization.CultureState.Value;
var title = await localization.GetStringAsync("Orders.Title", cancellationToken);
```

语言包撤销会发布新快照；旧快照仍是可读取的历史值。应用不得修改返回的 package、fallback 或资源集合。
