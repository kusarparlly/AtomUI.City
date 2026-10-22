# 使用指南：核心（Core）

本目录面向希望使用 **AtomUI.City.Core（以下简称为 Core）** 构建**纯命令行（CLI / 控制台）应用程序**的使用者。

Core 是 AtomUI.City 中处于代码冻结、被视为生产级（industrial-grade）的模块，因此这些指南**只覆盖已冻结的功能**（范围见第一篇文章《[功能范围](./overview.md)》“冻结范围”一节）。

> 本指南的验证工程基于 **源代码引用（ProjectReference）**；可执行资产位于
> [Core MVP fixture](../../../fixtures/AtomUI.City.Core.Mvp/)，并经过编译与运行验证。

## 目标读者与前提

- 你将构建**不含 UI**（无 Avalonia / 无 Presentation / 无 MVVM / 无路由）的 CLI 应用。
- 你熟悉 C# 与 .NET（示例目标框架为 net10.0）。
- 你已经克隆了 AtomUI.City 仓库源码（示例通过 `ProjectReference` 引用本仓库内的 `src/AtomUI.City.Core` 与 `src/AtomUI.City.Generators`）。

## 指南目录

| 指南 | 内容 | 配套示例 |
| --- | --- | --- |
| [功能范围](./overview.md) | 本文：Core 冻结能力总览、术语、应用形态、示例代码结构 | `samples/Quickstart`、`samples/TodoCli` |
| [快速开始](./getting-started.md) | 搭建项目、引用 Core、写下最小值 CLI（`ApplicationHost.CreateBuilder` → `Build` → `RunAsync`） | `samples/Quickstart` |
| [创建应用](./create-application.md) | 应用宿主 `ApplicationHost`、`IApplicationHost`、DI、启动/停止生命周期 | `samples/TodoCli/Program.cs`、`TodoHost.cs` |
| [宿主配置](./hosting-options.md) | `ApplicationHostOptions` 各项配置、`ConfigureHost`、宿主扩展方法 | `samples/TodoCli/TodoHost.cs` |
| [创建模块](./create-module.md) | `[ApplicationModule]`、`ModuleBase`、`ConfigureServices`、初始化/关闭钩子、`[DependsOn]` | `samples/TodoCli/CliModule.cs` |
| [依赖注入](./dependency-injection.md) | `[Service]`、`[ExposeServices]`、标记接口、源码生成器注册、范围 | `samples/TodoCli/TodoStore.cs` 等 |
| [配置](./configuration.md) | 配置提供程序、`ConfigureServices`、Options 模式（`IConfigureOptions`） | `samples/TodoCli/TodoOptions.cs` |
| [诊断](./diagnostics.md) | `IHostDiagnostics`、`HostDiagnosticRecord`、构建期错误、`GetBuildDiagnostics` | `samples/TodoCli/CliRunner.cs` |
| [测试](./testing.md) | 纯逻辑单测 + CLI 进程级集成测试、入口程序集注意事项 | `samples/TodoCli.Tests` |

## 应用形态（纯 CLI）

本系列指南面向的“应用”是一个**控制台入口**，通常结构如下：

- 一个可执行项目（`OutputType = Exe`），含一个 `Program.Main`。
- 至少一个**应用模块**（带 `[ApplicationModule]`），作为服务注册与初始化的载体。
- 一个或多个由生成器自动注册的服务（`[Service]`、标记接口）。
- 可选：一个 `IHostedService` 在启动时执行一次命令后请求关闭（见《创建应用》）。

## 概念速览

- **宿主（Host）**：`ApplicationHost` 是应用的根。通过 `ApplicationHost.CreateBuilder(args)` 创建 `IApplicationHostBuilder`，配置后 `Build()` 得到一个 `IApplicationHost`，再 `StartAsync()` / `RunAsync()`。
- **模块（Module）**：功能单元，`[ApplicationModule]` 标记，继承 `ModuleBase`，拥有服务配置与初始化钩子。
- **生成器（Generator）**：`AtomUI.City.Generators` 在编译期扫描 `[Service]`、标记接口、模块，自动生成注册代码，减少手写样板。
- **诊断（Diagnostics）**：宿主与模块运行时的结构化诊断记录（`HostDiagnosticRecord`）。

> 注意：生成器对模块与服务的发现是基于**入口程序集（`Assembly.GetEntryAssembly()`）**的，因此测试时如需完整启动宿主，更稳妥的是进程级集成测试（见《测试》）。

## 术语对照

| 中文 | 英文 | 命名空间 |
| --- | --- | --- |
| 应用宿主 | `ApplicationHost` / `IApplicationHost` | `AtomUI.City.Core.Hosting` |
| 宿主构建器 | `IApplicationHostBuilder` | `AtomUI.City.Core.Hosting` |
| 模块 | `ModuleBase` | `AtomUI.City.Core.Modularity` |
| 依赖注入 | `[Service]`、`[ExposeServices]` 等 | `AtomUI.City.Core.DependencyInjection` |
| 诊断 | `IHostDiagnostics`、`HostDiagnosticRecord` | `AtomUI.City.Core.Diagnostics` |

## 参考文档

- 模块内部设计文档：`docs/modules/core/`（`hosting.md`、`modularity.md`、`dependency-injection.md`、`diagnostics.md`、`configuration.md`、`lifecycle.md`、`oversight`/`release-candidate-report.md` 等）。
- 决策记录：`docs/decisions/0002-use-microsoft-extensions-hosting.md`、`0003-keep-core-ui-independent.md`、`0006-aot-first-source-generation.md`。
