# 快速开始

本文带你搭建一个最小的、可运行的 Core CLI 应用。完整验证工程见 [Core Headless App](../../../fixtures/AtomUI.City.Core.HeadlessApp/)。

## 1. 准备仓库与 SDK

示例通过**源代码引用**构建，因此先确保已经克隆了 AtomUI.City 仓库，并且本机安装了 .NET 10 SDK。

> **SDK 版本注意**：仓库根目录的 `global.json` 要求 SDK `10.0.300`。如果本机安装的是其它 10.x 版本（例如 `10.0.111`），请在示例项目目录放置一个本地 `global.json`。示例已经提供了
> 仓库使用的 SDK 约束见 [根目录 global.json](../../../global.json)：
>
> ```json
> {
>   "sdk": {
>     "version": "10.0.111",
>     "rollForward": "latestMajor",
>     "allowPrerelease": false
>   }
> }
> ```
>
> SDK 解析从当前工作目录向上查找 `global.json`；在仓库根目录执行命令即可使用仓库统一约束。

## 2. 项目文件

创建一个控制台项目，`OutputType` 必须显式设为 `Exe`（仓库根 `Common.props` 默认是 `Library`）。引用 Core 与生成器（`AtomUI.City.Generators` 作为分析器）：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$(AtomUICityDevelopTargetFramework)</TargetFramework>
  </PropertyGroup>

</Project>
```

示例通过 `samples/Directory.Build.props` 为 `samples` 下的所有项目统一加入 Core 与生成器引用，因此单个项目文件不必重复写：

```xml
<Project>

  <Import Project="../../../../Directory.Build.props" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$(MSBuildThisFileDirectory)../../../../src/AtomUI.City.Core/AtomUI.City.Core.csproj" />
    <ProjectReference Include="$(MSBuildThisFileDirectory)../../../../src/AtomUI.City.Generators/AtomUI.City.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

</Project>
```

## 3. 最小程序

```csharp
using AtomUI.City.Core.Hosting;

namespace Quickstart;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "Quickstart";
            options.ApplicationName = "Quickstart";
        });

        await using var host = builder.Build();

        await host.RunAsync();

        return 0;
    }
}
```

解释：

- `ApplicationHost.CreateBuilder(args)` 返回 `IApplicationHostBuilder`。宿主默认已经配置了 Console 日志，无需额外设置。
- `ConfigureHost(...)` 通过 `ApplicationHostOptions` 设置应用标识（详见《宿主配置》）。
- `Build()` 构建并为生成器注册的服务搭建 DI 容器。
- `RunAsync()` 启动宿主，等待被停止后再退出。对于纯 CLI 应用，你通常会在一个 `IHostedService` 里执行命令并调用停止（详见《创建应用》）。

## 4. 构建与运行

在 `samples` 目录下执行：

```
dotnet build Quickstart/Quickstart.csproj -c Debug
dotnet output/bin/Debug/Quickstart/net10.0/Quickstart.dll
```

## 5. 下一步

- 了解宿主如何承载模块与 DI 生命周期 →《创建应用》
- 配置宿主各项选项 →《宿主配置》
- 用 `[ApplicationModule]` 组织功能 →《创建模块》
