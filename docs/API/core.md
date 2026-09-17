# Core API

`AtomUI.City.Core` 是所有 City 应用的基础包，负责 Host、模块、依赖注入、生命周期、诊断与 UI dispatcher 抽象。

完整合同：[Core API Contracts](../modules/core/api-contracts.md)

## Host

| API | 用途 | 关键规则 |
| --- | --- | --- |
| `ApplicationHost.CreateBuilder` | 创建 Host builder | 配置阶段不保证并发写安全 |
| `IApplicationHostBuilder` | 配置 Host、服务和模块 | `Build()` 后所有配置入口冻结 |
| `IApplicationHost` | 启动、停止和访问服务 | Start/Stop/Dispose 共享受控生命周期 |
| `ApplicationHostOptions` | 应用标识、名称等 Host 配置 | Build 前设置，之后作为快照读取 |
| `IApplicationContext` | 应用实例、环境和目录信息 | Host 创建的只读上下文 |

`StopAsync()` 可以在 Start 之前调用，并仍然完成 scope、模块和 Generic Host 的清理。停止后的 Host 不允许再次启动。

## Module

| API | 用途 |
| --- | --- |
| `IModule` / `ModuleBase` | 定义服务配置、contribution、初始化和关闭钩子 |
| `[DependsOn]` | 声明必需或可选模块依赖 |
| `ServiceConfigurationContext` | 在构建 Provider 前注册服务 |
| `ContributionConfigurationContext` | Provider 建立后配置模块贡献 |
| `ApplicationInitializationContext` | 模块启动上下文与应用 scope |
| `ApplicationShutdownContext` | 模块关闭上下文 |

推荐覆盖异步生命周期方法，并传递调用方 token：

```csharp
public sealed class WorkspaceModule : ModuleBase
{
    public override async ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        await LoadWorkspaceAsync(cancellationToken);
    }
}
```

## DI registration markers

核心 marker 包括 `ServiceAttribute`、`SingletonServiceAttribute`、`ScopedServiceAttribute`、`TransientServiceAttribute`、`ExposeServicesAttribute` 以及相应 marker interface。生成器只把已选择 Module 拥有的注册结果带入 Root Provider；引用程序集不等于自动接收其中的全部服务。

服务所有权必须明确，避免无关程序集夺取另一个 Module 的注册所有权。Native AOT 路径依赖生成清单，不依赖运行时程序集扫描。

## LifecycleScope

| API | 用途 |
| --- | --- |
| `LifecycleScope` | 管理 application/window/navigation 等树形生命周期 |
| `LifecycleScopeKind` | 描述 scope 类型 |
| `LifecycleStage` / `LifecycleStages` | 生命周期阶段标识和标准阶段集 |
| `LifecyclePipeline` | 按顺序执行 middleware |

父 scope 停止时按 leaf-first 处理子 scope。Dispose 与并发 Stop 使用同一终止事务；框架不会把正常的 child 并发释放误报为父级关闭失败。

## Diagnostics

| API | 用途 |
| --- | --- |
| `IHostDiagnostics` | 写入和读取 Host 诊断 |
| `HostDiagnosticRecord` | code、severity、message 与 context |
| `HostDiagnosticSeverity` | 诊断级别 |
| `HostDiagnosticIds` | Core 稳定诊断码 |

空 code、空 message、未知 severity 会在边界被拒绝。Diagnostics 释放后禁止继续写入。

## UI dispatcher abstraction

Core 只公开 `IUiDispatcher`，不引用 Avalonia。桌面适配器由 Presentation/平台层注入；无 UI 环境可以使用 Testing 的 `FakeUiDispatcher`。
