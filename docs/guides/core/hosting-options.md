# 宿主配置

`ApplicationHostOptions` 用于配置应用宿主。通过 `builder.ConfigureHost(...)` 设置，其所有字段均有默认值，按需覆盖即可。

## ApplicationHostOptions 字段

命名空间 `AtomUI.City.Core.Hosting`。

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ApplicationId` | `string?` | `null` | 应用唯一标识（建议与程序集/产品一致）。 |
| `ApplicationName` | `string?` | `null` | 应用显示名称。 |
| `ApplicationVersion` | `string?` | `null` | 应用版本字符串。 |
| `ShutdownTimeout` | `TimeSpan` | `30s` | 正常关闭的最长等待时间；超时后强制终止。 |
| `DiagnosticsCapacity` | `int` | `1024` | 运行期诊断记录（`IHostDiagnostics`）的最大容量（见《诊断》）。 |

## 配置示例

```csharp
builder.ConfigureHost(options =>
{
    options.ApplicationId = "TodoCli";
    options.ApplicationName = "TodoCli";
    options.ApplicationVersion = "1.0.0";
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
    options.DiagnosticsCapacity = 2048;
});
```

在 `TodoCli` 示例中，仅覆盖了 `ApplicationId`、`ApplicationName` 与 `ShutdownTimeout`：

```csharp
builder.ConfigureHost(options =>
{
    options.ApplicationId = "TodoCli";
    options.ApplicationName = "TodoCli";
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
});
```

## 宿主构建器方法

`IApplicationHostBuilder` 提供：

- `Configuration`（`IConfigurationManager`）——向宿主加入配置提供程序（见《配置》）。
- `ConfigureServices(Action<IServiceCollection>)`——注册自定义服务。
- `ConfigureHost(Action<ApplicationHostOptions>)`——设置上述选项。
- `Build()`——完成构建，返回 `IApplicationHost`。

以及这些扩展方法（命名空间 `AtomUI.City.Core`）：

| 扩展 | 作用 | 引入能力 |
| --- | --- | --- |
| `UseModule<T>()` | 显式声明应用模块 | 模块系统（见《创建模块》） |
| `ConfigureLifecycle(...)` | 配置生命周期管线 | 生命周期 |
| `GetBuildDiagnostics()` | 取回构建期诊断 | 诊断（见《诊断》） |
| `UseDefaultServiceProvider(...)` | 配置最终 Microsoft DI 容器的 `ServiceProviderOptions` | Host / DI |

需要在所有环境中执行启动期 DI 校验时，可显式配置：

```csharp
builder.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
```

该扩展只配置 Generic Host 最终拥有的唯一 Provider，不会调用 `BuildServiceProvider` 创建临时容器。未调用时继续使用 Generic Host 的默认策略。

> 目前**没有**公开的 `ConfigureOptions` / `ConfigureConfiguration` 扩展方法。配置 Options 的推荐方式是直接在 `ConfigureServices` 里调用 `services.Configure<T>(...)`（见《配置》）。

## 参考

- 创建应用宿主：`docs/guides/core/create-application.md`
- 配置与 Options：`docs/guides/core/configuration.md`
- 模块：`docs/guides/core/create-module.md`
