# 组织应用与模块

City 应用推荐以业务能力拆分 Module，以一个应用 Module 作为组合根。Module 不是任意代码文件夹的同义词，它拥有明确的依赖、服务注册和生命周期边界。

## 推荐工程形态

```text
Contoso.Inventory.App/          可执行程序与组合根
Contoso.Inventory.Foundation/   日志、设置等基础能力
Contoso.Inventory.Catalog/      商品领域
Contoso.Inventory.Orders/       订单领域
Contoso.Inventory.Desktop/      View、ViewModel 与窗口组合
Contoso.Inventory.Tests/        单元与契约测试
```

程序集可以引用其他程序集，但不能夺取另一个 Module 的服务注册所有权。跨项目依赖表达代码可见性；Module owner 表达谁有权把注册结果带入应用。

## 声明依赖图

```csharp
[Module("Contoso.Catalog")]
public sealed class CatalogModule : ModuleBase;

[Module("Contoso.Orders")]
[DependsOn(typeof(CatalogModule))]
public sealed class OrdersModule : ModuleBase;

[ApplicationModule]
[ServiceRegistrationOwner]
[Module("Contoso.Inventory.Desktop")]
[DependsOn(typeof(OrdersModule))]
public sealed class InventoryDesktopModule : ModuleBase;
```

应用只需要选择根 Module；依赖 Module 由图展开。City 会在实例化 Module 之前验证循环和非法依赖，因此不要用手工调用初始化方法绕过模块图。

## 注册服务

简单服务可以由生成器注册：

```csharp
[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IOrderReader))]
public sealed class OrderReader : IOrderReader
{
}
```

需要工厂、外部实例或复杂 options 时，在 Module 的 `ConfigureServices` 中使用 `IServiceCollection`：

```csharp
public sealed class OrdersModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<OrderCache>();
        context.Services.AddScoped<IOrderSession, OrderSession>();
    }
}
```

两种方式可以并存，但一个注册必须有清晰 owner。生成器把服务归属于同一程序集内带 `[ServiceRegistrationOwner]` 的 Module；不要把“程序集被引用”误认为“其中全部服务都应全局注册”。

## 生命周期职责

| 位置 | 适合做什么 | 不适合做什么 |
| --- | --- | --- |
| Module 构造函数 | 参数校验、保存不可变依赖 | IO、启动线程、解析 scoped service |
| `ConfigureServices` | 描述 DI 注册 | 从尚未构建的 Provider 解析服务 |
| contribution 配置 | 注册路由、事件、资源等贡献 | 启动长期运行任务 |
| `OnApplicationInitializationAsync` | 异步预热、连接和启动 | fire-and-forget 初始化 |
| `OnApplicationShutdownAsync` | 停止本 Module 拥有的工作 | 释放其他 Module 的资源 |

关闭顺序与依赖顺序相反。每个 Module 应只清理自己创建或明确拥有的资源。

## 组合根保持薄

可执行项目负责：

- 读取启动参数和环境；
- 配置 Host options；
- 选择顶层 Module；
- 启动平台生命周期；
- 把最终退出码返回操作系统。

业务规则不应堆积在 `Program.cs`。完整的 48 Module 依赖图实例可参考 [Desktop Dogfood Module Catalog](../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/MODULE-CATALOG.md)，但普通应用应从少量清晰边界开始。

下一步：[Core 专题](core/overview.md) 或 [发布和订阅事件](eventbus/publish-subscribe.md)。
