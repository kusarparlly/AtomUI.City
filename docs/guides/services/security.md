# 认证与授权

Security 模块区分“当前是谁”和“是否允许做某件事”。身份是只读快照；授权通过 permission、requirement 和 policy 得出显式结果。

## 注册安全基础设施

```csharp
builder.ConfigureServices(services =>
    services.AddSecurity(new SecurityPersistenceOptions(
        accountDirectory,
        defaultCredentialResource: "contoso-api")));
```

应用再注册自己的 credential provider、账号存储策略和服务端权限刷新逻辑。凭据不进入普通配置、State 或 diagnostics。

## 认证状态不是一个布尔值

应用至少要区分 anonymous、authenticating、authenticated、refreshing、offline-restricted 和 failed 等业务状态。切换账号是一个事务：准备新账号、验证凭据和权限、提交新 snapshot，失败时保留可解释的原状态。

## 用 policy 授权

```csharp
var result = await authorization.EvaluatePolicyAsync(
    principal,
    "contoso.orders.refund",
    cancellationToken: cancellationToken);

if (!result.Succeeded)
{
    return ShowForbidden(result);
}
```

业务服务必须再次授权；按钮禁用和 route guard 只是更早的用户体验反馈。不要根据角色名称在各处写字符串分支，应把 permission 组合进稳定 policy。

## 离线与过期

- 离线授权只能使用仍在允许期限内的持久化 permission snapshot；
- 过期权限不能因为“上次允许”而继续放行；
- 恢复在线后先刷新凭据和权限，再提升会话能力；
- 切换账号时取消或隔离旧账号的 in-flight 请求；
- token 刷新和 permission 刷新要防止并发重复执行。

## 与 Routing 和 Data 集成

Route guard 可以在进入页面前调用同一授权 evaluator；Data credential provider 从当前 account session 取得凭据。二者共享安全状态，但 Routing、Data 不应各自维护第二份“当前用户”。

完整的多账号、离线受限、刷新和持久化场景见 [Desktop Dogfood Security](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Security/)。
