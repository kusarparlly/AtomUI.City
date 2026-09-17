# Testing API

`AtomUI.City.Testing` 只供测试项目引用，生产项目不得依赖它。

完整合同：[Testing API Contracts](../modules/testing/api-contracts.md)

## TestHost

```csharp
await using var host = TestHost
    .CreateBuilder()
    .UseProperty("scenario", "checkout")
    .Build();

Assert.NotNull(host.Dispatcher);
Assert.NotNull(host.Scheduler);
Assert.False(host.Diagnostics.Contains("AUCTEST999"));
```

`TestHostBuilder.Build()` 后冻结。Host 释放时停止 fake runtime、释放 tracker 中的资源并按配置删除测试目录。

## 专用 Host

| API | 用途 |
| --- | --- |
| `ModuleTestHost` | 验证模块图、服务配置、初始化和 shutdown |
| `RoutingTestHost` | 构造路由并测试 match/navigation helper |
| `PluginTestHost` | 测试插件状态和 contribution 撤销；不等于生产 PluginSystem |

这些 Host 的 builder 都是一次性配置对象，成功 Build 后拒绝继续修改。

## 确定性线程工具

| API | 用途 |
| --- | --- |
| `FakeUiDispatcher` | 排队并由测试显式 drain UI work |
| `FakeUiWorkItem` | 观察完成、取消和异常 |
| `DeterministicScheduler` | 使用虚拟时间替代固定 `Task.Delay` |
| `DeterministicScheduledWorkItem` | 观察和取消计划任务 |
| `DisposableTracker` | 统一跟踪同步/异步可释放资源 |

```csharp
using var scheduler = new DeterministicScheduler();
var called = false;

scheduler.Schedule(TimeSpan.FromSeconds(5), () => called = true);
scheduler.AdvanceBy(TimeSpan.FromSeconds(5));

Assert.True(called);
```

## Source generator 与 AOT

| API | 用途 |
| --- | --- |
| `SourceGenerationTestCase` | 在内存 compilation 中运行 generator |
| `GeneratedSourceSnapshot` | 规范化并稳定排序生成源码 |
| `SourceGenerationTestResult` | 同时暴露 generator 与 compilation diagnostics |
| `AotCompatibilityCheck` | 检查运行时扫描、dynamic code 和反射风险模式 |

## Data 测试替身

`FakeDataConnection`、`ScriptedDataTransport`、`ScriptedDataCredentialProvider` 和 `RecordingDataRequestHandler` 用于构造确定性的连接、传输、认证和管线测试。脚本响应与调用记录都是快照，不应由外部修改。

## 测试层级

使用 `TestLayerAttribute` 或 `TestLayerNames` 标记 Unit、Contract、FrameworkIntegration、RuntimeLifecycle、PlatformIntegration、Generator、Build 等层级，便于工程门禁选择正确测试集。
