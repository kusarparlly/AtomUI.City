# 从零启动一个 City 应用

本篇建立一个最小但生命周期完整的 City 应用。这里先使用 Core；其他能力都以 Module 或服务注册的方式增量接入。

## 1. 准备项目

当前仓库开发基线使用 `.NET 10`。City 正式包发布前，仓库内示例通过 `ProjectReference` 使用源码：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="path/to/AtomUI.City.Core.csproj" />
    <ProjectReference Include="path/to/AtomUI.City.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

生成器是编译期依赖，不应成为应用的运行时程序集引用。正式包可用后，将源码引用替换为相应 `PackageReference`，业务代码不需要改变。

## 2. 创建 Host

```csharp
using AtomUI.City.Core.Hosting;

var builder = ApplicationHost.CreateBuilder(args);
builder.ConfigureHost(options =>
{
    options.ApplicationId = "Contoso.Inventory";
    options.ApplicationName = "Inventory";
    options.ApplicationVersion = "0.1.0";
});

await using var host = builder.Build();

try
{
    await host.StartAsync();
    await RunApplicationAsync(host.Services);
}
finally
{
    await host.StopAsync(CancellationToken.None);
}
```

这段代码建立四个明确边界：

1. `CreateBuilder`：收集配置、服务和模块；
2. `Build`：冻结 builder，验证模块图并创建 Root Provider；
3. `StartAsync`：执行真正的异步初始化；
4. `StopAsync`/`DisposeAsync`：停止 scope、模块和底层 Generic Host。

不要在 `Build()` 后修改 builder，也不要用同步等待包裹 `StartAsync()` 或 `StopAsync()`。

## 3. 添加应用模块

```csharp
using AtomUI.City.Core.Modularity;

[ApplicationModule]
[Module("Contoso.Inventory.Application")]
public sealed class InventoryApplicationModule : ModuleBase
{
    public override async ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        await WarmUpAsync(cancellationToken);
    }
}

builder.UseModule<InventoryApplicationModule>();
```

模块构造函数只建立对象不变量。需要 IO、dispatcher 或已构建 Provider 的工作放到异步初始化阶段。

## 4. 选择能力而不是全量引用

```csharp
builder
    .UseModule<EventBusModule>()
    .UseModule<RoutingModule>()
    .UseModule<PresentationModule>()
    .UseModule<InventoryApplicationModule>();
```

应用只选择实际需要的 Module。共享一个 Root Provider 不代表所有被引用程序集中的服务都会自动进入容器；服务注册受已选择 Module 和 owner 约束。

## 5. 确认退出路径

在继续增加业务前，先验证：

- 正常运行后进程能够退出；
- Start 失败时已创建资源得到回滚；
- 在 Start 之前调用 Stop 也能完成清理；
- 取消 token 能传递到应用自己的异步任务；
- `await using` 不被省略。

可直接运行的最小工程见 [Core Quickstart](core/samples/Quickstart/)；完整 CLI 结构见 [Core TodoCli](core/samples/TodoCli/)。

下一步：[组织应用与模块](application-structure.md)。
