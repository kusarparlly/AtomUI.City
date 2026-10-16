# 0001 context 上下文参数 + 独立 CancellationToken 参数的双设计

- 日期：2026-10-14
- 范围：`AtomUI.City.Core`（Scope/Context）、`AtomUI.City.Mvvm`（激活/命令）、`AtomUI.City.EventBus`、`AtomUI.City.Data`
- 结论：语义正确，人体工学欠账。综合 **8/10**（语义 9，人体工学 6.5）。

## 1. 背景（核实过的事实，带出处）

- `LifecycleScope` 是同一类 + `Kind` 区分（`src/AtomUI.City.Core/Lifecycle/LifecycleScopeKind.cs:6-48`），父子靠 `Parent + _children`（`LifecycleScope.cs:11,50`）。
- 每个 Scope 私藏一颗 CTS，对外只露 `Token`（`LifecycleScope.cs:10,83`）；孩子出生即链接父 token（`LifecycleScope.cs:141`），父红子必红，反之不然。
- token 随 Scope 生（`Build()` 生 `HostScope`，`Start()` 生 `ApplicationScope`，导航/激活/点击逐层生）；context 随调用现包现扔（`LifecycleContext.cs:25-42`）。
- 每个 context 只带**一个** token 属性（`LifecycleContext` / `EventContext<T>.cs:107` / `DataRequestContext.cs:69`）；`ActivationContext` 无 token 属性，经由 `.Scope.CancellationToken` 取。
- “两个 token”只出现在**方法签名**上（如 `OnActivatedAsync(context, ct)`），且是两个不同的值：Scope 的命 vs 本次调用的命。`ViewModelBase.cs:132-142` 两处都要查即为实证。
- 命令递给开发者的是**新操作自己的** token，不是传进来的那个（`CommandFactory.cs:370-378`，`OperationScope.cs:11,26-31` 自建 CTS + 登记外部）。
- `CancellationToken` 是结构体，无字符串内容，只能问红没红；追踪靠 `OperationId / CorrelationId / EventId`（`EventId/OperationId` 每次新建，`CorrelationId` 同因果链共用）。

## 2. 好的地方

1. **区分是真实需求**：桌面 Scope 活几小时、调用活几秒，“页面关了”与“调用被顶掉”是两种死法，必须两个 token。ASP.NET Core 单 `RequestAborted` 够用只因请求 Scope ≈ 调用。
2. **符合 .NET TAP 惯例**：下游 `HttpClient/EF/Task.Delay` 只认 `ct` 参数，独立参数透传零摩擦；context 管记账，token 管红绿。
3. **基础设施以身作则**：全仓 30 处 `CreateLinkedTokenSource`（Scope 建孩子、命令执行、Data 管线、EventBus 派发），取消纪律统一。

## 3. 欠的地方（带证据）

1. **双 token 并列是 footgun**：只查一个能编译能跑，漏的那个只在 corner case（如导航顶掉激活）爆，编译器不拦。
2. **各门形状不统一**：EventBus handler 只有 context，命令只有 `ct`，激活/模块是双份；`ActivationContext` 取 token 还要绕 `.Scope`。
3. **合成负担在开发者侧**：想要“两个都盯”得自写 `CreateLinkedTokenSource`，框架未给应用层现成 helper。

## 4. 补强建议（三选一即可到 9 分）

- `context.GetEffectiveToken()` 类扩展，一键拿 Scope+调用合成后的 token；
- analyzer 加“双 token 入口必须双查”规则（`AUCGEN` 系列）；
- 文档铁律化：“入口双查、往下传调用那个”。

满分保留 1 分：待大生产验证（全部门形状统一、千万级调用无泄漏、第三方插件零误用）后再议。

## 5. 是否提请 ADR

- 提请。建议 ADR 标题：《双 token 入口规范（查两个、传调用那个）》，将第 4 节第一项或第三项立为框架约束。
