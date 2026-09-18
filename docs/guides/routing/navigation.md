# 定义路由并导航

Routing 把“用户要去哪里”表达为可验证、可生成、可取消的导航事务。路由匹配成功不等于 UI 已经呈现；Presentation 负责把提交后的目标映射到 View 和 outlet。

## 1. 定义生成路由

```csharp
public readonly record struct OrderRouteParameters(Guid Id);

[RouteMap]
public static partial class AppRoutes
{
    [LayoutRoute(typeof(ShellViewModel), Id = "app.shell", Outlet = "primary")]
    public static partial RouteReference Shell();

    [RouteGroup("orders", Id = "app.orders", Parent = nameof(Shell))]
    public static partial RouteReference OrdersGroup();

    [Route(
        "{id:guid}",
        typeof(OrderDetailsViewModel),
        Id = "app.orders.details",
        Parent = nameof(OrdersGroup))]
    public static partial RouteReference<OrderRouteParameters> OrderDetails();
}
```

使用稳定、带命名空间的 `Id`。参数约束让非法地址在匹配阶段失败，而不是进入 ViewModel 后再猜测。

## 2. 注册生成清单

```csharp
builder.ConfigureServices(services =>
    services.AddRouting(
        AtomUI.City.Generated.GeneratedRoutingRouteManifest.CreateDescriptors()));

builder.UseModule<RoutingModule>();
```

生成清单避免运行时扫描，也使 Native AOT 路径可预测。不要手工复制生成 descriptor；声明和生成结果应保持单一来源。

## 3. 导航并检查结果

```csharp
var result = await router.NavigateAsync(
    AppRoutes.OrderDetails(),
    new OrderRouteParameters(orderId),
    new NavigationOptions
    {
        OutletName = "primary",
        ConcurrencyPolicy = NavigationConcurrencyPolicy.CancelPrevious,
        HistoryBehavior = NavigationHistoryBehavior.Record,
        Timeout = TimeSpan.FromSeconds(10),
    },
    cancellationToken);

if (result.Status != NavigationResultStatus.Success)
{
    HandleNavigationFailure(result);
}
```

也可以使用 `NavigateByPathAsync` 处理文本路径，或使用 `BackAsync`、`ForwardAsync` 操作当前 navigation scope 的 journal。应用代码必须检查 `NavigationResult`，不能把“方法没有抛异常”当作导航成功。

## 4. 理解事务顺序

一次导航大致经过：

```text
匹配目标
-> leave guards（当前叶到根）
-> enter guards（目标根到叶）
-> resolvers（目标根到叶）
-> middleware
-> 原子提交 snapshot 与 journal
-> Presentation 呈现
```

NotFound、Rejected、Cancelled 或 Failed 都不能留下半提交 snapshot。guard 和 resolver 不应直接修改已经对用户可见的全局状态；需要提交的改变应在导航成功边界之后完成。

## 5. 并发和历史

- 用户快速连续点击时，桌面应用通常选择 `CancelPrevious`；
- 必须串行完成的向导可选择拒绝或排队策略；
- replace、redirect 和不记录历史是不同语义，不要用清空 journal 模拟；
- 每个窗口或导航区域使用自己的 navigation scope，避免多个窗口争用一份历史。

完整的 layout、group、index、参数、redirect 和多 outlet 示例见 [Dogfood routes](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Routing/DogfoodRoutes.cs)，实际导航与结果检查见 [Routing workload](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Routing/DogfoodRoutingWorkload.cs)。
