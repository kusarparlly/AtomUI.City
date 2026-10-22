# 测试

Core CLI 应用推荐两层测试：**纯逻辑单测**（快速、无需宿主）与 **CLI 进程级集成测试**（真实启动宿主、验证生成器与 DI）。配套证据见 [Core tests](../../../tests/AtomUI.City.Core.Tests/)。

## 1. 为什么要两层

Core 的生成器（`AtomUI.City.Generators`）在**编译期**把 `[Service]`、标记接口、模块的注册写进生成代码，并在宿主构建时通过**入口程序集**（`Assembly.GetEntryAssembly()`）发现（见 `src/AtomUI.City.Core/Hosting/ApplicationHostBuilder.cs` 中 `ModuleCatalog.LoadGenerated(...)` 与 `GeneratedServiceRegistrationCatalog.LoadGenerated(...)`）。

> 这意味着：在测试进程里运行宿主时，`GetEntryAssembly()` 是测试运行器（如 vstest / xunit 宿主）而不是你的应用程序集，因此**应用模块及其生成器注册不会被自动发现**。这正是 `TodoCli` 采用“进程级集成测试”——直接以子进程启动 `TodoCli.dll`（此时入口程序集就是应用自身）——的原因。

纯逻辑类（`TodoStore`、`TodoFormatter`）没有宿主依赖，可以直接用单测覆盖。

## 2. 纯逻辑单测

```csharp
public sealed class TodoStoreTests
{
    [Fact]
    public void AddThenCompleteUpdatesTheItem()
    {
        var store = new TodoStore();
        var added = store.Add("Buy milk");
        var completed = store.Complete(added.Id);

        Assert.True(completed);
        var item = Assert.Single(store.List());
        Assert.True(item.IsDone);
    }
}
```

把命令解析、格式化、状态机等不依赖 DI 的代码全部抽取成这样的普通类，即可获得高性价比的单元覆盖。

## 3. 进程级集成测试

集成测试以子进程运行应用，断言退出码、标准输出与标准错误，从而**真实**走一遍宿主、生成器、模块与 DI。可执行入口必须捕获普通顶层异常，将完整异常写入 `stderr` 并返回非零退出码；测试不得依赖 Windows Error Reporting 弹窗表达失败。

```csharp
private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        ErrorDialog = false,
    };
    startInfo.ArgumentList.Add(CliDll);   // AppContext.BaseDirectory 下的 TodoCli.dll
    foreach (var arg in args)
    {
        startInfo.ArgumentList.Add(arg);
    }

    using var process = Process.Start(startInfo)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException)
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException("CLI did not exit within 30 seconds.");
    }
    return (process.ExitCode, stdout.Result, stderr.Result);
}

[Fact]
public void AddThenCompleteProducesExitCodeZero()
{
    Assert.Equal(0, Run("add", "Buy milk").ExitCode);
}

[Fact]
public void UnknownCommandFails()
{
    Assert.NotEqual(0, Run("bogus").ExitCode);
}

[Fact]
public void HelpPrintsCommands()
{
    var (exitCode, stdout, _) = Run("--help");
    Assert.Equal(0, exitCode);
    Assert.Contains("add", stdout);
}
```

仓库测试基础设施还必须为子进程设置有界等待：超时后终止整个进程树，并同时收集 `stdout`、`stderr` 与退出码。Windows 测试运行器应只在启动测试子进程期间设置 `SEM_NOGPFAULTERRORBOX`，fixture 自身也在入口设置相同进程错误模式；不得修改机器级或用户级 WER 注册表配置。这样普通托管异常由入口报告，无法捕获的进程故障也不会挂起自动化等待人工点击。

应用入口的最小异常边界如下：

```csharp
public static async Task<int> Main(string[] args)
{
    try
    {
        return await RunAsync(args);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
}
```

`TodoCli.dll` 因为测试项目 `ProjectReference` 了 `TodoCli`，会被复制到测试输出目录，因此用 `AppContext.BaseDirectory` 定位即可，路径稳定。

## 4. 项目文件

测试项目引用 xunit（版本来自仓库 `Directory.Packages.props` 的集中管理）并引用被测的可执行项目：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$(AtomUICityDevelopTargetFramework)</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="$(MSBuildThisFileDirectory)../TodoCli/TodoCli.csproj" />
  </ItemGroup>

</Project>
```

> 注意：测试项目的引用路径要相对正确。`TodoCli.Tests` 位于仓库根向下第 5 层，引用 `src/` 需要 5 级 `../`。

## 5. 运行测试

在 `samples` 目录下（让本地 `global.json` 生效）：

```
dotnet test TodoCli.Tests/TodoCli.Tests.csproj -c Debug
```

## 6. 关于 `AtomUI.City.Testing`

仓库还提供 `src/AtomUI.City.Testing`（`TestHost` / `ModuleTestHost`），用于**确定性**地驱动模块钩子并携带假 UI 调度器与确定性调度器，适合验证模块钩子时序与 UI 相关逻辑。注意：`ModuleTestHost` 直接调用模块钩子，**不会**运行生成器注册，因此在以生成器为主的纯 CLI 场景下，优先用上文第 3 节的进程级集成测试。

## 7. 参考

- 内部测试基础设施：`docs/modules/testing/overview.md`、`test-host.md`
- 测试策略：`docs/decisions/0009-unit-test-gate.md`
