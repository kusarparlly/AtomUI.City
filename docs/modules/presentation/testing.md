# AtomUI.City.Presentation Testing

## 测试层次

1. Unit/contract：纯对象、并发、ownership、失败注入和诊断。
2. Generator：Presentation View registrar 的确定性、重复检测和构造参数。
3. Avalonia.Headless process：真实 Window/Control/VisualTree/dispatcher，但不依赖桌面显示服务器。
4. Stress：10,000 次导航、Interaction、关闭竞态和资源回收。
5. Windows desktop dogfood：真实 Win32 Window、Show/Attach/Detach/Close 生命周期。

CLI 或纯 mock 不能代替第 3 和第 5 层。

## Feature 测试矩阵

| Feature | 必须覆盖 | 主要测试 |
| --- | --- | --- |
| 001 | inline/background dispatch、取消、callback failure、stopping cleanup | AvaloniaUiDispatcherTests, PlatformIntegrationTests |
| 002/003/012 | exact key、override/revoke、factory type、UI binding、generator output | ViewLocatorTests, ViewBindingTests, generator Presentation tests |
| 004/010/014/015 | FIFO、queue full、plan reuse、rollback、post-commit cleanup、async lease、Faulted | RouteOutletTests, PresentationIndustrialContractTests |
| 005 | Bind 不伪造、真实 attach/detach、identity 过滤、迟到事件、handler failure | ViewBindingTests, VisualFeedbackTests, headless/desktop fixture |
| 006/011 | scope resolution、modal FIFO、跨 Window 并行、capacity、cancel/revoke、optional validation | Interaction/Validation tests |
| 007/008 | owner revoke、覆盖恢复、active view、字典 UI revoke、局部失败继续 | Resource/Plugin tests |
| 009/016 | Attach、Show 前注册、named outlet、三类 close、唯一 confirmation、close/stop race | Runtime tests, headless/desktop fixture |

## 可执行命令

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
dotnet test tests/AtomUI.City.Presentation.Tests/AtomUI.City.Presentation.Tests.csproj -c Release
dotnet test tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj -c Release --filter FullyQualifiedName~Presentation
dotnet run --project fixtures/AtomUI.City.Presentation.HeadlessApp -c Release
dotnet run --project fixtures/AtomUI.City.Presentation.DesktopApp -c Release
./engineering/check-presentation-benchmarks.ps1 -Mode Verify -Rounds 1 -Job Short
./engineering/check-presentation-package-consumer.ps1
```

## 批准性能基线

Presentation 1.0 的批准性能环境是固定 Windows 开发机。BenchmarkDotNet 分别测量 exact View lookup、RouteOutlet replace、Interaction dispatch 和 visual identity notification；真实 Avalonia 行为仍由 Headless 与 desktop process 负责，benchmark 不替代平台测试。

```powershell
# 首次批准或明确重新批准时：三次独立运行并取中位数。
./engineering/check-presentation-benchmarks.ps1 -Mode Capture -Rounds 3 -Job Medium

# 发布候选：同机三次独立运行并与已批准文件比较。
./engineering/check-presentation-benchmarks.ps1 -Mode Compare -Rounds 3 -Job Medium
```

批准文件记录 Windows、CPU、进程架构、SDK、Runtime、BenchmarkDotNet、Git commit 和候选内容指纹，不记录用户名或机器名。环境字段不相同时禁止比较，必须由维护者明确重新批准，门禁不得静默覆盖 baseline。时间回退上限为 15%，分配回退上限为 10%；基线分配为零时新结果也必须为零。普通 Windows CI 使用 `Verify/Short` 证明 benchmark 可执行和结果完整，不把共享 runner 的波动写入批准基线。

## 日常 CI 门禁

- 单压力场景至少 10,000 次操作并在 30 秒内完成。
- 完成后所有 queue pending/in-flight 为 0。
- 候选、Entry、ViewModel lease、scope 和 registration 恰好释放一次。
- 被替换 View/VM 的弱引用在最多三次 `GC.Collect/WaitForPendingFinalizers` 后不可存活。
- warm-up 后压力段 retained memory 增量不超过 8 MiB。
- 无未处理异常、死锁或进程 hang；子进程 watchdog 超时必须失败并终止进程。

2026-09-10 Windows 1.0 可重复证据：工程合同 `69/69`、`AtomUI.City.Presentation.Tests` `143/143`、Presentation generator `22/22`；进程测试分别运行 Headless 工业场景和真实 Windows desktop self-test。固定开发机已生成 `baselines/windows-x64.json` 并通过独立 `3 x Medium` Compare；661 条公开签名已冻结；Release `net8.0/net10.0` 构建、SourceLink、strict package validation 和脱离 ProjectReference 的自包含 NuGet consumer 均通过。统一 RC 门禁最终输出候选内容指纹。

## 发布门禁

- Windows desktop self-test 必须创建并显示真实 Window，完成主/命名 Outlet 导航、真实 attached/detached 回执、Interaction、三类关闭映射和资源释放。
- Release build、unit、generator、headless、stress、desktop 均为零失败。
- 相对批准 baseline，执行时间回退不超过 15%，分配回退不超过 10%。
- Linux/macOS 只要求 build/publish；没有平台 smoke 证据时保持 experimental。
- `PublicAPI.Shipped.txt`、PublicApiAnalyzers、strict SDK package validation、XML 文件和 SourceLink 必须通过。
- 隔离 package consumer 只能从本地候选 NuGet 包引用 Presentation 及其依赖，不允许 ProjectReference；必须完成 View lookup、Outlet commit、Interaction 和 Runtime stop。

Windows 发布候选的统一入口是：

```powershell
./engineering/check-presentation-release.ps1
```

该命令顺序执行 format、Release 零警告 build、工程合同、Presentation unit/Headless/stress/desktop、generator、public API/SourceLink、包消费者和批准基线比较，并输出候选内容指纹。任一子门禁失败时不得标记或发布候选。

## 失败注入

必须覆盖 lookup/create/bind、guard/confirm、activation/deactivation、dispatcher、temporary attach/restore、old Entry cleanup、failure presenter、Window cleanup、plugin revoke 和 queue overflow。测试断言状态、Result、diagnostic code、关键 context、当前 UI 与所有权释放。
