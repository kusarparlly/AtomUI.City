# AtomUI.City.Presentation Testing

## 测试层次

1. Unit/contract：纯对象、并发、ownership、失败注入和诊断。
2. Generator：Presentation View registrar 的确定性、重复检测和构造参数。
3. Avalonia.Headless process：真实 Window/Control/VisualTree/dispatcher，但不依赖桌面显示服务器。
4. Stress：10,000 次导航、Interaction、关闭竞态和资源回收。
5. Windows desktop dogfood：真实 Win32 Window、Show/Attach/Detach/Close 生命周期。

CLI 或纯 mock 不能代替第 3 和第 5 层。

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-PRESENTATION-001 | Unit/PlatformIntegration | AvaloniaUiDispatcherTests; PresentationPlatformIntegrationTests | inline/background dispatch、取消、callback failure 和 stopping cleanup。 | dispatcher 不可用、callback 失败和取消。 | Release Verified |
| AUC-PRESENTATION-002 | Unit/Generator | ViewLocatorTests; AtomUICityIncrementalGeneratorPresentationTests | exact key、override/revoke、generator output 和确定性 lookup。 | 缺失、重复和歧义 mapping。 | Release Verified |
| AUC-PRESENTATION-003 | Unit/PlatformIntegration | ViewBindingTests | factory type、DataContext、真实 attach/detach 和释放。 | factory/binding/dispatcher 失败。 | Release Verified |
| AUC-PRESENTATION-004 | Unit/RuntimeLifecycle | RouteOutletTests | FIFO、plan reuse、commit、rollback 和 post-commit cleanup。 | queue full、commit/rollback 和 cleanup 失败。 | Release Verified |
| AUC-PRESENTATION-005 | Unit/PlatformIntegration | ViewBindingTests; VisualFeedbackTests | Bind 不伪造、identity 过滤、迟到事件和真实 visual feedback。 | handler failure、错误 identity 和释放后事件。 | Release Verified |
| AUC-PRESENTATION-006 | Unit/Contract | PresentationInteractionHandlerTests; ValidationVisualStateBindingTests | scope resolution、interaction 和 optional validation。 | handler 缺失、取消、revoke 和 validation adapter 失败。 | Release Verified |
| AUC-PRESENTATION-007 | Unit/RuntimeLifecycle | PresentationResourceRegistryTests; PresentationResourceDictionaryRevokerTests | owner revoke、覆盖恢复、字典 UI revoke 和局部失败继续。 | revoke 竞争和局部清理失败。 | Release Verified |
| AUC-PRESENTATION-008 | Unit/PluginLifecycle | ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests | active view 清理、插件 owner revoke 和卸载协调。 | active view、延迟清理和 unload failure。 | Release Verified |
| AUC-PRESENTATION-009 | RuntimeLifecycle/PlatformIntegration | PresentationRuntimeTests; PresentationHeadlessProcessTests; PresentationDesktopProcessTests | Attach、Show 前注册、named outlet 和 Window 生命周期。 | attach/show/close/stop 竞争。 | Release Verified |
| AUC-PRESENTATION-010 | Unit/RuntimeLifecycle | ViewModelFactoryTests; RouteOutletTests | ViewModel acquisition、ownership、scope 和 Outlet commit。 | factory、ownership 和释放失败。 | Release Verified |
| AUC-PRESENTATION-011 | Unit/RuntimeLifecycle | PresentationInteractionHandlerTests | modal FIFO、跨 Window 并行、capacity 和 cancel/revoke。 | queue full、取消、handler/revoke 失败。 | Release Verified |
| AUC-PRESENTATION-012 | Unit/Generator | ViewLocatorTests; AtomUICityIncrementalGeneratorPresentationTests | layered override、revoke 和 generated registration。 | 优先级、冲突和恢复失败。 | Release Verified |
| AUC-PRESENTATION-013 | Contract | PresentationResourceRegistryTests | Retired identity 保留且不重新进入运行时合同。 | 编号复用或重新暴露。 | Retired Before 1.0 |
| AUC-PRESENTATION-014 | Unit/RuntimeLifecycle | PresentationIndustrialContractTests | hierarchical fault、Faulted 和恢复边界。 | 普通故障升级、不变量失败和诊断失败。 | Release Verified |
| AUC-PRESENTATION-015 | Unit/RuntimeLifecycle | PresentationIndustrialContractTests | bounded admission、candidate ownership、FIFO 和 exactly-once。 | queue full、候选泄漏和所有权重复。 | Release Verified |
| AUC-PRESENTATION-016 | RuntimeLifecycle/PlatformIntegration | PresentationRuntimeTests; PresentationDesktopProcessTests | close origins、唯一 confirmation 和 close/stop race。 | 重复确认、拒绝关闭和并发停止。 | Release Verified |
| AUC-PRESENTATION-017 | Unit/RuntimeLifecycle | ViewModelCompositionTests; PresentationRuntimeTests | Root/Child attach、独立 Scope、leaf-to-root、部分失败和 Outlet 隔离。 | ownership 转移、close/stop race 和释放失败。 | Release Verified |
| AUC-PRESENTATION-018 | Unit/Contract | UiCommandFactoryTests; PresentationIndustrialContractTests | 聚合注册、重载转发、DI 注入和通知失败隔离。 | 自定义注册覆盖、参数丢失和 diagnostics failure。 | Release Verified |
| AUC-PRESENTATION-019 | FrameworkIntegration/RuntimeLifecycle | RoutePresentationCoordinatorTests | Router/Outlet 协调、恢复、exactly-once、取消、串行和诊断隔离。 | plan/Outlet 失败、stopped Outlet、迟到恢复和竞争。 | Release Verified |

Feature 017 的 nested child/task drain、activation rollback、显式 dispose 与 Runtime stop 竞态、真实 Headless Window close、RouteOutlet 隔离、Borrowed 跨 owner 独占/转移及 throwing-diagnostics 已进入可重复测试。详细证据见 [viewmodel-composition.md](viewmodel-composition.md)。

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

2026-10-21 增量发布证据：锁定 SDK `10.0.300` 下 Presentation `188/188`（非平台筛选 `187/187`）、Routing `177/177`、16 个非平台项目合计 `2250/2250`；Release net8.0/net10.0 全解构建、724 条 Presentation frozen signatures、Public API governance `4/4`、全解决方案 pack 和隔离 `win-x64` NuGet consumer 均通过。consumer 同时证明 `coordination=restored`、`composition=disposed`、`interaction=completed`、`runtime=stopped`。

## 发布门禁

- Windows desktop self-test 必须创建并显示真实 Window，完成主/命名 Outlet 导航、真实 attached/detached 回执、Interaction、三类关闭映射和资源释放。
- Release build、unit、generator、headless、stress、desktop 均为零失败。
- 相对批准 baseline，执行时间回退不超过 15%，分配回退不超过 10%。
- Linux/macOS 只要求 build/publish；没有平台 smoke 证据时保持 experimental。
- `PublicAPI.Shipped.txt`、PublicApiAnalyzers、strict SDK package validation、XML 文件和 SourceLink 必须通过。
- 隔离 package consumer 只能从本地候选 NuGet 包引用 Presentation 及其依赖，不允许 ProjectReference；必须完成 View lookup、Outlet commit、Routing-Presentation 条件恢复、ViewModel composition、Interaction 和 Runtime stop。

Windows 发布候选的统一入口是：

```powershell
./engineering/check-presentation-release.ps1
```

该命令顺序执行 format、Release 零警告 build、工程合同、Presentation unit/Headless/stress/desktop、generator、public API/SourceLink、包消费者和批准基线比较，并输出候选内容指纹。任一子门禁失败时不得标记或发布候选。

## 失败注入

必须覆盖 lookup/create/bind、guard/confirm、activation/deactivation、dispatcher、temporary attach/restore、old Entry cleanup、failure presenter、Window cleanup、plugin revoke、queue overflow、plan factory failure、Router conditional restore rejection 和 throwing diagnostics。测试断言状态、Result、diagnostic code、关键 context、当前 UI 与所有权释放。

## Public API 合同覆盖

- DI 扩展入口由 `AvaloniaUiDispatcherTests`、`PresentationInteractionHandlerTests`、`PresentationRuntimeTests`、`ViewLocatorTests`、`ActivePluginViewRegistryTests`、`PresentationResourceRegistryTests` 和 `PresentationPluginUnloadCoordinatorTests` 证明 `TryAdd`、同实例映射和重复注册行为。
- `ViewFactoryContext`、`ViewModelAcquisitionRequest`、`RouteOutletCommitResult`、`RouteOutletProperties`、`ICommandBindingHandle`、`UiCommandState`、`ValidationVisualStateSnapshot` 与 null failure presenter 由 View/Outlet/Command/Validation unit tests 及 Headless/Desktop fixtures 覆盖。
- `VisualLifecycleEvent`、`VisualLifecycleEventKind`、`VisualLifecycleSubscriptionOptions` 和 `UiStateFeedbackKind/Policy` 由 `VisualFeedbackTests`、`ViewBindingTests` 与真实 visual tree fixtures 覆盖。
- active view、resource contribution/lease/dictionary revocation 和 unload request/error/result/coordinator 是 Presentation-owned plugin-facing contract；其测试只证明 Presentation cleanup，不作为 PluginSystem 已完成的证据。
- 文档和 Public API baseline 必须对每个公开类型保留可定位的精确类型名；只写通配类型族不能替代 API 卡。
