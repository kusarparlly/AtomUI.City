# 测试 City 应用

City 应用至少需要三层测试：纯业务单元测试、框架契约测试和真实 Host/平台集成测试。单一“大而全”端到端测试既难定位，也无法替代边界测试。

## 1. 使用 TestHost 管理测试资源

```csharp
await using var host = TestHost
    .CreateBuilder()
    .UseProperty("scenario", "checkout")
    .UseDirectoryName("checkout")
    .Build();

Assert.Equal("checkout", host.Properties["scenario"]);
Assert.False(host.IsStopped);
```

`TestHost` 提供隔离目录、fake dispatcher、确定性 scheduler 和测试 diagnostics。builder 在 `Build()` 后冻结；Host 释放时停止工具并按配置删除目录。

## 2. 不用 Task.Delay 猜时间

```csharp
using var scheduler = new DeterministicScheduler();
var called = false;

scheduler.Schedule(TimeSpan.FromSeconds(5), () => called = true);
scheduler.AdvanceBy(TimeSpan.FromSeconds(5));

Assert.True(called);
```

虚拟推进时间能稳定验证 timeout、retry、debounce 和周期任务。固定延时只会让测试变慢，并在负载不同的机器上产生偶发失败。

## 3. 显式排空 UI 工作

把 `FakeUiDispatcher` 注入依赖 `IUiDispatcher` 的代码，由测试主动执行排队工作。断言至少覆盖：

- 工作运行在哪个逻辑 dispatcher；
- 取消前后是否执行；
- callback 异常是否进入预期结果或 diagnostics；
- Dispose 后是否拒绝新工作。

## 4. 测试 Module 生命周期

Module 测试应记录并断言：

```text
构造
-> ConfigureServices
-> contribution 配置
-> initialization
-> shutdown
-> dispose
```

除成功路径外，还要验证循环依赖、初始化失败回滚、并发 Stop/Dispose、Stop-before-Start 和清理异常聚合。需要完整生成清单时，优先使用进程级 fixture，因为入口程序集就是生成器连接协议的一部分。

## 5. 测试事件、路由和状态

| 能力 | 最小断言 |
| --- | --- |
| EventBus | handler 结果、顺序边界、拒绝/背压、owner 释放后不再投递 |
| Routing | Success/NotFound/Rejected/Cancelled/Failed、journal、并发策略、失败不半提交 |
| State | comparer、并发 Update、通知策略、scope 释放、非法 writer 被拒绝 |
| Presentation | UI dispatcher、ViewModel ownership、窗口/outlet 关闭、资源撤销 |

## 6. 运行真实应用门禁

发布候选至少运行：

- Core CLI 进程测试；
- Core + EventBus dogfood；
- 桌面 Headless 自动化；
- Windows GUI 自动化（Windows 发布目标）；
- Native AOT publish 与产物启动；
- 压力或 soak 配置下的资源释放检查。

把日志、退出码和结构化报告作为证据，不以“窗口看起来能打开”作为通过标准。测试工具详见 [Testing API 文档](../../API/testing.md)，完整桌面门禁见 [Desktop Dogfood test plan](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/TEST-PLAN.md)。
