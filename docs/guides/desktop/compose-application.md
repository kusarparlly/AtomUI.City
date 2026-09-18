# 组合桌面应用

City 的桌面应用由两个协作的生命周期组成：City Host 管理 Module、DI 和后台能力，Avalonia 管理 UI 线程和桌面 lifetime。二者应显式协作，不能相互假设对方已经启动或关闭。

## 组合顺序

```text
解析启动参数
-> 创建 City builder
-> 注册 State/Security/Localization/Presentation 等服务
-> 选择 EventBus/Routing/Data/Presentation 和应用 Module
-> Build City Host
-> 启动 Avalonia lifetime
-> StartAsync City Host
-> 创建并显示主窗口
-> 请求退出
-> 关闭全部 WindowSession
-> StopAsync City Host
-> Dispose Host
```

实际平台可能调整 City Host 与 UI lifetime 的精确启动点，但必须保证 UI 服务只能在 dispatcher 可用后执行，且退出时不留下窗口、订阅或后台任务。

## 组合根示例

```csharp
var builder = ApplicationHost.CreateBuilder(args);

builder.ConfigureHost(options =>
{
    options.ApplicationId = "Contoso.Inventory.Desktop";
    options.ApplicationName = "Inventory Workbench";
});

builder.ConfigureServices(services =>
{
    services.AddState();
    services.AddRouting(
        AtomUI.City.Generated.GeneratedRoutingRouteManifest.CreateDescriptors());
    services.AddLocalization(options =>
    {
        options.DefaultCulture = CultureInfo.GetCultureInfo("en-US");
        options.DefaultUICulture = CultureInfo.GetCultureInfo("en-US");
    });
    services.AddPresentation(new PresentationQueueOptions
    {
        OutletPendingCapacity = 32,
        ModalInteractionPendingCapacity = 8,
    });
});

builder
    .UseModule<EventBusModule>()
    .UseModule<RoutingModule>()
    .UseModule<PresentationModule>()
    .UseModule<InventoryDesktopModule>();
```

这是组合根，不是让所有 Module 都把注册堆进一个方法。各业务 Module 仍负责自己的服务和贡献。

## UI 线程规则

- View、控件和窗口只在 UI dispatcher 上创建或修改；
- Core 只依赖 `IUiDispatcher`，平台适配由 Presentation 层提供；
- 后台 handler 不因为“最终会更新 UI”就默认运行在 UI 线程；
- 不通过捕获 `SynchronizationContext` 建立隐式线程策略；
- 测试中使用 `FakeUiDispatcher` 或 Avalonia.Headless，而不是固定延时等待 UI。

## 窗口和页面所有权

每个窗口应拥有独立的 lifecycle scope、navigation scope、短期 state 和 EventBus subscriptions。窗口关闭时按以下方向释放：

```text
停止接收新交互
-> 取消窗口工作
-> 关闭 outlet 与 interaction
-> 撤销订阅和资源贡献
-> 释放 ViewModel/View
-> 终止窗口 scope
```

关闭一个窗口不应停止 application scope，也不应撤销其他窗口的订阅。

## 路由到呈现

Routing 决定目标并提交 navigation snapshot；Presentation 根据 ViewModel 类型和 view key 定位 View，再提交到指定 outlet。业务代码不要直接调用内部 view factory 绕过这个事务，否则 guard、scope、history 和释放顺序会失去一致性。

## 生产级参考

[Desktop Dogfood](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/README.md) 是当前端到端参考：它覆盖真实窗口、VisualTree、多 outlet、路由、State、EventBus、Localization、Security、Data 和确定性关闭。阅读时优先关注其 [组合根](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Composition/DogfoodHost.cs) 与 [设计说明](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/DESIGN.md)，不要直接复制压力规模。
