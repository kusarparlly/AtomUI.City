# AtomUI.City.Presentation Detailed Design

本文只提供实现导航。规范性总合同见 [industrial-design.md](industrial-design.md)。

## 模块组成

| 区域 | 关键实现 | 专题合同 |
| --- | --- | --- |
| Runtime | PresentationModule, AddPresentation, PresentationRuntime, WindowSession | [ui-runtime.md](ui-runtime.md), [lifecycle.md](lifecycle.md) |
| Dispatcher | AvaloniaUiDispatcher | [dispatcher.md](dispatcher.md), [threading.md](threading.md) |
| View | ViewRegistry, ViewFactory, ViewBinder, DefaultViewModelFactory | [view-locator.md](view-locator.md), [view-binding.md](view-binding.md) |
| Outlet | RouteOutlet, commit plan, PresentationEntry, bounded lane | [route-outlet.md](route-outlet.md) |
| Feedback | VisualLifecycleHub, AvaloniaVisualLifecycleSubscription | [activation-integration.md](activation-integration.md) |
| Interaction | InteractionHandlerRegistry, CommandBinding, ValidationVisualStateBinding | [interaction-and-validation.md](interaction-and-validation.md) |
| Plugin UI | resource/active-view registries and unload coordinator | [resources-and-plugins.md](resources-and-plugins.md) |

## 关键事务

### Startup

`UseModule<PresentationModule>` 和 `AddPresentation` 都调用同一 aggregate registration。Host 与 Avalonia 分阶段启动；`Attach` 发生在 Avalonia 初始化后且不修改 provider。

### Navigation commit

adapter 创建单次 plan；Outlet admission 获取所有权；guard 和 activation 在 UI 线程外；event subscribe、temporary attach、rollback、unbind 在 UI dispatcher；final commit 原子替换 current Entry；旧 Entry 清理失败不回滚。

### Close

Window 原生 closing 映射成明确 origin。可拒绝路径执行 guards 和唯一 confirmation；不可拒绝路径直接提交。所有 Outlet 清空并停止，移除 Window 事件和 attached session，最后释放 WindowScope。

### Backpressure

Outlet 与每 Window modal Interaction 共用内部 bounded serial lane 模型：一个 worker、FIFO pending queue、reject-newest。terminal cleanup 作为 control item 排在已接收工作之后，不受业务容量限制。

## 文档索引

- [architecture.md](architecture.md)：对象和故障域。
- [api-contracts.md](api-contracts.md)：公开 API card。
- [diagnostics.md](diagnostics.md)：诊断码。
- [features.md](features.md)：Feature 状态。
- [testing.md](testing.md)：可执行验收。
- [state-and-localization.md](state-and-localization.md)：严格跨模块边界。
- [compatibility.md](compatibility.md)：1.0 稳定面。
