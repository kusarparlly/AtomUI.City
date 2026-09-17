# 安装与快速开始

## 选择包

最小 Host 只需要 `AtomUI.City.Core`。按需添加 EventBus、Routing、State 等运行时包；不要为了“以后可能会用”而一次引用所有模块。

候选版本尚未正式发布到 NuGet 时，可使用仓库生成的本地候选包。发布后项目引用形式如下：

```xml
<ItemGroup>
  <PackageReference Include="AtomUI.City.Core" Version="&lt;candidate-version&gt;" />
</ItemGroup>
```

## 最小 Host

```csharp
using AtomUI.City.Core.Hosting;

var builder = ApplicationHost.CreateBuilder(args);
builder.ConfigureHost(options =>
{
    options.ApplicationId = "Contoso.Inventory";
    options.ApplicationName = "Inventory";
});

await using var host = builder.Build();
await host.StartAsync();

// 在这里解析并运行应用服务。

await host.StopAsync();
```

`Build()` 冻结 builder，构造 Root Provider 和已选择模块；异步初始化在 `StartAsync()` 中执行。`StopAsync()` 与 `DisposeAsync()` 都是生命周期事务的一部分，应用必须等待它们完成。

## 添加模块

```csharp
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

public sealed class InventoryModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<InventoryService>();
    }
}

var builder = ApplicationHost.CreateBuilder(args);
builder.UseModule<InventoryModule>();
```

模块依赖使用 `[DependsOn]` 声明。City 在创建模块实例前验证完整模块图；循环依赖、缺失必需依赖和非法 descriptor 会使 `Build()` 失败。

## 生命周期模板

```csharp
await using var host = builder.Build();

try
{
    await host.StartAsync(cancellationToken);
    await RunApplicationAsync(host.Services, cancellationToken);
}
finally
{
    await host.StopAsync(CancellationToken.None);
}
```

原则：

- 不在 `Build()` 期间执行真正的异步 IO；
- 不缓存从已释放 scope 解析出的 scoped 服务；
- 将订阅、窗口、导航和后台任务绑定到相应 `LifecycleScope`；
- 不使用固定 `Task.Delay` 猜测关闭完成；
- 对 `StartAsync`、业务操作和 `StopAsync` 分别考虑取消策略。

## 下一步

- [Core API](core.md)
- [Core 使用指南](../guides/core/getting-started.md)
- [包边界](../architecture/package-boundaries.md)
