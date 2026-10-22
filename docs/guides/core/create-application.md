# 创建应用

本文说明如何构建一个真实的 Core CLI 应用：从 `ApplicationHost` 起步，把命令行参数接入 DI，注册一个 `IHostedService` 执行命令并干净地退出。完整验证工程见 [Core MVP CLI](../../../fixtures/AtomUI.City.Core.Mvp/AtomUI.City.Core.MvpCli/)。

## 1. 宿主概览

`ApplicationHost` 是应用的根对象，路径为 `AtomUI.City.Core.Hosting`。核心 API：

```csharp
IApplicationHostBuilder builder = ApplicationHost.CreateBuilder(args);
// builder.Configuration        -> IConfigurationManager（见《配置》）
// builder.ConfigureServices(...) -> 注册用户服务
// builder.ConfigureHost(...)     -> 配置 ApplicationHostOptions（见《宿主配置》）

IApplicationHost host = builder.Build();
await host.StartAsync();   // 启动：模块初始化 + 运行托管服务
await host.StopAsync();    // 停止：关闭模块 + 释放
await host.DisposeAsync();
```

`IApplicationHost` 提供：

- `Services`（`IServiceProvider`）——运行期容器，用于解析服务。
- `Context`（`IApplicationContext`）——应用上下文。
- `RunAsync()`、`StartAsync()`、`StopAsync()`——生命周期入口。
- 实现 `IDisposable` 与 `IAsyncDisposable`。

> 生成器对模块/服务的发现依赖**入口程序集**（`Assembly.GetEntryAssembly()`）。入口程序集必须是包含 `[ApplicationModule]` 的那个程序集（即 CLI 可执行程序集），否则模块不会被自动加载。

## 2. 把命令行参数接入 DI

CLI 需要读取 `args`。Core 本身不解析参数，我们将原始 `string[] args` 作为单例注册进 DI，供 `CliRunner` 使用：

```csharp
public static class TodoHost
{
    public static IApplicationHostBuilder CreateBuilder(string[] args)
    {
        var builder = ApplicationHost.CreateBuilder(args);

        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "TodoCli";
            options.ApplicationName = "TodoCli";
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });

        builder.Configuration.AddEnvironmentVariables(prefix: "TODOCLI_");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(args);
            services.Configure<TodoOptions>(builder.Configuration.GetSection("Todo"));
            services.AddSingleton<CliRunner>();
            services.AddHostedService(sp => sp.GetRequiredService<CliRunner>());
        });

        return builder;
    }
}
```

要点：

- 用 `services.AddSingleton(args)` 将 `string[]` 注册为单例——运行时 `CliRunner` 可以直接解析到同一个数组。
- `CliRunner` 既是单例，又被注册为 `IHostedService`，这样它能在宿主启动时自动执行。

## 3. 用 `IHostedService` 执行一次命令

纯 CLI 常见的模式：一个 `HostedService` 在 `StartAsync` 里跑完命令、写退出码，然后调用 `IHostApplicationLifetime.StopApplication()` 让宿主停止：

```csharp
public sealed class CliRunner : IHostedService
{
    private readonly string[] _args;

    public CliRunner(string[] args /* , ITodoStore store, ... */)
    {
        _args = args;
    }

    public int ExitCode { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ExitCode = Run();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Command failed.");
            ExitCode = 1;
        }

        _lifetime.StopApplication();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

> 用 `IHostApplicationLifetime.StopApplication()` 请求宿主在托管服务跑完后停止，是让 CLI 自然退出的关键。

## 4. 装配入口程序

`Program.Main` 通过一个可复用的 `CreateBuilder` 方法把构造与执行分开，便于测试复用：

```csharp
public static async Task<int> Main(string[] args)
{
    var builder = TodoHost.CreateBuilder(args);

    await using var host = builder.Build();

    await host.StartAsync();   // 触发 CliRunner.StartAsync -> 执行命令 -> StopApplication

    var runner = host.Services.GetRequiredService<CliRunner>();
    var exitCode = runner.ExitCode;

    await host.StopAsync();    // 清理

    return exitCode;
}
```

`ExitCode` 成为进程退出码，因此 `Run()` 返回的每种错误都能映射为不同的退出码（例如未知命令返回 `2`、未找到的 id 返回 `3`）。

## 5. 命令分发

`CliRunner.Run()` 只是 `switch` 分发到各个子命令方法：

```csharp
private int Run()
{
    var verb = _args.FirstOrDefault();
    switch (verb)
    {
        case "add":       return RunAdd();
        case "list":      return RunList();
        case "complete":  return RunComplete();
        case "--help":
        case "-h":        return RunHelp();
        default:
            Console.Error.WriteLine("Usage: TodoCli <add|list|complete|--help>");
            return 2;
    }
}
```

## 6. 关于宿主"启动信息"日志

Core 宿主默认会输出类似 `Microsoft.Hosting.Lifetime` 的“Application started / Application is shutting down…”信息日志。CLI 应用里它们略显冗余，但属于正常行为；可自行在 `ConfigureLogging` 中调整过滤（Core 不强制依赖 `Microsoft.Extensions.Logging.Console`，默认已可用）。

## 7. 参考

- 宿主选项与配置：`docs/guides/core/hosting-options.md`
- 服务与生成器注册：`docs/guides/core/dependency-injection.md`
- 模块生命周期：`docs/guides/core/create-module.md`
